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
using System.Text;

namespace core
{
    /// <summary>
    /// Growable byte buffer with a read offset. Consuming from the front is
    /// O(1) (it just advances the offset) instead of shifting the whole list.
    /// </summary>
    class ByteBuffer
    {
        private byte[] data = new byte[256];
        private int start = 0;
        private int end = 0;

        public int Count
        {
            get { return this.end - this.start; }
        }

        public byte this[int i]
        {
            get { return this.data[this.start + i]; }
        }

        private void EnsureWrite(int extra)
        {
            if (this.end + extra <= this.data.Length)
                return;

            if (this.start > 0)
            {
                int len = this.end - this.start;
                Buffer.BlockCopy(this.data, this.start, this.data, 0, len);
                this.start = 0;
                this.end = len;

                if (this.end + extra <= this.data.Length)
                    return;
            }

            int size = this.data.Length;

            while (size < this.end + extra)
                size *= 2;

            byte[] bigger = new byte[size];
            Buffer.BlockCopy(this.data, 0, bigger, 0, this.end);
            this.data = bigger;
        }

        public void Add(byte b)
        {
            this.EnsureWrite(1);
            this.data[this.end++] = b;
        }

        public void AddRange(byte[] src)
        {
            if (src == null || src.Length == 0)
                return;

            this.EnsureWrite(src.Length);
            Buffer.BlockCopy(src, 0, this.data, this.end, src.Length);
            this.end += src.Length;
        }

        public void AddRange(ArraySegment<byte> seg)
        {
            if (seg.Array == null || seg.Count <= 0)
                return;

            this.EnsureWrite(seg.Count);
            Buffer.BlockCopy(seg.Array, seg.Offset, this.data, this.end, seg.Count);
            this.end += seg.Count;
        }

        public int IndexOf(byte value)
        {
            for (int i = this.start; i < this.end; i++)
                if (this.data[i] == value)
                    return i - this.start;

            return -1;
        }

        public void CopyTo(int index, byte[] dst, int dstOffset, int count)
        {
            if (count <= 0)
                return;

            Buffer.BlockCopy(this.data, this.start + index, dst, dstOffset, count);
        }

        public byte[] GetRange(int index, int count)
        {
            byte[] result = new byte[count];

            if (count > 0)
                Buffer.BlockCopy(this.data, this.start + index, result, 0, count);

            return result;
        }

        public void RemoveRange(int index, int count)
        {
            if (count <= 0)
                return;

            if (index == 0)
            {
                this.start += count;

                if (this.start >= this.end)
                    this.start = this.end = 0;

                return;
            }

            int len = this.end - (this.start + index + count);

            if (len > 0)
                Buffer.BlockCopy(this.data, this.start + index + count, this.data, this.start + index, len);

            this.end -= count;
        }

        public void InsertRange(int index, byte[] src)
        {
            if (src == null || src.Length == 0)
                return;

            if (index == 0)
            {
                byte[] current = this.ToArray();
                this.start = 0;
                this.end = 0;
                this.EnsureWrite(src.Length + current.Length);
                Buffer.BlockCopy(src, 0, this.data, 0, src.Length);
                Buffer.BlockCopy(current, 0, this.data, src.Length, current.Length);
                this.end = src.Length + current.Length;
            }
            else
            {
                throw new NotSupportedException("ByteBuffer.InsertRange only supports index 0");
            }
        }

        public void Clear()
        {
            this.start = 0;
            this.end = 0;
        }

        public byte[] ToArray()
        {
            return this.GetRange(0, this.Count);
        }

        private int NextEndLine()
        {
            for (int i = this.start; i < this.end - 1; i++)
                if (this.data[i] == 13 && this.data[i + 1] == 10)
                    return i - this.start;

            return -1;
        }

        public bool CanTakeLine()
        {
            return this.NextEndLine() > -1;
        }

        public String TakeLine()
        {
            int index = this.NextEndLine();

            if (index > -1)
            {
                String str = Encoding.Default.GetString(this.data, this.start, index);
                this.RemoveRange(0, index + 2);
                return str;
            }

            return String.Empty;
        }
    }
}
