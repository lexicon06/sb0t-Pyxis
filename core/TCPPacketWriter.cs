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
using System.Linq;
using System.Text;
using System.Net;

namespace core
{
    class TCPPacketWriter
    {
        private byte[] Data = new byte[256];
        private int Count = 0;

        public int GetByteCount()
        {
            return this.Count;
        }

        private void EnsureCapacity(int extra)
        {
            int needed = this.Count + extra;

            if (needed <= this.Data.Length)
                return;

            int size = this.Data.Length;

            while (size < needed)
                size *= 2;

            byte[] bigger = new byte[size];
            Buffer.BlockCopy(this.Data, 0, bigger, 0, this.Count);
            this.Data = bigger;
        }

        public void WriteByte(byte b)
        {
            this.EnsureCapacity(1);
            this.Data[this.Count++] = b;
        }

        public void WriteBytes(byte[] b)
        {
            if (b == null || b.Length == 0)
                return;

            this.EnsureCapacity(b.Length);
            Buffer.BlockCopy(b, 0, this.Data, this.Count, b.Length);
            this.Count += b.Length;
        }

        public void WriteGuid(Guid g)
        {
            this.WriteBytes(g.ToByteArray());
        }

        public void WriteUInt16(ushort i)
        {
            this.EnsureCapacity(2);
            this.Data[this.Count] = (byte)i;
            this.Data[this.Count + 1] = (byte)(i >> 8);
            this.Count += 2;
        }

        public void WriteUInt32(uint i)
        {
            this.EnsureCapacity(4);
            this.Data[this.Count] = (byte)i;
            this.Data[this.Count + 1] = (byte)(i >> 8);
            this.Data[this.Count + 2] = (byte)(i >> 16);
            this.Data[this.Count + 3] = (byte)(i >> 24);
            this.Count += 4;
        }

        public void WriteUInt64(ulong i)
        {
            this.EnsureCapacity(8);

            for (int b = 0; b < 8; b++)
                this.Data[this.Count + b] = (byte)(i >> (8 * b));

            this.Count += 8;
        }

        public void WriteIP(String ip_string)
        {
            this.WriteBytes(IPAddress.Parse(ip_string).GetAddressBytes());
        }

        public void WriteIP(long ip_numeric)
        {
            byte[] temp = BitConverter.GetBytes((uint)ip_numeric);
            Array.Reverse(temp);
            this.WriteBytes(new IPAddress(temp).GetAddressBytes());
        }

        public void WriteIP(byte[] ip_bytes)
        {
            if (ip_bytes.Length != 4) return;
            this.WriteBytes(new IPAddress(ip_bytes).GetAddressBytes());
        }

        public void WriteIP(IPAddress ip_object)
        {
            this.WriteBytes(ip_object.GetAddressBytes());
        }

        public void WriteString(String text, bool nulled, bool unenc)
        {
            this.WriteBytes(Encoding.UTF8.GetBytes(text));

            if (nulled)
                this.WriteByte(0);
        }

        public void WriteString(IClient client, String text)
        {
            this.WriteString(client, text, true);
        }

        public void WriteString(IClient client, String text, bool null_terminated)
        {
            if (client.Encryption.Mode == EncryptionMode.Encrypted)
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                data = Crypto.Encrypt(data, client.Encryption.Key, client.Encryption.IV);
                this.WriteUInt16((ushort)data.Length);
                this.WriteBytes(data);
            }
            else this.WriteBytes(Encoding.UTF8.GetBytes(text));

            if (null_terminated)
                this.WriteByte(0);
        }

        public void WriteString(core.LinkHub.Leaf leaf, String text)
        {
            this.WriteString(leaf, text, true);
        }

        public void WriteString(core.LinkHub.Leaf leaf, String text, bool null_terminated)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            data = Crypto.Encrypt(data, leaf.Key, leaf.IV);
            this.WriteUInt16((ushort)data.Length);
            this.WriteBytes(data);

            if (null_terminated)
                this.WriteByte(0);
        }

        public void WriteString(core.LinkLeaf.LinkClient client, String text)
        {
            this.WriteString(client, text, true);
        }

        public void WriteString(core.LinkLeaf.LinkClient client, String text, bool null_terminated)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            data = Crypto.Encrypt(data, client.Key, client.IV);
            this.WriteUInt16((ushort)data.Length);
            this.WriteBytes(data);

            if (null_terminated)
                this.WriteByte(0);
        }

        public void ReplaceByte(byte b, int i)
        {
            this.Data[i] = b;
        }

        public byte[] ToByteArray()
        {
            byte[] result = new byte[this.Count];
            Buffer.BlockCopy(this.Data, 0, result, 0, this.Count);
            return result;
        }

        public byte[] ToAresPacket(TCPMsg packet_id)
        {
            byte[] result = new byte[this.Count + 3];
            result[0] = (byte)this.Count;
            result[1] = (byte)(this.Count >> 8);
            result[2] = (byte)packet_id;
            Buffer.BlockCopy(this.Data, 0, result, 3, this.Count);
            return result;
        }

        public byte[] ToLinkPacket(core.LinkHub.LinkMsg msg)
        {
            byte[] result = new byte[this.Count + 3];
            result[0] = (byte)this.Count;
            result[1] = (byte)(this.Count >> 8);
            result[2] = (byte)msg;
            Buffer.BlockCopy(this.Data, 0, result, 3, this.Count);
            return result;
        }
    }
}
