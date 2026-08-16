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
using System.Data;
using Microsoft.Data.Sqlite;
using System.Net;
using iconnect;

namespace core
{
    class BanSystem
    {
        private static List<Ban> list;
        private static String DataPath { get; set; }

        /// <summary>
        /// True once the ban list has been loaded from disk.
        /// </summary>
        public static bool Initialized { get { return list != null; } }

        /// <summary>
        /// Loads the ban list from disk if it has not been loaded yet (used by
        /// the web panel to list/unban users while the server is stopped).
        /// </summary>
        public static void EnsureLoaded()
        {
            if (list == null)
                LoadBans();
        }

        public static bool IsBanned(IClient client)
        {
            return list.Find(x => x.ExternalIP.Equals(client.ExternalIP) ||
                x.Guid.Equals(client.Guid)) != null;
        }

        public static void Eval(Action<IBan> action)
        {
            for (int i = (list.Count - 1); i > -1; i--)
                action(list[i]);
        }

        public static void AddBan(IClient client)
        {
            Ban ban = new Ban
            {
                Name = client.Name,
                Version = client.Version,
                Guid = client.Guid,
                ExternalIP = client.ExternalIP,
                LocalIP = client.LocalIP,
                Port = client.DataPort,
                Ident = NextIdent
            };

            list.Add(ban);

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"insert into bans (name, version, guid, externalip, localip, port, ident) 
                                 values (@name, @version, @guid, @externalip, @localip, @port, @ident)";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@name", ban.Name));
                    command.Parameters.Add(new SqliteParameter("@version", ban.Version));
                    command.Parameters.Add(new SqliteParameter("@guid", ban.Guid.ToString()));
                    command.Parameters.Add(new SqliteParameter("@externalip", ban.ExternalIP.ToString()));
                    command.Parameters.Add(new SqliteParameter("@localip", ban.LocalIP.ToString()));
                    command.Parameters.Add(new SqliteParameter("@port", (int)ban.Port));
                    command.Parameters.Add(new SqliteParameter("@ident", (int)ban.Ident));
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void AddBan(IRecord record)
        {
            if (list.Find(x => x.ExternalIP.Equals(record.ExternalIP)) != null)
                return;

            if (list.Find(x => x.Guid.Equals(record.Guid)) != null)
                return;

            Ban ban = new Ban
            {
                Name = record.Name,
                Version = record.Version,
                Guid = record.Guid,
                ExternalIP = record.ExternalIP,
                LocalIP = record.LocalIP,
                Port = record.DataPort,
                Ident = NextIdent
            };

            list.Add(ban);

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"insert into bans (name, version, guid, externalip, localip, port, ident) 
                                 values (@name, @version, @guid, @externalip, @localip, @port, @ident)";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@name", ban.Name));
                    command.Parameters.Add(new SqliteParameter("@version", ban.Version));
                    command.Parameters.Add(new SqliteParameter("@guid", ban.Guid.ToString()));
                    command.Parameters.Add(new SqliteParameter("@externalip", ban.ExternalIP.ToString()));
                    command.Parameters.Add(new SqliteParameter("@localip", ban.LocalIP.ToString()));
                    command.Parameters.Add(new SqliteParameter("@port", (int)ban.Port));
                    command.Parameters.Add(new SqliteParameter("@ident", (int)ban.Ident));
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void RemoveBan(ushort ident)
        {
            Ban ban = list.Find(x => x.Ident == ident);

            if (ban != null)
            {
                using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
                {
                    connection.Open();

                    String query = @"delete from bans
                                     where ident=@ident";

                    using (SqliteCommand command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@ident", (int)ban.Ident));
                        command.ExecuteNonQuery();
                    }
                }

                list.RemoveAll(x => x.Ident == ident);
            }
        }

        public static void AutoClearBans()
        {
            uint time = Helpers.UnixTime;

            if (Settings.Get<bool>("auto_ban_clear_enabled"))
            {
                int interval = Settings.Get<int>("auto_ban_clear_interval");

                if (time > (LastAutoCleared + (interval * 3600)))
                {
                    LastAutoCleared = time;
                    ClearBans();
                    Events.BansAutoCleared();
                }
            }
        }

        public static void ClearBans()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"delete from bans";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                    command.ExecuteNonQuery();
            }

            list.Clear();
        }

        private static uint LastAutoCleared { get; set; }

        public static void LoadBans()
        {
            LastAutoCleared = Helpers.UnixTime;

            DataPath = DataPaths.Dat;

            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            DataPath += "\\banned.dat";

            CreateDatabase();

            list = new List<Ban>();

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand("select * from bans", connection))
                using (SqliteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        list.Add(new Ban
                        {
                            Name = (String)reader["name"],
                            Version = (String)reader["version"],
                            Guid = new Guid((String)reader["guid"]),
                            ExternalIP = IPAddress.Parse((String)reader["externalip"]),
                            LocalIP = IPAddress.Parse((String)reader["localip"]),
                            Port = (ushort)(int)(long)reader["port"],
                            Ident = (ushort)(int)(long)reader["ident"]
                        });
            }
        }

        private static void CreateDatabase()
        {

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"create table if not exists bans
                                 (
                                     name text not null,
                                     version text not null,
                                     guid text not null,
                                     externalip text not null,
                                     localip text not null,
                                     port int not null,
                                     ident int not null
                                 )";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                    command.ExecuteNonQuery();
            }
        }

        private static ushort NextIdent
        {
            get
            {
                ushort result = 0;

                for (ushort u = 0; u < ushort.MaxValue; u++)
                {
                    result = u;
                    int index = list.FindIndex(x => x.Ident == u);

                    if (index == -1)
                        break;
                }

                return result;
            }
        }
    }
}
