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
using System.Threading.Tasks;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;

namespace core
{
    class IgnoreManager
    {
        private static Dictionary<Guid, List<string>> ignores = new Dictionary<Guid, List<string>>();
        private static string path { get; set; }

        public static void LoadIgnores(IClient client)
        {
            List<string> list;
            ignores.TryGetValue(client.Guid, out list);

            if(list != null)
            {
                foreach(string str in list)
                {
                    client.IgnoreList.Add(str);
                }
            }
        }

        public static void AddIgnore(IClient client, string str)
        {
            if(AddIgnore(client.Guid, str))
            {
                using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
                {
                    connection.Open();

                    String query = @"insert into ignores (guid, str) values (@guid, @str)";

                    using (SqliteCommand command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.Add(new SqliteParameter("@guid", client.Guid.ToString()));
                        command.Parameters.Add(new SqliteParameter("@str", str));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private static bool AddIgnore(Guid guid, string str)
        {
            List<string> list;
            ignores.TryGetValue(guid, out list);

            if (list == null)
            {
                list = new List<string>();
            }

            if(list.Contains(str))
            {
                return false;
            }

            list.Add(str);
            ignores[guid] = list;
            return true;
        }

        public static void RemoveIgnore(IClient client, string str)
        {
            List<string> list;
            ignores.TryGetValue(client.Guid, out list);

            if (list == null)
            {
                return;
            }

            if (!list.Contains(str))
            {
                return;
            }

            list.Remove(str);
            ignores[client.Guid] = list;

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();

                String query = @"delete from ignores where guid = @guid and str = @str";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@guid", client.Guid.ToString()));
                    command.Parameters.Add(new SqliteParameter("@str", str));
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void init()
        {
            path = DataPaths.Dat;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            path += "\\ignores.dat";

            CreateDatabase();

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand("select * from ignores", connection))
                {
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            AddIgnore(new Guid((String)reader["guid"]), (String)reader["str"]);
                        }
                    }
                }
            }
        }

        private static void CreateDatabase()
        {

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();

                String query = @"create table if not exists ignores
                                 (
                                     guid text not null,
                                     str text not null
                                 )";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

    }
}
