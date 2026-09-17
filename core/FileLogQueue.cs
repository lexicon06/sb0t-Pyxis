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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace core
{
    /// <summary>
    /// Background file writer for logs. Keeps all disk I/O off the server loop:
    /// callers just enqueue a line and a dedicated thread writes it.
    /// </summary>
    static class FileLogQueue
    {
        private struct Item
        {
            public String Path;
            public String Line;
        }

        private static readonly ConcurrentQueue<Item> queue = new ConcurrentQueue<Item>();
        private static readonly Dictionary<String, StreamWriter> writers = new Dictionary<String, StreamWriter>();
        private static readonly object _lock = new object();
        private static readonly AutoResetEvent signal = new AutoResetEvent(false);
        private static volatile bool running = false;
        private static Thread worker;

        public static void Enqueue(String path, String line)
        {
            if (String.IsNullOrEmpty(path) || line == null)
                return;

            queue.Enqueue(new Item { Path = path, Line = line });
            EnsureWorker();
            signal.Set();
        }

        private static void EnsureWorker()
        {
            if (running)
                return;

            lock (_lock)
            {
                if (running)
                    return;

                running = true;
                worker = new Thread(Loop);
                worker.IsBackground = true;
                worker.Name = "sb0t-log";
                worker.Start();
            }
        }

        private static void Loop()
        {
            while (running)
            {
                Item item;

                while (queue.TryDequeue(out item))
                {
                    try
                    {
                        StreamWriter writer;

                        if (!writers.TryGetValue(item.Path, out writer))
                        {
                            String dir = Path.GetDirectoryName(item.Path);

                            if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            writer = new StreamWriter(item.Path, true);
                            writers[item.Path] = writer;
                        }

                        writer.WriteLine(item.Line);
                    }
                    catch { }
                }

                foreach (StreamWriter writer in writers.Values)
                    try { writer.Flush(); } catch { }

                signal.WaitOne(1000);
            }

            foreach (StreamWriter writer in writers.Values)
                try { writer.Dispose(); } catch { }

            writers.Clear();
        }

        public static void Shutdown()
        {
            running = false;
            signal.Set();

            try
            {
                if (worker != null)
                    worker.Join(2000);
            }
            catch { }
        }
    }
}
