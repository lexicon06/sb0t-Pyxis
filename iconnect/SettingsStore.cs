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
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace iconnect
{
    /// <summary>
    /// Cross-platform replacement for the Windows Registry based settings store.
    /// Persists settings to a JSON file in the application data folder.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly object sync = new object();
        private static Dictionary<String, Entry> cache;
        private static String filePath;
        private static DateTime lastWriteTime;

        /// <summary>
        /// Optional identity override so different executables can share the
        /// same settings file (e.g. desktop GUI and web panel).
        /// </summary>
        public static String Identity { get; set; }

        /// <summary>
        /// Effective identity used for the data folder name. Falls back to the
        /// process friendly name when no override is set, so different executables
        /// (desktop GUI and web panel) can share the same data folder.
        /// </summary>
        public static String EffectiveIdentity
        {
            get { return String.IsNullOrEmpty(Identity) ? AppDomain.CurrentDomain.FriendlyName : Identity; }
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

        [DataContract]
        private class Entry
        {
            [DataMember]
            public int Type { get; set; }

            [DataMember]
            public String Value { get; set; }
        }

        [DataContract]
        private class Document
        {
            [DataMember]
            public List<Entry> Values { get; set; }

            [DataMember]
            public List<String> Keys { get; set; }
        }

        private static void EnsureLoaded()
        {
            if (cache != null)
                return;

            String appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            String name = EffectiveIdentity;
            String dir = Path.Combine(appData, "sb0t", name);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            filePath = Path.Combine(dir, "settings.json");
            cache = new Dictionary<String, Entry>();
            lastWriteTime = DateTime.MinValue;

            ReloadFile();
        }

        private static void ReloadFile()
        {
            if (cache == null)
                return;

            try
            {
                if (!File.Exists(filePath))
                    return;

                DateTime mtime = File.GetLastWriteTimeUtc(filePath);

                // Only reload when the file changed externally (another process
                // like the desktop GUI or web panel modified it). Avoids clobbering
                // in-memory changes made by this process.
                if (mtime != lastWriteTime)
                {
                    var fresh = new Dictionary<String, Entry>();

                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Document));
                        Document doc = (Document)serializer.ReadObject(stream);

                        if (doc != null && doc.Values != null && doc.Keys != null)
                        {
                            int count = Math.Min(doc.Values.Count, doc.Keys.Count);

                            for (int i = 0; i < count; i++)
                                if (doc.Keys[i] != null)
                                    fresh[doc.Keys[i]] = doc.Values[i];
                        }
                    }

                    cache = fresh;
                    lastWriteTime = mtime;
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                Document doc = new Document
                {
                    Keys = new List<String>(),
                    Values = new List<Entry>()
                };

                foreach (KeyValuePair<String, Entry> pair in cache)
                {
                    doc.Keys.Add(pair.Key);
                    doc.Values.Add(pair.Value);
                }

                using (FileStream stream = File.Create(filePath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Document));
                    serializer.WriteObject(stream, doc);
                }

                lastWriteTime = File.GetLastWriteTimeUtc(filePath);
            }
            catch { }
        }

        private static String Key(String name, String[] subkeys)
        {
            String result = name;

            foreach (String sub in subkeys)
                result = sub + "\\" + result;

            return result;
        }

        private static int IndexOf(Type type)
        {
            for (int i = 0; i < AcceptableTypes.Length; i++)
                if (AcceptableTypes[i].Equals(type))
                    return i;

            return -1;
        }

        private static Object ToValue(Entry entry, Type type)
        {
            int index = IndexOf(type);

            if (entry == null || index == -1)
            {
                if (index == 5)
                    return String.Empty;

                if (index == 6)
                    return default(byte[]);

                if (index == 7)
                    return false;

                return Convert.ChangeType(0, type);
            }

            switch (index)
            {
                case 5:
                    return entry.Value == null ? String.Empty : entry.Value;

                case 6:
                    return entry.Value == null ? default(byte[]) : Convert.FromBase64String(entry.Value);

                case 7:
                    return entry.Value == "1";

                default:
                    return Convert.ChangeType(entry.Value, type);
            }
        }

        private static Entry ToEntry(Object value)
        {
            int index = IndexOf(value.GetType());
            Entry entry = new Entry { Type = index, Value = null };

            switch (index)
            {
                case 5:
                    entry.Value = (String)value;
                    break;

                case 6:
                    entry.Value = Convert.ToBase64String((byte[])value);
                    break;

                case 7:
                    entry.Value = (bool)value ? "1" : "0";
                    break;

                default:
                    entry.Value = Convert.ToString(value);
                    break;
            }

            return entry;
        }

        public static T Get<T>(String name, params String[] subkeys)
        {
            lock (sync)
            {
                EnsureLoaded();
                ReloadIfChanged();

                String key = Key(name, subkeys);

                if (cache.TryGetValue(key, out Entry entry))
                    return (T)ToValue(entry, typeof(T));

                return (T)ToValue(null, typeof(T));
            }
        }

        private static void ReloadIfChanged()
        {
            // Called under the sync lock. Re-reads the file if another process
            // (desktop GUI or web panel) saved changes since we last loaded.
            if (cache == null)
                return;

            try
            {
                if (!File.Exists(filePath))
                    return;

                DateTime mtime = File.GetLastWriteTimeUtc(filePath);

                if (mtime != lastWriteTime)
                    ReloadFile();
            }
            catch { }
        }

        public static bool Set(String name, Object value, params String[] subkeys)
        {
            if (IndexOf(value.GetType()) == -1)
                return false;

            lock (sync)
            {
                EnsureLoaded();
                ReloadIfChanged();
                cache[Key(name, subkeys)] = ToEntry(value);
                Save();
            }

            return true;
        }

        public static void Delete(String name, params String[] subkeys)
        {
            lock (sync)
            {
                EnsureLoaded();
                ReloadIfChanged();
                cache.Remove(Key(name, subkeys));
                Save();
            }
        }

        public static String[] Keys(String subkey)
        {
            lock (sync)
            {
                EnsureLoaded();
                ReloadIfChanged();

                List<String> result = new List<String>();
                String prefix = subkey + "\\";

                foreach (String key in cache.Keys)
                    if (key.StartsWith(prefix))
                        result.Add(key.Substring(prefix.Length));

                return result.ToArray();
            }
        }
    }
}
