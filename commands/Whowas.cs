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
using System.IO;
using Microsoft.Data.Sqlite;
using iconnect;

namespace commands
{
    class Whowas
    {
        private static String DataPath { get; set; }

        public static void Setup()
        {
            DataPath = Path.Combine(Server.DataPath, "whowas.db");

            if (!File.Exists(DataPath))
                CreateDatabase();
            else
            {
                int time = (int)(Server.Time - 1209600);

                using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
                {
                    connection.Open();

                    using (SqliteCommand command = new SqliteCommand("delete from whowas where jointime<@jointime", connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@jointime", time));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public static LastSeenResult Last(IUser client)
        {
            LastSeenResult result = null;

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"select * from whowas where ip=@ip order by jointime desc";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@ip", client.ExternalIP.ToString()));

                    using (SqliteDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                        {
                            String name = (String)reader["name"];

                            if (name != client.Name)
                            {
                                result = new LastSeenResult();
                                result.Name = name;
                                result.Time = (int)(long)reader["jointime"];
                            }
                        }
                }
            }

            return result;
        }

        public static void Add(IUser client)
        {
            int time = (int)(Server.Time - 1);

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();
                bool exists = false;

                String query = @"select * from whowas where name=@name and ip=@ip";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@name", client.Name));
                    command.Parameters.Add(new SqliteParameter("@ip", client.ExternalIP.ToString()));

                    using (SqliteDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            exists = true;
                }

                if (!exists)
                {
                    query = @"insert into whowas (name, version, ip, jointime) 
                                 values (@name, @version, @ip, @jointime)";

                    using (SqliteCommand command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@name", client.Name));
                        command.Parameters.Add(new SqliteParameter("@version", client.Version));
                        command.Parameters.Add(new SqliteParameter("@ip", client.ExternalIP.ToString()));
                        command.Parameters.Add(new SqliteParameter("@jointime", time));
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    query = @"update whowas set jointime=@jointime where name=@name and ip=@ip";

                    using (SqliteCommand command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@name", client.Name));
                        command.Parameters.Add(new SqliteParameter("@ip", client.ExternalIP.ToString()));
                        command.Parameters.Add(new SqliteParameter("@jointime", time));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void Query(IUser admin, String q)
        {
            String query = @"select * from whowas
                             where name like @name
                             or ip like @ip
                             order by jointime desc
                             limit 50";

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@name", "%" + q + "%"));
                    command.Parameters.Add(new SqliteParameter("@ip", q + "%"));
                    bool exists = false;

                    using (SqliteDataReader reader = command.ExecuteReader())
                        while (reader.Read())
                        {
                            exists = true;
                            String name = (String)reader["name"];
                            String version = (String)reader["version"];
                            String ip = (String)reader["ip"];
                            int time = (int)(long)reader["jointime"];

                            admin.Print(Template.Text(Category.WhoWas, 0).Replace("+n",
                                name).Replace("+v", version).Replace("+ip", ip).Replace("+t", Helpers.UnixTimeToDateString((uint)time)));
                        }

                    if (!exists)
                        admin.Print(Template.Text(Category.WhoWas, 1).Replace("+n", q));
                }
            }
        }

        private static void CreateDatabase()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"create table if not exists whowas
                                 (
                                     name text not null,
                                     version text not null,
                                     ip text not null,
                                     jointime int not null
                                 )";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                    command.ExecuteNonQuery();
            }
        }
    }

    class LastSeenResult
    {
        public String Name { get; set; }
        public int Time { get; set; }
    }
}
