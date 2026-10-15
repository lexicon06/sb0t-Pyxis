/*
    sb0t ares chat server
    Copyright (C) 2017  AresChat

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace core.ib0t
{
    // Guarda los mensajes recientes de cada sala (vroom) y de cada
    // conversacion privada junto con sus reacciones. Sirve para:
    //   (a) validar que una reaccion apunta a un mensaje que existe,
    //   (b) reproducir los mensajes recientes (con sus reacciones) cuando
    //       un cliente inbizier entra o se reconecta.
    class MessageBuffer
    {
        public const int MAX_ROOM = 100;
        public const int MAX_PM = 100;
        // Limites de conversaciones/salas retenidas en memoria (evita que el
        // buffer crezca sin control en servidores de larga vida).
        public const int MAX_PM_CONVS = 500;
        public const int MAX_ROOMS = 1000;

        public class Entry
        {
            public String Id { get; set; }
            public String Sender { get; set; }
            public String Text { get; set; }
            public bool Emote { get; set; }
            public ulong Time { get; set; }
            // emoji -> conjunto de nombres que reaccionaron
            public Dictionary<String, HashSet<String>> Reactions { get; set; }

            public Entry()
            {
                this.Reactions = new Dictionary<String, HashSet<String>>();
            }
        }

        private static long id_counter = 0;
        private static readonly Object id_lock = new Object();
        private static readonly Object sync = new Object();

        private static Dictionary<ushort, List<Entry>> rooms = new Dictionary<ushort, List<Entry>>();
        private static Dictionary<String, List<Entry>> pms = new Dictionary<String, List<Entry>>();
        // Orden de creacion para poder desalojar los buffers mas antiguos.
        private static readonly Queue<ushort> room_order = new Queue<ushort>();
        private static readonly Queue<String> pm_order = new Queue<String>();

        // Devuelve un id unico por sesion de servidor (hex corto).
        public static String NextId()
        {
            lock (id_lock)
                return (++id_counter).ToString("x");
        }

        public static void AddRoom(ushort vroom, String id, String sender, String text, bool emote, ulong time)
        {
            if (String.IsNullOrEmpty(id)) return;

            lock (sync)
            {
                List<Entry> list;

                if (!rooms.TryGetValue(vroom, out list))
                {
                    list = new List<Entry>();
                    rooms[vroom] = list;
                    room_order.Enqueue(vroom);

                    while (room_order.Count > MAX_ROOMS)
                    {
                        ushort old = room_order.Dequeue();

                        if (old != vroom)
                            rooms.Remove(old);
                    }
                }

                list.Add(new Entry { Id = id, Sender = sender, Text = text, Emote = emote, Time = time });

                if (list.Count > MAX_ROOM)
                    list.RemoveRange(0, list.Count - MAX_ROOM);
            }
        }

        public static bool ApplyRoomReaction(ushort vroom, String id, String name, String emoji, bool isAdd)
        {
            lock (sync)
            {
                List<Entry> list;

                if (!rooms.TryGetValue(vroom, out list))
                    return false;

                Entry e = list.Find(x => x.Id == id);

                if (e == null)
                    return false;

                HashSet<String> names;

                if (!e.Reactions.TryGetValue(emoji, out names))
                {
                    if (!isAdd)
                        return false;

                    names = new HashSet<String>();
                    e.Reactions[emoji] = names;
                }

                if (isAdd)
                    names.Add(name);
                else
                {
                    names.Remove(name);

                    if (names.Count == 0)
                        e.Reactions.Remove(emoji);
                }

                return true;
            }
        }

        public static bool HasRoom(ushort vroom, String id)
        {
            lock (sync)
            {
                List<Entry> list;

                if (!rooms.TryGetValue(vroom, out list))
                    return false;

                return list.Exists(x => x.Id == id);
            }
        }

        public static List<Entry> RoomHistory(ushort vroom)
        {
            lock (sync)
            {
                List<Entry> list;

                if (!rooms.TryGetValue(vroom, out list))
                    return new List<Entry>();

                return new List<Entry>(list);
            }
        }

        // Convierte las reacciones a un JSON compacto (lo base64-ea el emisor).
        // Formato: [{"e":"emoji","n":["ana","leo"]}, ...]
        public static String ReactionsToJson(Entry e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            bool first = true;

            lock (sync)
            {
                foreach (KeyValuePair<String, HashSet<String>> kv in e.Reactions)
                {
                    if (kv.Value.Count == 0) continue;

                    if (!first) sb.Append(',');
                    first = false;

                    sb.Append("{\"e\":").Append(JsonString(kv.Key)).Append(",\"n\":[");
                    bool f2 = true;

                    foreach (String n in kv.Value)
                    {
                        if (!f2) sb.Append(',');
                        f2 = false;
                        sb.Append(JsonString(n));
                    }

                    sb.Append("]}");
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static String JsonString(String s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('"');

            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        // Envia al cliente el historial reciente de su sala actual (con
        // reacciones) para que lo pinte al entrar o al reconectar.
        public static void SendRoomHistory(ib0tClient client)
        {
            if (client == null || !client.LoggedIn)
                return;

            List<Entry> list = RoomHistory(client.Vroom);

            client.QueuePacket(WebOutbound.HistoryHead(client, list.Count));

            foreach (Entry e in list)
            {
                String json = ReactionsToJson(e);
                String b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                client.QueuePacket(WebOutbound.HistoryMsg(client, e.Sender, e.Text, e.Id, e.Time, b64, e.Emote));
            }

            client.QueuePacket(WebOutbound.HistoryEnd(client));
        }

        public static String PmKey(String a, String b)
        {
            return String.CompareOrdinal(a, b) <= 0 ? a + "\u0001" + b : b + "\u0001" + a;
        }

        public static void AddPm(String a, String b, String id, String sender, String text, ulong time)
        {
            if (String.IsNullOrEmpty(id)) return;

            String key = PmKey(a, b);

            lock (sync)
            {
                List<Entry> list;

                if (!pms.TryGetValue(key, out list))
                {
                    list = new List<Entry>();
                    pms[key] = list;
                    pm_order.Enqueue(key);

                    while (pm_order.Count > MAX_PM_CONVS)
                    {
                        String old = pm_order.Dequeue();

                        if (old != key)
                            pms.Remove(old);
                    }
                }

                list.Add(new Entry { Id = id, Sender = sender, Text = text, Time = time });

                if (list.Count > MAX_PM)
                    list.RemoveRange(0, list.Count - MAX_PM);
            }
        }

        public static bool HasPm(String a, String b, String id)
        {
            String key = PmKey(a, b);

            lock (sync)
            {
                List<Entry> list;

                if (!pms.TryGetValue(key, out list))
                    return false;

                return list.Exists(x => x.Id == id);
            }
        }

        public static bool ApplyPmReaction(String a, String b, String id, String name, String emoji, bool isAdd)
        {
            String key = PmKey(a, b);

            lock (sync)
            {
                List<Entry> list;

                if (!pms.TryGetValue(key, out list))
                    return false;

                Entry e = list.Find(x => x.Id == id);

                if (e == null)
                    return false;

                HashSet<String> names;

                if (!e.Reactions.TryGetValue(emoji, out names))
                {
                    if (!isAdd)
                        return false;

                    names = new HashSet<String>();
                    e.Reactions[emoji] = names;
                }

                if (isAdd)
                    names.Add(name);
                else
                {
                    names.Remove(name);

                    if (names.Count == 0)
                        e.Reactions.Remove(emoji);
                }

                return true;
            }
        }
    }
}
