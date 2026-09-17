using System;
using core;
using core.LinkLeaf;
using core.LinkHub;

namespace linktest
{
    static class Program
    {
        private static int failed = 0;

        private static void Check(bool ok, String name)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);

            if (!ok)
                failed++;
        }

        static int Main()
        {
            CryptoRoundtrip();
            ReaderWriterRoundtrip();

            LinkClient link = new LinkClient();
            link.Key = Crypto.CreateKey;
            link.IV = Crypto.CreateIV;
            link.HubName = "test";

            AudioChunking(link);
            AvatarChunking(link);
            ScribbleChunking(link);

            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "ALL OK" : (failed + " FAILED"));
            return failed == 0 ? 0 : 1;
        }

        private static void CryptoRoundtrip()
        {
            byte[] key = Crypto.CreateKey;
            byte[] iv = Crypto.CreateIV;
            int[] sizes = { 0, 1, 15, 16, 17, 100, 4064, 30000, 100000 };
            bool ok = true;

            foreach (int size in sizes)
            {
                byte[] data = new byte[size];
                new Random(size + 1).NextBytes(data);
                byte[] enc = Crypto.Encrypt(data, key, iv);
                byte[] dec = Crypto.Decrypt(enc, key, iv);

                if (dec.Length != data.Length)
                    ok = false;
                else
                    for (int i = 0; i < data.Length; i++)
                        if (dec[i] != data[i])
                        {
                            ok = false;
                            break;
                        }
            }

            Check(ok, "crypto roundtrip (0..100000, incluye 30000 read-full)");
        }

        private static void ReaderWriterRoundtrip()
        {
            LinkClient link = new LinkClient();
            link.Key = Crypto.CreateKey;
            link.IV = Crypto.CreateIV;

            TCPPacketWriter w = new TCPPacketWriter();
            w.WriteUInt32(0xDEADBEEF);
            w.WriteUInt16(0xBEEF);
            w.WriteByte(0x42);
            w.WriteString(link, "usuario");
            w.WriteString(link, "texto con acentos: ñáéíóú");
            w.WriteBytes(new byte[] { 1, 2, 3, 4, 5 });

            TCPPacketReader r = new TCPPacketReader(w.ToByteArray());

            uint u = r;
            ushort s = r;
            byte b = r;
            String n = r.ReadString(link);
            String t = r.ReadString(link);
            byte[] tail = r;

            bool ok = u == 0xDEADBEEF && s == 0xBEEF && b == 0x42 &&
                      n == "usuario" && t == "texto con acentos: ñáéíóú" &&
                      tail.Length == 5 && tail[0] == 1 && tail[4] == 5;

            Check(ok, "reader/writer roundtrip (strings cifrados + unicode + bytes)");
        }

        private static core.LinkLeaf.LinkUser User(String name)
        {
            core.LinkLeaf.LinkUser u = new core.LinkLeaf.LinkUser(42);
            u.SetName(name);
            return u;
        }

        // Strips [2-byte ares len][MSG_LINK_PROTO][2-byte link len][link msg] and returns the payload reader + msg.
        private static TCPPacketReader Unwrap(byte[] ares, out LinkMsg msg)
        {
            msg = (LinkMsg)ares[5];
            byte[] body = new byte[ares.Length - 6];
            Buffer.BlockCopy(ares, 6, body, 0, body.Length);
            return new TCPPacketReader(body);
        }

        private static byte[] Concat(byte[][] parts)
        {
            int size = 0;
            foreach (byte[] p in parts) size += p.Length;
            byte[] result = new byte[size];
            int offset = 0;
            foreach (byte[] p in parts)
            {
                Buffer.BlockCopy(p, 0, result, offset, p.Length);
                offset += p.Length;
            }
            return result;
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;

            return true;
        }

        private static void AudioChunking(LinkClient link)
        {
            byte[] payload = new byte[100000];
            new Random(7).NextBytes(payload);

            int total = (payload.Length + 39999) / 40000;
            byte[][] parts = new byte[total][];
            bool ok = true;

            for (int i = 0; i < total && ok; i++)
            {
                int off = i * 40000;
                int len = Math.Min(40000, payload.Length - off);
                byte[] chunk = new byte[len];
                Buffer.BlockCopy(payload, off, chunk, 0, len);

                LinkMsg msg;
                TCPPacketReader r = Unwrap(LeafOutbound.LeafAudio(link, User("u1"), "hubuser", (ushort)total, (ushort)i, chunk), out msg);

                uint ident = r;
                String name = r.ReadString(link);
                String sender = r.ReadString(link);
                ushort t = r;
                ushort idx = r;
                byte[] got = r;

                ok = msg == LinkMsg.MSG_LINK_LEAF_AUDIO && ident == 42 && name == "u1" &&
                     sender == "hubuser" && t == total && idx == i && Equal(got, chunk);
                parts[i] = got;
            }

            Check(ok && Equal(Concat(parts), payload), "audio chunked link roundtrip (100000, 3 chunks)");
        }

        private static void AvatarChunking(LinkClient link)
        {
            byte[] payload = new byte[65000];
            new Random(11).NextBytes(payload);

            int total = (payload.Length + 39999) / 40000;
            byte[][] parts = new byte[total][];
            bool ok = true;

            for (int i = 0; i < total && ok; i++)
            {
                int off = i * 40000;
                int len = Math.Min(40000, payload.Length - off);
                byte[] chunk = new byte[len];
                Buffer.BlockCopy(payload, off, chunk, 0, len);

                LinkMsg msg;
                TCPPacketReader r = Unwrap(LeafOutbound.LeafAvatar(link, "u1", (ushort)total, (ushort)i, chunk), out msg);

                String name = r.ReadString(link);
                ushort t = r;
                ushort idx = r;
                byte[] got = r;

                ok = msg == LinkMsg.MSG_LINK_LEAF_AVATAR && name == "u1" && t == total && idx == i && Equal(got, chunk);
                parts[i] = got;
            }

            Check(ok && Equal(Concat(parts), payload), "avatar chunked link roundtrip (65000, 2 chunks)");
        }

        private static void ScribbleChunking(LinkClient link)
        {
            byte[] payload = new byte[120000];
            new Random(13).NextBytes(payload);

            int total = (payload.Length + 39999) / 40000;
            byte[][] parts = new byte[total][];
            bool ok = true;

            for (int i = 0; i < total && ok; i++)
            {
                int off = i * 40000;
                int len = Math.Min(40000, payload.Length - off);
                byte[] chunk = new byte[len];
                Buffer.BlockCopy(payload, off, chunk, 0, len);

                LinkMsg msg;
                TCPPacketReader r = Unwrap(LeafOutbound.LeafScribbleUser(link, User("u1"), "hubuser", 10, (ushort)total, (ushort)i, chunk), out msg);

                uint ident = r;
                String name = r.ReadString(link);
                String sender = r.ReadString(link);
                uint height = r;
                ushort t = r;
                ushort idx = r;
                byte[] got = r;

                ok = msg == LinkMsg.MSG_LINK_LEAF_SCRIBBLE_USER && ident == 42 && name == "u1" &&
                     sender == "hubuser" && height == 10 && t == total && idx == i && Equal(got, chunk);
                parts[i] = got;
            }

            Check(ok && Equal(Concat(parts), payload), "scribble chunked link roundtrip (120000, 3 chunks)");
        }
    }
}
