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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using iconnect;

namespace commands
{
    class Greets
    {
        private static List<String> list { get; set; }

        public static void Add(String str)
        {
            if (!String.IsNullOrEmpty(str))
                if (list.Find(x => x == str) == null)
                {
                    list.Add(str);
                    Update();
                }
        }

        public static String Remove(int index)
        {
            if (index < 0 || index >= list.Count)
                return null;

            String str = list[index];
            list.RemoveAt(index);
            Update();
            return str;
        }

        public static void List(IUser client)
        {
            if (list.Count == 0)
                client.Print(Template.Text(Category.Greetings, 3));

            for (int i = 0; i < list.Count; i++)
                client.Print(i + " - " + list[i]);
        }

        private static int greet_index;
        private static int last_pick = -1;

        public static String GetGreet(IUser client)
        {
            if (list.Count > 0)
            {
                // Elegir una bienvenida al azar (antes era secuencial).
                int pick;

                if (list.Count == 1)
                    pick = 0;
                else
                {
                    // Evitar repetir la ultima usada para que no salga dos veces seguidas.
                    pick = (int)Math.Floor(new Random().NextDouble() * list.Count);

                    if (pick == last_pick)
                        pick = (pick + 1) % list.Count;
                }

                last_pick = pick;
                String s = list[pick];
                String _str = Helpers.CountryCodeToString(client.Country);
                List<String> list1 = new List<String>();
                Server.Users.Ares(x => list1.Add(x.Name));
                Server.Users.Web(x => list1.Add(x.Name));
                list1.RemoveAll(x => x == client.Name);
                String rnd_user = client.Name;

                if (list1.Count > 0)
                {
                    int index = (int)Math.Floor(new Random().NextDouble() * list1.Count);
                    rnd_user = list1[index];
                }

                if (_str != "?")
                    if (client.Region.Length > 0)
                        _str = client.Region + ", " + _str;

                if (_str == "?")
                    if (client.Region.Length > 0)
                        _str = client.Region;

                if (_str == "?")
                    _str = "unknown";

                s = ReplaceToken(s, "+n", client.Name);
                s = ReplaceToken(s, "+ip", client.ExternalIP.ToString());
                s = ReplaceToken(s, "+id", client.ID.ToString());
                s = ReplaceToken(s, "+f", client.FileCount.ToString());
                s = ReplaceToken(s, "+v", client.Version);
                s = ReplaceToken(s, "+p", client.DataPort.ToString());
                s = ReplaceToken(s, "+uc", Server.Stats.CurrentUserCount.ToString());
                s = ReplaceToken(s, "+rn", Server.Chatroom.Name);
                s = ReplaceToken(s, "+ut", Helpers.GetUptime);
                s = ReplaceToken(s, "+ru", rnd_user);
                s = ReplaceToken(s, "+l", _str);

                return s;
            }

            return "hello " + client.Name;
        }

        // Reemplaza un token sin importar mayusculas/minusculas (+id == +ID).
        // El orden de los tokens mas largos va primero para no cortar otros
        // (ej. +uc antes que +u... no hay +u, pero igual se respeta).
        private static String ReplaceToken(String text, String token, String value)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                text, System.Text.RegularExpressions.Regex.Escape(token), value,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        public static void Load()
        {
            greet_index = 0;
            last_pick = -1;
            list = new List<String>();

            try
            {
                XmlDocument xml = new XmlDocument();
                xml.PreserveWhitespace = true;
                xml.Load(Path.Combine(Server.DataPath, "greetings.xml"));

                XmlNodeList nodes = xml.GetElementsByTagName("item");

                foreach (XmlNode n in nodes)
                    list.Add(n.InnerText);
            }
            catch { }
        }

        private static void Update()
        {
            XmlDocument xml = new XmlDocument();
            xml.PreserveWhitespace = true;
            xml.AppendChild(xml.CreateXmlDeclaration("1.0", null, null));
            XmlNode root = xml.AppendChild(xml.CreateElement("greetings"));

            foreach (String i in list)
            {
                XmlNode item = root.OwnerDocument.CreateNode(XmlNodeType.Element, "item", root.BaseURI);
                root.AppendChild(item);
                item.InnerText = i;
            }

            try { xml.Save(Path.Combine(Server.DataPath, "greetings.xml")); }
            catch { }
        }

        private static String _pm { get; set; }

        public static void SetPM(String text)
        {
            _pm = text;
            Settings.PMGreetMsgText = _pm;
        }

        public static String GetPM(IUser client)
        {
            if (String.IsNullOrEmpty(_pm))
            {
                _pm = Settings.PMGreetMsgText;

                if (String.IsNullOrEmpty(_pm))
                    SetPM("welcome to my room +n");
            }

            String _str = Helpers.CountryCodeToString(client.Country);
            List<String> list1 = new List<String>();
            Server.Users.Ares(x => list1.Add(x.Name));
            Server.Users.Web(x => list1.Add(x.Name));
            list1.RemoveAll(x => x == client.Name);
            String rnd_user = client.Name;

            if (list1.Count > 0)
            {
                int index = (int)Math.Floor(new Random().NextDouble() * list1.Count);
                rnd_user = list1[index];
            }

            if (_str != "?")
                if (client.Region.Length > 0)
                    _str = client.Region + ", " + _str;

            if (_str == "?")
                if (client.Region.Length > 0)
                    _str = client.Region;

            if (_str == "?")
                _str = "unknown";

            String s = _pm;
            s = ReplaceToken(s, "+n", client.Name);
            s = ReplaceToken(s, "+ip", client.ExternalIP.ToString());
            s = ReplaceToken(s, "+id", client.ID.ToString());
            s = ReplaceToken(s, "+f", client.FileCount.ToString());
            s = ReplaceToken(s, "+v", client.Version);
            s = ReplaceToken(s, "+p", client.DataPort.ToString());
            s = ReplaceToken(s, "+uc", Server.Stats.CurrentUserCount.ToString());
            s = ReplaceToken(s, "+rn", Server.Chatroom.Name);
            s = ReplaceToken(s, "+ut", Helpers.GetUptime);
            s = ReplaceToken(s, "+ru", rnd_user);
            s = ReplaceToken(s, "+l", _str);
            return s;
        }
    }
}
