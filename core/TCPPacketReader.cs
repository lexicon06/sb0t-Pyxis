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
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Security.Cryptography;

namespace core
{
    class TCPPacketReader
    {
        private static readonly String[] bad_chars = new String[] // skiddy
            {
                "\u00A0",
                "\u00AD",
                "\u009d",
                "\r\n",
                "\r",
                "\n",
                "\x00cc\x00b8",
                "͋"
            };

        // One pass instead of 10 Regex.Replace calls per string.
        private static readonly Regex bad_chars_regex = new Regex(
            String.Join("|", Array.ConvertAll(bad_chars, x => Regex.Escape(x))),
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private int Position = 0;
        private byte[] Data;

        public TCPPacketReader(byte[] bytes)
        {
            this.Data = bytes ?? new byte[0];
            this.Position = 0;
        }

        public int Remaining
        {
            get { return this.Data.Length - this.Position; }
        }

        public void SkipByte()
        {
            this.Position++;
        }

        public void SkipBytes(int count)
        {
            this.Position += count;
        }

        public static implicit operator byte(TCPPacketReader p)
        {
            byte tmp = p.Data[p.Position];
            p.Position++;
            return tmp;
        }

        public static implicit operator byte[](TCPPacketReader p)
        {
            int len = p.Data.Length - p.Position;

            if (len <= 0)
                return new byte[0];

            byte[] tmp = new byte[len];
            Buffer.BlockCopy(p.Data, p.Position, tmp, 0, len);
            p.Position += len;
            return tmp;
        }

        public static implicit operator Guid(TCPPacketReader p)
        {
            byte[] tmp = new byte[16];
            Buffer.BlockCopy(p.Data, p.Position, tmp, 0, 16);
            p.Position += 16;

            using (MD5 md5 = MD5.Create())
                tmp = md5.ComputeHash(tmp);

            return new Guid(tmp);
        }

        public static implicit operator ushort(TCPPacketReader p)
        {
            ushort tmp = BitConverter.ToUInt16(p.Data, p.Position);
            p.Position += 2;
            return tmp;
        }

        public static implicit operator uint(TCPPacketReader p)
        {
            uint tmp = BitConverter.ToUInt32(p.Data, p.Position);
            p.Position += 4;
            return tmp;
        }

        public static implicit operator ulong(TCPPacketReader p)
        {
            ulong tmp = BitConverter.ToUInt64(p.Data, p.Position);
            p.Position += 8;
            return tmp;
        }

        public static implicit operator IPAddress(TCPPacketReader p)
        {
            byte[] tmp = new byte[4];
            Buffer.BlockCopy(p.Data, p.Position, tmp, 0, 4);
            p.Position += 4;
            return new IPAddress(tmp);
        }

        public byte[] ReadBytes(int count)
        {
            byte[] tmp = new byte[count];
            Buffer.BlockCopy(this.Data, this.Position, tmp, 0, count);
            this.Position += count;
            return tmp;
        }

        private static String Sanitize(String str)
        {
            if (!String.IsNullOrEmpty(str))
                str = bad_chars_regex.Replace(str, String.Empty);

            return str.Trim();
        }

        public String ReadString(core.LinkHub.Leaf leaf)
        {
            String str = String.Empty;
            ushort size = this;
            byte[] data = this.ReadBytes(size);
            data = Crypto.Decrypt(data, leaf.Key, leaf.IV);
            str = Encoding.UTF8.GetString(data);

            if (this.Position < this.Data.Length)
                if (this.Data[this.Position] == 0)
                    this.Position++;

            return Sanitize(str);
        }

        public String ReadString(core.LinkLeaf.LinkClient client)
        {
            String str = String.Empty;
            ushort size = this;
            byte[] data = this.ReadBytes(size);
            data = Crypto.Decrypt(data, client.Key, client.IV);
            str = Encoding.UTF8.GetString(data);

            if (this.Position < this.Data.Length)
                if (this.Data[this.Position] == 0)
                    this.Position++;

            return Sanitize(str);
        }

        public String ReadString(IClient client)
        {
            String str = String.Empty;

            if (client.Encryption.Mode == EncryptionMode.Encrypted)
            {
                ushort size = this;
                byte[] data = this.ReadBytes(size);
                data = Crypto.Decrypt(data, client.Encryption.Key, client.Encryption.IV);
                str = Encoding.UTF8.GetString(data);

                if (this.Position < this.Data.Length)
                    if (this.Data[this.Position] == 0)
                        this.Position++;
            }
            else
            {
                int split = Array.IndexOf(this.Data, (byte)0, this.Position);
                int len = split > -1 ? (split - this.Position) : (this.Data.Length - this.Position);
                byte[] tmp = new byte[len];
                Buffer.BlockCopy(this.Data, this.Position, tmp, 0, len);
                this.Position = split > -1 ? (split + 1) : this.Data.Length;
                str = Encoding.UTF8.GetString(tmp);
            }

            return Sanitize(str);
        }

        public String ReadString()
        {
            String str = String.Empty;
            int split = Array.IndexOf(this.Data, (byte)0, this.Position);
            int len = split > -1 ? (split - this.Position) : (this.Data.Length - this.Position);
            byte[] tmp = new byte[len];
            Buffer.BlockCopy(this.Data, this.Position, tmp, 0, len);
            this.Position = split > -1 ? (split + 1) : this.Data.Length;
            str = Encoding.UTF8.GetString(tmp);

            return Sanitize(str);
        }

        public byte[] ToArray()
        {
            return this.Data;
        }
    }
}
