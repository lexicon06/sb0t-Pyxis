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
using System.Security.Cryptography;

namespace core
{
    class ObSalt
    {
        private static String DataPath { get; set; }
        private static List<Item> list { get; set; }

        private class Item
        {
            public IPAddress Address { get; set; }
            public byte[] Salt { get; set; }
            public uint LastUsed { get; set; }
        }

        public static void Init()
        {
            DataPath = DataPaths.Dat;

            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            DataPath += "\\salt.dat";

            CreateDatabase();

            list = new List<Item>();
            uint time = Helpers.UnixTime;

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand("delete from salt where s < @s", connection))
                {
                    int offset = (int)time;
                    offset -= 1209600;
                    command.Parameters.Add(new SqliteParameter("@s", offset));
                    command.ExecuteNonQuery();
                }

                using (SqliteCommand command = new SqliteCommand("select * from salt", connection))
                using (SqliteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                    {
                        String ip = (String)reader["ip"];
                        String s = (String)reader["s"];
                        Item item = new Item();
                        item.Address = IPAddress.Parse(ip);
                        item.Salt = new Guid(s).ToByteArray();
                        item.LastUsed = (uint)(int)(long)reader["lu"];
                        list.Add(item);
                    }
            }
        }

        public static void GetSalt(IClient client)
        {
            uint time = Helpers.UnixTime;
            Item item = list.Find(x => x.Address.Equals(client.ExternalIP));

            if (item != null)
            {
                using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
                {
                    connection.Open();

                    using (SqliteCommand command = new SqliteCommand("update salt set lu=@lu where ip=@ip", connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@lu", (int)time));
                        command.Parameters.Add(new SqliteParameter("@ip", client.ExternalIP.ToString()));
                        command.ExecuteNonQuery();
                    }
                }
            }
            else
            {
                item = new Item();
                item.Address = client.ExternalIP;
                item.Salt = Guid.NewGuid().ToByteArray();
                item.LastUsed = time;
                list.Add(item);

                using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
                {
                    connection.Open();

                    using (SqliteCommand command = new SqliteCommand("insert into salt (ip, s, lu) values (@ip, @s, @lu)", connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@ip", item.Address.ToString()));
                        command.Parameters.Add(new SqliteParameter("@s", new Guid(item.Salt).ToString()));
                        command.Parameters.Add(new SqliteParameter("@lu", (int)item.LastUsed));
                        command.ExecuteNonQuery();
                    }
                }
            }

            byte[] addr = client.ExternalIP.GetAddressBytes();
            byte[] salt = addr.Concat(item.Salt).ToArray();

            int excess = 2;
            int h = 19, l = 0;

            using (SHA1 sha1 = SHA1.Create())
                salt = sha1.ComputeHash(salt);

            for (int i = 0; i < excess; i++)
                addr[addr.Length - (i + 1)] = salt[i % 2 == 0 ? h-- : l++];

            client.ExternalIP = new IPAddress(addr);
        }

        private static void CreateDatabase()
        {

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"create table if not exists salt
                                 (
                                     ip text not null,
                                     s text not null,
                                     lu int not null
                                 )";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                    command.ExecuteNonQuery();
            }
        }
    }
}
