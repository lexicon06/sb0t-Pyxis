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

namespace core
{
    public class Avatars
    {
        private static byte[] server_avatar { get; set; }
        private static byte[] default_avatar { get; set; }

        public static void UpdateServerAvatar(byte[] data)
        {
            try
            {
                server_avatar = Scale(data);
            }
            catch { return; }

            if (UserPool.AUsers != null)
                UserPool.AUsers.ForEachWhere(x => x.SendPacket(TCPOutbound.BotAvatar(x, server_avatar)),
                    x => x.LoggedIn);
        }

        public static void UpdateDefaultAvatar(byte[] data)
        {
            try
            {
                default_avatar = Scale(data);
            }
            catch { return; }

            if (UserPool.AUsers != null)
                UserPool.AUsers.ForEachWhere(x => x.Avatar = default_avatar, x => x.DefaultAvatar);
        }

        internal static byte[] Server(AresClient client)
        {
            if (server_avatar == null)
                return TCPOutbound.BotAvatarCleared(client);
            else
                return TCPOutbound.BotAvatar(client, server_avatar);
        }

        internal static bool GotServerAvatar
        {
            get { return server_avatar != null; }
        }

        internal static byte[] Server(ib0t.ib0tClient client)
        {
            if (server_avatar == null)
                return ib0t.WebOutbound.AvatarClearTo(client, Settings.Get<String>("bot"));
            else
                return ib0t.WebOutbound.AvatarTo(client, Settings.Get<String>("bot"), server_avatar);
        }

        public static byte[] GetServerAvatar()
        {
            return server_avatar;
        }

        /// <summary>
        /// Loads the persisted server/default avatar files from disk into memory
        /// so avatars survive a server restart.
        /// </summary>
        public static void LoadFromDisk()
        {
            try
            {
                string serverFile = Path.Combine(DataPaths.Avatars, "server");
                if (File.Exists(serverFile))
                    server_avatar = Scale(File.ReadAllBytes(serverFile));
            }
            catch { }

            try
            {
                string defaultFile = Path.Combine(DataPaths.Avatars, "default");
                if (File.Exists(defaultFile))
                    default_avatar = Scale(File.ReadAllBytes(defaultFile));
            }
            catch { }
        }
        public static void CheckAvatars(ulong time)
        {
            if (default_avatar == null)
                return;

            UserPool.AUsers.ForEachWhere(x =>
            {
                x.Avatar = default_avatar;
                x.OrgAvatar = default_avatar;
                x.AvatarReceived = true;
                x.DefaultAvatar = true;
            },
            x => !x.AvatarReceived &&
                 x.LoggedIn &&
                 time > (x.AvatarTimeout + 10000));
        }

        private static byte[] Default
        {
            get
            {
                if (default_avatar == null)
                    return new byte[] { };
                else
                    return default_avatar;
            }
        }

        private static byte[] Scale(byte[] raw)
        {
            return ImageUtils.ScaleJpeg(raw, 48, 48);
        }
    }
}
