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
using System.IO;
using System.Text;

namespace core
{
    /// <summary>
    /// Central file logger. Splits the server output into separate files so a
    /// busy log full of normal activity doesn't hide real problems:
    ///   - serverlog.txt      -> general activity
    ///   - server_error.log   -> exceptions / errors (with stack trace)
    ///   - logs\&lt;channel&gt;.log -> per-area logs (link, udp, tcp, web, ...)
    /// </summary>
    public static class ServerLog
    {
        private static readonly object _lock = new object();

        public static String InfoPath
        {
            get { return Path.Combine(DataPaths.Root, "serverlog.txt"); }
        }

        public static String ErrorPath
        {
            get { return Path.Combine(DataPaths.Root, "server_error.log"); }
        }

        public static String ChannelPath(String channel)
        {
            return Path.Combine(DataPaths.Root, "logs", channel + ".log");
        }

        /// <summary>General activity.</summary>
        public static void Info(String message)
        {
            if (String.IsNullOrEmpty(message))
                return;

            Write(InfoPath, message);
        }

        /// <summary>Error: goes to server_error.log with full detail.</summary>
        public static void Error(String message, Exception e)
        {
            String text = Format(message, e);

            if (String.IsNullOrEmpty(text))
                return;

            Write(ErrorPath, text);
        }

        /// <summary>Activity for a specific area (link, udp, tcp, web, ...).</summary>
        public static void Channel(String channel, String message)
        {
            if (String.IsNullOrEmpty(channel) || String.IsNullOrEmpty(message))
                return;

            Write(ChannelPath(channel), message);
        }

        /// <summary>
        /// Error for a specific area: written to both the area log and
        /// server_error.log.
        /// </summary>
        public static void ChannelError(String channel, String message, Exception e)
        {
            String text = Format(message, e);

            if (String.IsNullOrEmpty(text))
                return;

            if (!String.IsNullOrEmpty(channel))
                Write(ChannelPath(channel), text);

            Write(ErrorPath, String.IsNullOrEmpty(channel) ? text : channel + ": " + text);
        }

        private static String Format(String message, Exception e)
        {
            StringBuilder sb = new StringBuilder();

            if (!String.IsNullOrEmpty(message))
                sb.Append(message);

            if (e != null)
            {
                if (sb.Length > 0)
                    sb.Append("\r\n");

                sb.Append(e.GetType().FullName).Append(": ").Append(e.Message);
                sb.Append("\r\n").Append(e.StackTrace);

                Exception inner = e.InnerException;

                while (inner != null)
                {
                    sb.Append("\r\n--- inner: ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
                    sb.Append("\r\n").Append(inner.StackTrace);
                    inner = inner.InnerException;
                }
            }

            return sb.ToString();
        }

        private static void Write(String path, String text)
        {
            try
            {
                FileLogQueue.Enqueue(path, DateTime.UtcNow + " " + text);
            }
            catch { }
        }
    }
}
