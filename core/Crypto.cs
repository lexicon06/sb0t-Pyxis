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
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace core
{
    class Crypto
    {
        private sealed class CachedAes
        {
            public byte[] Iv;
            public Aes Aes;
        }

        private static readonly ConditionalWeakTable<byte[], CachedAes> aes_cache = new ConditionalWeakTable<byte[], CachedAes>();
        private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        private static Aes GetAes(byte[] key, byte[] iv)
        {
            CachedAes cached;

            if (aes_cache.TryGetValue(key, out cached) && ReferenceEquals(cached.Iv, iv))
                return cached.Aes;

            Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            cached = new CachedAes { Iv = iv, Aes = aes };
            aes_cache.Remove(key);
            aes_cache.Add(key, cached);

            return aes;
        }

        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            byte[] result;

            using (MemoryStream ms = new MemoryStream())
            using (ICryptoTransform enc = GetAes(key, iv).CreateEncryptor())
            using (CryptoStream cs = new CryptoStream(ms, enc, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                result = ms.ToArray();
            }

            return result;
        }

        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            byte[] result;

            using (MemoryStream ms = new MemoryStream(data))
            using (ICryptoTransform enc = GetAes(key, iv).CreateDecryptor())
            using (CryptoStream cs = new CryptoStream(ms, enc, CryptoStreamMode.Read))
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int size;

                while ((size = cs.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, size);

                result = output.ToArray();
            }

            return result;
        }

        public static byte[] CreateKey
        {
            get
            {
                byte[] result = new byte[32];
                rng.GetBytes(result);
                return result;
            }
        }

        public static byte[] CreateIV
        {
            get
            {
                byte[] result = new byte[16];
                rng.GetBytes(result);
                return result;
            }
        }

        public static byte[] d67(byte[] data, ushort b)
        {
            byte[] buffer = new byte[data.Length];
            Array.Copy(data, buffer, data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                buffer[i] = (byte)(data[i] ^ b >> 8 & 255);
                b = (ushort)((b + data[i]) * 23219 + 36126 & 65535);
            }

            return buffer;
        }

        public static byte[] e67(byte[] data, ushort b)
        {
            byte[] buffer = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                buffer[i] = (byte)((data[i] ^ (b >> 8)) & 255);
                b = (ushort)(((buffer[i] + b) * 23219 + 36126) & 65535);
            }

            return buffer;
        }
    }

    enum EncryptionMode
    {
        Encrypted,
        Unencrypted
    }

    class Encryption
    {
        public EncryptionMode Mode { get; set; }
        public byte[] Key { get; set; }
        public byte[] IV { get; set; }
    }
}
