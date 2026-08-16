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
using System.Security.Cryptography;
using System.Net;

namespace core.LinkHub
{
    public class TrustedLeavesManager
    {
        private static List<TrustedLeafItem> items { get; set; }
        private static String DataPath { get; set; }

        public static void Init()
        {
            items = new List<TrustedLeafItem>();

            DataPath = DataPaths.Dat;

            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            DataPath += "\\trusted.dat";

            CreateDatabase();

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand("select * from trusted", connection))
                using (SqliteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        items.Add(new TrustedLeafItem
                        {
                            Name = (String)reader["name"],
                            Guid = new Guid((String)reader["guid"])
                        });
            }
        }

        private static void CreateDatabase()
        {

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"create table if not exists trusted
                                 (
                                     name text not null,
                                     guid text not null
                                 )";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                    command.ExecuteNonQuery();
            }
        }

        public static bool AddItem(TrustedLeafItem item)
        {
            if (IsTrusted(item))
                return false;

            items.Add(item);

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"insert into trusted (name, guid) values (@name, @guid)";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@name", item.Name));
                    command.Parameters.Add(new SqliteParameter("@guid", item.Guid.ToString()));
                    command.ExecuteNonQuery();
                }
            }

            return true;
        }

        public static void RemoveItem(TrustedLeafItem item)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"delete from trusted where name=@name and guid=@guid";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@name", item.Name));
                    command.Parameters.Add(new SqliteParameter("@guid", item.Guid.ToString()));
                    command.ExecuteNonQuery();
                }
            }

            items.RemoveAll(x => x.Guid.Equals(item.Guid) && x.Name == item.Name);
        }

        public static TrustedLeafItem[] Items
        {
            get { return items.ToArray(); }
        }

        public static bool IsTrusted(TrustedLeafItem item)
        {
            return items.Find(x => x.Guid.Equals(item.Guid) && x.Name == item.Name) != null;
        }

        public static TrustedLeafItem GetTrusted(IPAddress ip, ushort port, byte[] data)
        {
            if (ip.Equals(IPAddress.Loopback) && port == Settings.Port)
            {
                return new TrustedLeafItem
                {
                    Guid = new Guid(Settings.Get<byte[]>("guid")),
                    Name = Settings.Name
                };
            }

            TrustedLeafItem result = null;

            using (SHA1 sha1 = SHA1.Create())
                foreach (TrustedLeafItem i in items)
                {
                    List<byte> list = new List<byte>();
                    list.AddRange(Encoding.UTF8.GetBytes(i.Name));
                    list.AddRange(i.Guid.ToByteArray());
                    list.Reverse();
                    byte[] buf = sha1.ComputeHash(list.ToArray());

                    if (buf.SequenceEqual(data))
                    {
                        result = i;
                        break;
                    }
                }

            return result;
        }
    }
}
