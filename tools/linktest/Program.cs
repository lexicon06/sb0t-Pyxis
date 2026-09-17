using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using core;
using core.LinkHub;
using core.LinkLeaf;

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

        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "hub": return HubMain(args);
                    case "leaf": return LeafMain(args);
                    case "e2e": return E2eMain();
                }
            }

            return UnitTests();
        }

        // ------------------------------------------------------------------
        //  Unit tests (protocol round-trips)
        // ------------------------------------------------------------------

        private static int UnitTests()
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

        // ------------------------------------------------------------------
        //  End-to-end: two ServerCore processes linked, both send + read
        // ------------------------------------------------------------------

        private static String HexStr(byte[] b)
        {
            return BitConverter.ToString(b).Replace("-", "");
        }

        private static byte[] HexBytes(String s)
        {
            byte[] r = new byte[s.Length / 2];
            for (int i = 0; i < r.Length; i++)
                r[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return r;
        }

        private static int E2eMain()
        {
            ushort hubPort = 55111;
            ushort leafPort = 55112;
            String hubName = "HUBTEST";
            String leafName = "LEAFTEST";
            byte[] hubGuid = Guid.NewGuid().ToByteArray();
            byte[] leafGuid = Guid.NewGuid().ToByteArray();

            String dll = Assembly.GetEntryAssembly().Location;

            String hubDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sb0t", "sb0t-linktest-hub");
            String leafDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sb0t", "sb0t-linktest-leaf");

            TryDeleteDir(hubDir);
            TryDeleteDir(leafDir);

            String readyFile = Path.Combine(Path.GetTempPath(), "sb0t-linktest-hub-ready");
            try { File.Delete(readyFile); } catch { }

            Console.WriteLine("[e2e] launching hub on " + hubPort + " ...");
            Process hub = StartChild(dll, "hub " + hubPort + " " + hubName + " " + HexStr(hubGuid) + " " + leafName + " " + HexStr(leafGuid));

            bool ready = WaitFile(readyFile, 30000);
            Console.WriteLine("[e2e] hub ready = " + ready);

            Console.WriteLine("[e2e] launching leaf on " + leafPort + " ...");
            Process leaf = StartChild(dll, "leaf " + leafPort + " " + leafName + " " + HexStr(leafGuid) + " 127.0.0.1 " + hubPort + " " + hubName);

            hub.WaitForExit(60000);
            leaf.WaitForExit(60000);

            if (!hub.HasExited) { try { hub.Kill(); } catch { } }
            if (!leaf.HasExited) { try { leaf.Kill(); } catch { } }

            Thread.Sleep(1000);

            String hubLog = ReadTodayLog(hubDir);
            String leafLog = ReadTodayLog(leafDir);

            Console.WriteLine();
            Console.WriteLine("===== HUB chatlog =====");
            Console.WriteLine(hubLog);
            Console.WriteLine("===== LEAF chatlog =====");
            Console.WriteLine(leafLog);
            Console.WriteLine();

            bool leafGotHub = leafLog.Contains("link-recv: hubuser> hola-desde-hub");
            bool hubGotLeaf = hubLog.Contains("link-recv: leafuser> hola-desde-leaf");
            bool leafGotHubCustom = leafLog.Contains("link-recv: @HUB@msg-custom-hub");
            bool hubGotLeafCustom = hubLog.Contains("link-recv: @LEAF@msg-custom-leaf");

            Check(leafGotHub, "leaf lee hub SIN customname (link-recv: hubuser> hola-desde-hub)");
            Check(hubGotLeaf, "hub lee leaf SIN customname (link-recv: leafuser> hola-desde-leaf)");
            Check(leafGotHubCustom, "leaf lee hub CON customname (link-recv: @HUB@msg-custom-hub)");
            Check(hubGotLeafCustom, "hub lee leaf CON customname (link-recv: @LEAF@msg-custom-leaf)");

            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "E2E OK" : "E2E FAILED");
            return failed == 0 ? 0 : 1;
        }

        private static Process StartChild(String dll, String args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "dotnet";
            psi.Arguments = "\"" + dll + "\" " + args;
            psi.UseShellExecute = false;
            return Process.Start(psi);
        }

        private static bool WaitFile(String path, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (File.Exists(path))
                    return true;

                Thread.Sleep(100);
            }

            return false;
        }

        private static void TryDeleteDir(String dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }

        private static String ReadTodayLog(String identityDir)
        {
            try
            {
                DateTime d = DateTime.Now;
                String path = Path.Combine(identityDir, "ChatLogs", d.Day + " " + d.Month + " " + d.Year + ".txt");
                return File.Exists(path) ? File.ReadAllText(path) : "(sin chatlog)";
            }
            catch (Exception e)
            {
                return "(error leyendo chatlog: " + e.Message + ")";
            }
        }

        private static void ConfigureIdentity(String identity, ushort port, String name, byte[] guid, int linkMode)
        {
            iconnect.SettingsStore.Identity = identity;
            core.Settings.Set("port", port);
            core.Settings.Set("name", name);
            core.Settings.Set("guid", guid);
            core.Settings.Set("link_mode", linkMode);
            core.Settings.Set("logging", true);
            core.Settings.Set("captcha", false);
            core.Settings.Set("roomsearch", false);
            core.Settings.Set("enabled", false, "web");
            core.Settings.Set("link_reconnect", false);
            core.Settings.Set("link_admin", false);
        }

        private static int HubMain(String[] a)
        {
            // hub <port> <name> <guidHex> <leafName> <leafGuidHex>
            ushort port = ushort.Parse(a[1]);
            String name = a[2];
            byte[] guid = HexBytes(a[3]);
            String leafName = a[4];
            byte[] leafGuid = HexBytes(a[5]);

            ConfigureIdentity("sb0t-linktest-hub", port, name, guid, 1);
            ServerCore.LogUpdate += (s, e) => { };

            TrustedLeavesManager.Init();
            TrustedLeavesManager.AddItem(new TrustedLeafItem { Name = leafName, Guid = new Guid(leafGuid) });

            ServerCore server = new ServerCore();

            if (!server.Open())
            {
                Console.WriteLine("[hub] Open() failed");
                return 2;
            }

            Console.WriteLine("[hub] listening on " + port + ", waiting for leaf " + leafName);

            File.WriteAllText(Path.Combine(Path.GetTempPath(), "sb0t-linktest-hub-ready"), "1");

            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 30000 && LeafPool.Leaves.Count < 2)
                Thread.Sleep(100);

            Console.WriteLine("[hub] leaves connected = " + LeafPool.Leaves.Count);

            while (sw.ElapsedMilliseconds < 45000 && !(ServerCore.Linker != null && ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == core.LinkLeaf.LinkLogin.Ready))
                Thread.Sleep(100);

            Thread.Sleep(2500);

            Console.WriteLine("[hub] sending public text as hubuser (sin custom) y hubuser2 (con custom)");
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "hubuser", "", "hola-desde-hub"));
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "hubuser2", "@HUB@", "msg-custom-hub"));

            Thread.Sleep(6000);
            Console.WriteLine("[hub] done");
            Environment.Exit(0);
            return 0;
        }

        private static int LeafMain(String[] a)
        {
            // leaf <port> <name> <guidHex> <hubHost> <hubPort> <hubName>
            ushort port = ushort.Parse(a[1]);
            String name = a[2];
            byte[] guid = HexBytes(a[3]);
            String hubHost = a[4];
            ushort hubPort = ushort.Parse(a[5]);
            String hubName = a[6];

            ConfigureIdentity("sb0t-linktest-leaf", port, name, guid, 2);
            ServerCore.LogUpdate += (s, e) => { };

            ServerCore server = new ServerCore();

            if (!server.Open())
            {
                Console.WriteLine("[leaf] Open() failed");
                return 2;
            }

            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 15000 && ServerCore.Linker == null)
                Thread.Sleep(50);

            Room room = new Room { IP = IPAddress.Parse(hubHost), Port = hubPort, Name = hubName };
            String hashlink = Hashlink.EncodeHashlink(room);
            Console.WriteLine("[leaf] connecting to hub '" + hubName + "' at " + hubHost + ":" + hubPort);
            ServerCore.Linker.Connect(hashlink);

            while (sw.ElapsedMilliseconds < 45000 && !(ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == core.LinkLeaf.LinkLogin.Ready))
                Thread.Sleep(100);

            Console.WriteLine("[leaf] link phase = " + ServerCore.Linker.LoginPhase);

            Thread.Sleep(2500);

            Console.WriteLine("[leaf] sending public text as leafuser (sin custom) y leafuser2 (con custom)");
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "leafuser", "", "hola-desde-leaf"));
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "leafuser2", "@LEAF@", "msg-custom-leaf"));

            Thread.Sleep(6000);
            Console.WriteLine("[leaf] done");
            Environment.Exit(0);
            return 0;
        }
    }
}
