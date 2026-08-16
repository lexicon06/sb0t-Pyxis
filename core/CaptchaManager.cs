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

namespace core
{
    class CaptchaManager
    {
        private static List<CaptchaRecord> list { get; set; }
        private static String DataPath { get; set; }

        public static bool HasCaptcha(IClient client)
        {
            CaptchaRecord record = list.Find(x => x.Guid.Equals(client.Guid));

            if (record != null)
            {
                int time = (int)(uint)(Helpers.UnixTime - 1000000000);

                if (record.Time < (time - 604800))
                {
                    record.Time = time;

                    using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
                    {
                        connection.Open();

                        String query = @"update captchas set time=@time where guid=@guid";

                        using (SqliteCommand command = new SqliteCommand(query, connection))
                        {
                            command.Parameters.Add(new SqliteParameter("@time", record.Time));
                            command.Parameters.Add(new SqliteParameter("@guid", record.Guid.ToString()));
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }

            return record != null;
        }

        public static void AddCaptcha(IClient client)
        {
            if (list.Find(x => x.Guid.Equals(client.Guid)) != null)
                return;

            CaptchaRecord record = new CaptchaRecord
            {
                Guid = client.Guid,
                Time = (int)(uint)(Helpers.UnixTime - 1000000000)
            };

            list.Add(record);

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"insert into captchas (guid, time) values (@guid, @time)";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    command.Parameters.Add(new SqliteParameter("@guid", record.Guid.ToString()));
                    command.Parameters.Add(new SqliteParameter("@time", record.Time));
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void Load()
        {
            DataPath = DataPaths.Dat;

            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            DataPath += "\\captcha.dat";

            CreateDatabase();

            list = new List<CaptchaRecord>();

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand("delete from captchas where time<@time", connection))
                {
                    int time = (int)(uint)(Helpers.UnixTime - 1000000000);
                    time -= 1209600;
                    command.Parameters.Add(new SqliteParameter("@time", time));
                    command.ExecuteNonQuery();
                }
            }

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                using (SqliteCommand command = new SqliteCommand("select * from captchas", connection))
                using (SqliteDataReader reader = command.ExecuteReader())
                    while (reader.Read())
                        list.Add(new CaptchaRecord
                        {
                            Guid = new Guid((String)reader["guid"]),
                            Time = (int)(long)reader["time"]
                        });
            }
        }

        private static void CreateDatabase()
        {

            using (SqliteConnection connection = new SqliteConnection("Data Source=" + DataPath))
            {
                connection.Open();

                String query = @"create table if not exists captchas
                                 (
                                     guid text not null,
                                     time int not null
                                 )";

                using (SqliteCommand command = new SqliteCommand(query, connection))
                    command.ExecuteNonQuery();
            }
        }
    }

    class CaptchaRecord
    {
        public Guid Guid { get; set; }
        public int Time { get; set; }
    }
}
