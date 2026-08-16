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
using System.Net;
using System.Diagnostics;
using System.IO;
using iconnect;
using scripting;

namespace core
{
    public class Settings
    {
        public const String RELEASE_URL = "https://github.com/lexicon06/sb0t-Pyxis/releases";
        public const String VERSION_CHECK_URL = "https://api.github.com/repos/lexicon06/sb0t-Pyxis/releases";
        public const String VERSION_NUMBER = "5.43.5";

        public const String VERSION = "sb0t Pyxis " + VERSION_NUMBER;
        public const ushort LINK_PROTO = 500;

        public static bool RUNNING { get; set; }
        public static String WebPath { get; set; }

        public static void Reset()
        {
            externalip = null;
            port = 0;
            name = null;
            language = 0;
            hide_ips = 0;

            WebPath = DataPaths.Root + "\\Style";

            if (!Directory.Exists(WebPath))
            {
                Directory.CreateDirectory(WebPath);
                WebPath += "\\";
            }
            else WebPath += "\\";

            DoOnce.Run();
        }

        public static void ScriptCanLevel(bool can)
        {
            Events.ScriptCanLevel(can);
        }

        private static int hide_ips = 0;
        public static bool HideIps
        {
            get
            {
                if (hide_ips == 0)
                    hide_ips = Get<bool>("hide_ips") ? 2 : 1;

                return hide_ips == 2;
            }
        }

        private static IPAddress externalip { get; set; }
        public static IPAddress ExternalIP
        {
            get
            {
                if (externalip != null)
                    return externalip;

                byte[] buffer = Get<byte[]>("ip");

                if (buffer != null)
                    externalip = new IPAddress(buffer);
                else
                    externalip = IPAddress.Loopback;

                return externalip;
            }
            set
            {
                externalip = value;
                Set("ip", externalip.GetAddressBytes());
            }
        }

        private static ushort port { get; set; }
        public static ushort Port
        {
            get
            {
                if (port == 0)
                    port = Get<ushort>("port");

                return port;
            }
        }

        private static String name { get; set; }
        public static String Name
        {
            get
            {
                if (name == null)
                    name = Get<String>("name");

                return name;
            }
        }

        private static byte language { get; set; }
        public static byte Language
        {
            get
            {
                if (language == 0)
                    language = Get<byte>("language");

                return language;
            }
        }

        private static String topic { get; set; }
        public static String Topic
        {
            get
            {
                if (topic == null)
                    topic = Get<String>("topic");

                if (topic.Length < 2)
                {
                    topic = "welcome to my room";
                    Set("topic", topic);
                }

                return topic;
            }
            set
            {
                topic = value;
                Set("topic", topic);
            }
        }

        private static Type[] AcceptableTypes = 
        {
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(String),
            typeof(byte[]),
            typeof(bool)
        };

        public static T Get<T>(String name, params String[] subkeys)
        {
            return SettingsStore.Get<T>(name, subkeys);
        }

        public static bool Set(String name, object value, params String[] subkeys)
        {
            bool result = SettingsStore.Set(name, value, subkeys);

            if (result && name == "url" && value is String)
                LiveScript.liveScriptsEndpoint = (String)value;

            return result;
        }

        public static IPAddress LocalIP
        {
            get
            {
                foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        return ip;

                return IPAddress.Loopback;
            }
        }
    }
}
