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
using System.IO;
using System.Net;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace core.Udp
{
    class UdpNodeManager
    {
        private static List<UdpNode> Nodes { get; set; }
        private static String DataPath { get; set; }

        public static UdpNode Find(Predicate<UdpNode> condition)
        {
            return Nodes.Find(condition);
        }

        public static void Initialize()
        {
            DataPath = DataPaths.Root;

            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            DataPath += "\\nodes.xml";

            if (!LoadList())
                LoadDefaultList();

            if (Nodes == null)
                Nodes = new List<UdpNode>();
        }

        public static void Add(EndPoint ep)
        {
            UdpNode node = new UdpNode();
            node.IP = ((IPEndPoint)ep).Address;
            node.Port = (ushort)((IPEndPoint)ep).Port;
            Add(node);
        }

        public static void Add(UdpNode server)
        {

            if (!server.IP.Equals(Settings.ExternalIP))
            {
                UdpNode sobj = Nodes.Find(s => s.IP.Equals(server.IP));

                if (sobj != null)
                    sobj.Port = server.Port;
                else
                {
                    if (Nodes.Count > 4000)
                    {
                        Nodes.Sort((x, y) => x.LastConnect.CompareTo(y.LastConnect));
                        Nodes.RemoveRange(0, 250);
                    }

                    Nodes.Add(server);
                }
            }
        }

        public static void Expire(ulong time)
        {
            Nodes.RemoveAll(s => s.Try > 4 &&
                (time - s.LastSentIPS) > 60000 &&
                (time - s.LastConnect) > 3600000);

            if (Nodes.Count < 10)
            {
                if (LoadDefaultList())
                    ServerCore.Log("local node list expired");
            }
        }

        public static void Update(ulong time)
        {
            Nodes.ForEachWhere(x => x.Ack = 40000, x => x.Ack > 65000);

            try
            {
                var linq = from x in Nodes
                           where x.Ack > 0 && (time - x.LastConnect) < 1800000
                           orderby x.Ack descending
                           select x;

                int counter = 0;
                XDocument xml = new XDocument(new XElement("NodeList"));

                foreach (UdpNode i in linq)
                {
                    xml.Element("NodeList").Add(new XElement("node",
                        new XElement("ip", i.IP),
                        new XElement("port", i.Port),
                        new XElement("ack", i.Ack > 65000 ? 40000 : i.Ack)));

                    if (++counter == 100)
                        break;
                }

                xml.Save(DataPath);
            }
            catch { }

            if (UdpStats.ACKIPS == 0)
                LoadDefaultList();
        }

        private static bool LoadList()
        {
            try
            {
                XDocument xml = XDocument.Load(DataPath);

                Nodes = (from i in xml.Descendants("node")
                         select new UdpNode
                         {
                             IP = IPAddress.Parse(i.Element("ip").Value),
                             Port = ushort.Parse(i.Element("port").Value),
                             Ack = int.Parse(i.Element("ack").Value)
                         }).ToList<UdpNode>();


                if (Nodes.Count == 0)
                    return false;

                ServerCore.Log("local node list loaded [" + Nodes.Count + "]");
                return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Fallback node list used when nodes.xml is missing and no Supabase
        /// configuration is present. These are well-known Ares chat servers so
        /// a fresh install (e.g. Linux) starts with peers to discover rooms.
        /// </summary>
        private static readonly string[] DefaultNodes =
        {
            "100.8.72.86:55555", "103.39.209.167:54321", "103.39.210.194:54321",
            "146.103.45.73:54321", "151.241.99.24:54321", "151.243.101.142:54321",
            "153.75.247.202:10000", "154.7.253.134:54321", "163.176.241.96:10000",
            "163.245.209.101:6890", "173.208.251.68:11000", "173.208.251.70:1000",
            "173.208.251.70:5009", "173.208.251.70:5010", "173.208.251.70:5011",
            "18.227.73.94:43827", "181.41.201.44:54321", "186.60.25.100:5000",
            "186.61.106.231:4040", "189.173.171.172:10010", "192.227.173.86:54321",
            "192.227.229.81:54321", "192.3.152.150:54321", "192.3.152.152:54321",
            "193.109.193.17:54321", "198.12.89.162:54321", "198.12.89.163:54321",
            "198.12.89.190:54321", "198.12.89.207:54321", "198.12.89.213:54321",
            "198.12.89.216:54321", "198.23.188.166:54321", "198.23.188.208:54321",
            "200.155.158.226:54321", "202.155.8.165:54321", "202.155.8.230:54321",
            "202.155.8.98:54321", "208.116.56.118:44271", "212.100.173.105:10000",
            "213.145.86.95:54321", "37.202.222.132:54321", "37.202.222.165:54321",
            "37.202.222.221:54321", "38.134.148.67:54321", "38.146.27.117:54321",
            "41.193.12.218:54321", "45.58.168.217:12000", "45.58.168.54:10000",
            "51.222.186.106:10000", "51.68.74.129:54321", "54.39.20.14:5009",
            "57.128.243.45:54321", "65.21.240.33:54321", "66.150.198.189:54321",
            "74.208.112.247:50000", "74.208.112.247:54321", "74.91.112.251:11000",
            "77.42.79.24:62175", "83.39.191.103:5010", "92.112.53.107:54321"
        };

        private static bool LoadDefaultList()
        {
            if (!SupabaseClient.IsConfigured)
            {
                // No nodes.xml and no Supabase: seed with the built-in node
                // list so the server has peers immediately (any OS). Persist
                // them to nodes.xml for subsequent starts.
                try
                {
                    Nodes = new List<UdpNode>();

                    foreach (String entry in DefaultNodes)
                    {
                        String[] parts = entry.Split(':');

                        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out IPAddress ip))
                            Nodes.Add(new UdpNode
                            {
                                IP = ip,
                                Port = ushort.Parse(parts[1]),
                                Ack = 0
                            });
                    }

                    if (Nodes.Count > 0)
                    {
                        SaveNodes();
                        ServerCore.Log("default node list seeded [" + Nodes.Count + "]");
                        return true;
                    }
                }
                catch { }

                if (Nodes == null)
                    Nodes = new List<UdpNode>();
                return false;
            }

            try
            {
                Nodes = new List<UdpNode>();

                var response = Task.Run(() => SupabaseClient.InitializeAsync("", "")).Result;
                if (!response) return false;
                var result = Task.Run(() => SupabaseClient.GetChatroomsAsync()).Result;

                for (int i = 0; i < result.Count; i++)
                {
                    Nodes.Add(toUdpNode(result[i].Ip, result[i].Port));
                }
                ServerCore.Log("default node list loaded from supabase");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static UdpNode toUdpNode(String ip, ushort port)
        {
            UdpNode udpNode = new UdpNode();
            udpNode.IP = IPAddress.Parse(ip);
            udpNode.Port = port;
            return udpNode;
        }

        private static void SaveNodes()
        {
            try
            {
                XDocument xml = new XDocument(new XElement("NodeList"));

                foreach (UdpNode i in Nodes)
                {
                    xml.Element("NodeList").Add(new XElement("node",
                        new XElement("ip", i.IP),
                        new XElement("port", i.Port),
                        new XElement("ack", i.Ack)));
                }

                xml.Save(DataPath);
            }
            catch { }
        }

        public static UdpNode[] GetServers(int max_servers, ulong time)
        {
            Nodes.Randomize();

            List<UdpNode> results = Nodes.FindAll(s => s.Ack > 0
                && (s.LastConnect + 900000) > time);

            if (results.Count > max_servers)
                results = results.GetRange(0, max_servers);

            return results.ToArray();
        }

        public static UdpNode[] GetServers(IPAddress target_ip, int max_servers, ulong time)
        {
            Nodes.Randomize();

            List<UdpNode> results = Nodes.FindAll(s => !s.IP.Equals(target_ip)
                && s.Ack > 0 && (s.LastConnect + 900000) > time);

            if (results.Count > max_servers)
                results = results.GetRange(0, max_servers);

            return results.ToArray();
        }

        public static UdpNode[] GetServers()
        {
            return Nodes.ToArray();
        }

        public static UdpNode NextPusher(ulong time)
        {
            if (Nodes.Count == 0)
                return null;

            Nodes.Sort((x, y) => x.LastSentIPS.CompareTo(y.LastSentIPS));

            if ((Nodes[0].LastSentIPS + 900000) < time)
            {
                Nodes[0].LastSentIPS = time;
                Nodes[0].Try++;
                return Nodes[0];
            }

            return null;
        }
    }
}