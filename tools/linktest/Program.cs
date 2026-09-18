using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
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
            String b64 = Convert.ToBase64String(payload);

            int total = (b64.Length + 15999) / 16000;
            StringBuilder sb = new StringBuilder();
            bool ok = true;

            for (int i = 0; i < total && ok; i++)
            {
                int off = i * 16000;
                int len = Math.Min(16000, b64.Length - off);
                String chunk = b64.Substring(off, len);

                LinkMsg msg;
                TCPPacketReader r = Unwrap(LeafOutbound.LeafAudio(link, User("u1"), "hubuser", (ushort)total, (ushort)i, chunk), out msg);

                uint ident = r;
                String name = r.ReadString(link);
                String sender = r.ReadString(link);
                ushort t = r;
                ushort idx = r;
                String got = r.ReadString(link);

                ok = msg == LinkMsg.MSG_LINK_LEAF_AUDIO && ident == 42 && name == "u1" &&
                     sender == "hubuser" && t == total && idx == i && got == chunk;
                sb.Append(got);
            }

            Check(ok && sb.ToString() == b64, "audio chunked link roundtrip (base64 16000)");
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

        private static byte[] AudioBytes(byte seed)
        {
            byte[] data = new byte[90000];
            new Random(seed).NextBytes(data);
            return data;
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

            Check(leafLog.Contains("link-recv: hubemoter emote-hub"), "leaf recibe EMOTE del hub");
            Check(hubLog.Contains("link-recv: leafemoter emote-leaf"), "hub recibe EMOTE del leaf");

            String hubLink = ReadLinkLog(hubDir);
            String leafLink = ReadLinkLog(leafDir);

            Console.WriteLine();
            Console.WriteLine("===== HUB link.log =====");
            Console.WriteLine(hubLink);
            Console.WriteLine("===== LEAF link.log =====");
            Console.WriteLine(leafLink);
            Console.WriteLine();

            Check(leafLink.Contains("recv-audio from hubaudio"), "leaf recibe AUDIO del hub");
            Check(leafLink.Contains("recv-avatar hubavatar"), "leaf recibe AVATAR del hub");
            Check(leafLink.Contains("recv-scribble hubscribble"), "leaf recibe SCRIBBLE del hub");
            Check(hubLink.Contains("recv-audio from leafaudio"), "hub recibe AUDIO del leaf");
            Check(hubLink.Contains("recv-avatar leafavatar"), "hub recibe AVATAR del leaf");
            Check(hubLink.Contains("recv-scribble leafscribble"), "hub recibe SCRIBBLE del leaf");

            String hubWebLog = ReadFileIfExists(Path.Combine(Path.GetTempPath(), "sb0t-linktest-hubweb.log"));
            String leafWebLog = ReadFileIfExists(Path.Combine(Path.GetTempPath(), "sb0t-linktest-leafweb.log"));

            Console.WriteLine();
            Console.WriteLine("===== HUB web client frames =====");
            Console.WriteLine(hubWebLog);
            Console.WriteLine("===== LEAF web client frames =====");
            Console.WriteLine(leafWebLog);
            Console.WriteLine();

            Check(leafWebLog.Contains("hubweb-msg"), "leaf WEB recibe texto de hub WEB (link)");
            Check(hubWebLog.Contains("leafweb-msg"), "hub WEB recibe texto de leaf WEB (link)");
            Check(leafWebLog.Contains("hola-desde-hub"), "leaf WEB recibe texto del HUB no-web (link)");
            Check(hubWebLog.Contains("hola-desde-leaf"), "hub WEB recibe texto del LEAF no-web (link)");

            Check(leafWebLog.Contains("AUDIO_HEAD:6,"), "leaf WEB recibe AUDIO de hub WEB (link)");
            Check(hubWebLog.Contains("AUDIO_HEAD:7,"), "hub WEB recibe AUDIO de leaf WEB (link)");

            Check(leafWebLog.Contains("AUDIO_HEAD:6,1:hubweb4"), "leaf WEB recibe AUDIO_HEAD count=4 (stream)");
            Check(CountOccurrences(leafWebLog, "AUDIO_BLOCK:") >= 4, "leaf WEB recibe 4+ AUDIO_BLOCK (chunks)");
            Check(hubWebLog.Contains("AUDIO_HEAD:7,1:leafweb4"), "hub WEB recibe AUDIO_HEAD count=4 (stream)");
            Check(CountOccurrences(hubWebLog, "AUDIO_BLOCK:") >= 4, "hub WEB recibe 4+ AUDIO_BLOCK (chunks)");

            Check(leafWebLog.Contains("AUDIO_HEAD:6,1:hubext3"), "leaf WEB recibe AUDIO de la extension del hub (stream en el destino)");
            Check(hubWebLog.Contains("AUDIO_HEAD:7,1:leafext3"), "hub WEB recibe AUDIO de la extension del leaf (stream en el destino)");

            Check(CountOccurrences(leafWebLog, "hubweb-msg") == 1, "leaf WEB: 1 sola copia del texto del hub (sin duplicar)");
            Check(CountOccurrences(hubWebLog, "leafweb-msg") == 1, "hub WEB: 1 sola copia del texto del leaf (sin duplicar)");

            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "E2E OK" : "E2E FAILED");
            return failed == 0 ? 0 : 1;
        }

        private static String ReadFileIfExists(String path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : "(no existe: " + path + ")";
            }
            catch (Exception e) { return "(error: " + e.Message + ")"; }
        }

        private static int CountOccurrences(String haystack, String needle)
        {
            int count = 0;
            int index = 0;

            while ((index = haystack.IndexOf(needle, index)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
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

        private static String ReadLinkLog(String identityDir)
        {
            try
            {
                String path = Path.Combine(identityDir, "logs", "link.log");
                return File.Exists(path) ? File.ReadAllText(path) : "(sin link.log)";
            }
            catch (Exception e)
            {
                return "(error leyendo link.log: " + e.Message + ")";
            }
        }

        private delegate byte[] ChunkEncoder(int total, int index, byte[] chunk);

        private static void SendChunks(LinkClient link, int size, int seed, ChunkEncoder enc)
        {
            byte[] data = new byte[size];
            new Random(seed).NextBytes(data);
            int total = (size + 39999) / 40000;

            for (int i = 0; i < total; i++)
            {
                int off = i * 40000;
                int len = Math.Min(40000, size - off);
                byte[] chunk = new byte[len];
                Buffer.BlockCopy(data, off, chunk, 0, len);
                link.SendPacket(enc(total, i, chunk));
            }
        }

        private static void SendMedia(LinkClient link, String tag)
        {
            uint ident = link.Leaves.Count > 0 ? link.Leaves[0].Ident : 0;
            core.LinkLeaf.LinkUser target = new core.LinkLeaf.LinkUser(ident);
            target.SetName("targetuser");

            byte[] audio = new byte[90000];
            new Random(7).NextBytes(audio);
            String ab64 = Convert.ToBase64String(audio);
            int atotal = (ab64.Length + 15999) / 16000;

            for (int i = 0; i < atotal; i++)
            {
                int off = i * 16000;
                int len = Math.Min(16000, ab64.Length - off);
                link.SendPacket(LeafOutbound.LeafAudio(link, target, tag + "audio", (ushort)atotal, (ushort)i, ab64.Substring(off, len)));
            }

            SendChunks(link, 70000, 11, (total, index, chunk) =>
                LeafOutbound.LeafAvatar(link, tag + "avatar", (ushort)total, (ushort)index, chunk));

            SendChunks(link, 110000, 13, (total, index, chunk) =>
                LeafOutbound.LeafScribbleUser(link, target, tag + "scribble", 10, (ushort)total, (ushort)index, chunk));

            Console.WriteLine("[" + tag + "] media sent: audio 90000, avatar 70000, scribble 110000");
        }

        // Mimics the audio library extension: it calls user.Audio(sender, base64) on every
        // user of the room, including the linked users. The receiving room must do the
        // streaming (AUDIO_HEAD/AUDIO_BLOCK) to its own users.
        private static void SendExtensionAudio(LinkClient link, String tag)
        {
            byte[] data = new byte[60000];
            new Random(tag == "hub" ? 0x61 : 0x62).NextBytes(data);
            String sender = tag + "ext";
            String b64 = Convert.ToBase64String(data);

            int users = 0;

            // Mimic the audio library extension: it calls user.Audio(sender, base64) on
            // every user of the room, including the linked users (Host.Users.Linked).
            foreach (core.LinkLeaf.Leaf leaf in link.Leaves)
                foreach (core.LinkLeaf.LinkUser u in leaf.Users)
                {
                    u.Audio(sender, b64);
                    users++;
                }

            Console.WriteLine("[" + tag + "] extension audio sent as '" + sender + "' (" + data.Length +
                " bytes) leaves=" + link.Leaves.Count + " linkedUsers=" + users);
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
            core.Settings.Set("enabled", true, "web");
            core.Settings.Set("audios", true, "settings");
            core.Settings.Set("scribbles", true, "settings");
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

            String hubWebLog = Path.Combine(Path.GetTempPath(), "sb0t-linktest-hubweb.log");
            try { File.Delete(hubWebLog); } catch { }
            WebTestClient hubWeb = new WebTestClient("127.0.0.1", port, hubWebLog);
            bool hubWebOk = hubWeb.Connect();
            Console.WriteLine("[hub] web client connected = " + hubWebOk);
            if (hubWebOk)
                hubWeb.Login("hubweb");

            Console.WriteLine("[hub] sending public text as hubuser (sin custom) y hubuser2 (con custom)");
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "hubuser", "", "hola-desde-hub"));
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "hubuser2", "@HUB@", "msg-custom-hub"));
            ServerCore.Linker.SendPacket(LeafOutbound.LeafEmoteText(ServerCore.Linker, "hubemoter", "emote-hub"));

            SendMedia(ServerCore.Linker, "hub");

            Thread.Sleep(3000);

            if (hubWebOk)
            {
                hubWeb.SendPublic("hubweb-msg " + new String('x', 260));
                hubWeb.SendAudio("hubweb", AudioBytes(0x41));
                Console.WriteLine("[hub] web public + audio sent");
            }

            SendExtensionAudio(ServerCore.Linker, "hub");

            Thread.Sleep(9000);
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

            String leafWebLog = Path.Combine(Path.GetTempPath(), "sb0t-linktest-leafweb.log");
            try { File.Delete(leafWebLog); } catch { }
            WebTestClient leafWeb = new WebTestClient("127.0.0.1", port, leafWebLog);
            bool leafWebOk = leafWeb.Connect();
            Console.WriteLine("[leaf] web client connected = " + leafWebOk);
            if (leafWebOk)
                leafWeb.Login("leafweb");

            Console.WriteLine("[leaf] sending public text as leafuser (sin custom) y leafuser2 (con custom)");
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "leafuser", "", "hola-desde-leaf"));
            ServerCore.Linker.SendPacket(LeafOutbound.LeafPublicText(ServerCore.Linker, "leafuser2", "@LEAF@", "msg-custom-leaf"));
            ServerCore.Linker.SendPacket(LeafOutbound.LeafEmoteText(ServerCore.Linker, "leafemoter", "emote-leaf"));

            SendMedia(ServerCore.Linker, "leaf");

            Thread.Sleep(3000);

            if (leafWebOk)
            {
                leafWeb.SendPublic("leafweb-msg " + new String('y', 260));
                leafWeb.SendAudio("leafweb", AudioBytes(0x42));
                Console.WriteLine("[leaf] web public + audio sent");
            }

            SendExtensionAudio(ServerCore.Linker, "leaf");

            Thread.Sleep(9000);
            Console.WriteLine("[leaf] done");
            Environment.Exit(0);
            return 0;
        }
    }

    sealed class WebTestClient
    {
        private readonly String host;
        private readonly ushort port;
        private readonly String logPath;
        private Socket sock;
        private volatile bool running;
        private readonly object sendLock = new object();
        private static readonly byte[] mask = new byte[] { 0x11, 0x22, 0x33, 0x44 };

        public WebTestClient(String host, ushort port, String logPath)
        {
            this.host = host;
            this.port = port;
            this.logPath = logPath;
        }

        public bool Connect()
        {
            try
            {
                this.sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                this.sock.Connect(new IPEndPoint(IPAddress.Parse(this.host), this.port));
                this.sock.Blocking = true;

                byte[] keyBytes = new byte[16];
                new Random().NextBytes(keyBytes);
                String key = Convert.ToBase64String(keyBytes);

                String req = "GET / HTTP/1.1\r\n" +
                             "Host: " + this.host + ":" + this.port + "\r\n" +
                             "Upgrade: websocket\r\n" +
                             "Connection: Upgrade\r\n" +
                             "Sec-WebSocket-Key: " + key + "\r\n" +
                             "Sec-WebSocket-Version: 13\r\n" +
                             "Origin: http://" + this.host + "\r\n" +
                             "\r\n";
                this.sock.Send(Encoding.ASCII.GetBytes(req));

                List<byte> reply = new List<byte>();
                byte[] tmp = new byte[4096];
                DateTime stop = DateTime.UtcNow.AddSeconds(5);
                bool done = false;

                while (!done && DateTime.UtcNow < stop)
                {
                    int n = this.sock.Receive(tmp);

                    if (n <= 0)
                        return false;

                    for (int i = 0; i < n; i++)
                        reply.Add(tmp[i]);

                    String text = Encoding.ASCII.GetString(reply.ToArray());

                    if (text.Contains("\r\n\r\n"))
                    {
                        if (!text.StartsWith("HTTP/1.1 101"))
                            return false;

                        done = true;
                    }
                }

                if (!done)
                    return false;

                this.running = true;
                Thread t = new Thread(this.ReadLoop);
                t.IsBackground = true;
                t.Start();
                return true;
            }
            catch { return false; }
        }

        public void Login(String name)
        {
            StringBuilder guid = new StringBuilder();
            Random r = new Random();

            for (int i = 0; i < 32; i++)
                guid.Append("0123456789abcdef"[r.Next(16)]);

            String pm = new String('p', 200);

            this.SendText("LOGIN:4,32," + name.Length + ",1,1,200,12" + ":" + "5000" + guid.ToString() + name + "1" + "1" + pm + "/default.png");
        }

        public void SendPublic(String text)
        {
            this.SendText("PUBLIC:" + text.Length + ":" + text);
        }

        public void SendAudio(String sender, byte[] data)
        {
            String b64 = Convert.ToBase64String(data);
            this.SendText("AUDIO:" + sender.Length + "," + b64.Length + ":" + sender + b64);
        }

        private void SendText(String payload)
        {
            if (this.sock == null)
                return;

            byte[] data = Encoding.UTF8.GetBytes(payload);
            int header;

            if (data.Length <= 125)
                header = 2;
            else if (data.Length < 65536)
                header = 4;
            else
                header = 10;

            byte[] frame = new byte[header + 4 + data.Length];
            frame[0] = 0x81;
            int p = 1;

            if (data.Length <= 125)
                frame[p++] = (byte)(0x80 | data.Length);
            else if (data.Length < 65536)
            {
                frame[p++] = (byte)(0x80 | 126);
                frame[p++] = (byte)(data.Length >> 8);
                frame[p++] = (byte)data.Length;
            }
            else
            {
                frame[p++] = (byte)(0x80 | 127);
                ulong l = (ulong)data.Length;

                for (int i = 7; i >= 0; i--)
                {
                    frame[p + i] = (byte)l;
                    l >>= 8;
                }

                p += 8;
            }

            Buffer.BlockCopy(mask, 0, frame, p, 4);
            p += 4;

            for (int i = 0; i < data.Length; i++)
                frame[p + i] = (byte)(data[i] ^ mask[i % 4]);

            try
            {
                lock (this.sendLock)
                    this.sock.Send(frame);
            }
            catch { }
        }

        private void SendPong(byte[] payload)
        {
            if (this.sock == null)
                return;

            byte[] data = payload;
            byte[] frame = new byte[2 + 4 + data.Length];
            frame[0] = 0x8A;
            frame[1] = (byte)(0x80 | (data.Length > 125 ? 125 : data.Length));
            Buffer.BlockCopy(mask, 0, frame, 2, 4);

            int len = data.Length > 125 ? 125 : data.Length;

            for (int i = 0; i < len; i++)
                frame[6 + i] = (byte)(data[i] ^ mask[i % 4]);

            try
            {
                lock (this.sendLock)
                    this.sock.Send(frame);
            }
            catch { }
        }

        private void ReadLoop()
        {
            List<byte> buf = new List<byte>();
            byte[] tmp = new byte[8192];
            StreamWriter w = null;

            try
            {
                w = new StreamWriter(this.logPath, false);
                w.AutoFlush = true;
            }
            catch { }

            while (this.running)
            {
                int n;

                try { n = this.sock.Receive(tmp); }
                catch { break; }

                if (n <= 0)
                    break;

                for (int i = 0; i < n; i++)
                    buf.Add(tmp[i]);

                while (true)
                {
                    if (buf.Count < 2)
                        break;

                    byte b0 = buf[0];
                    int op = b0 & 0x0f;
                    int len = buf[1] & 0x7f;
                    int offset = 2;
                    bool masked = (buf[1] & 0x80) != 0;

                    if (len == 126)
                    {
                        if (buf.Count < 4) break;
                        len = (buf[2] << 8) | buf[3];
                        offset = 4;
                    }
                    else if (len == 127)
                    {
                        if (buf.Count < 10) break;
                        ulong l = 0;

                        for (int i = 0; i < 8; i++)
                            l = (l << 8) | buf[2 + i];

                        len = (int)l;
                        offset = 10;
                    }

                    int maskLen = masked ? 4 : 0;

                    if (buf.Count < offset + maskLen + len)
                        break;

                    byte[] payload = new byte[len];

                    for (int i = 0; i < len; i++)
                        payload[i] = buf[offset + maskLen + i];

                    if (masked)
                    {
                        byte[] mk = new byte[4];

                        for (int i = 0; i < 4; i++)
                            mk[i] = buf[offset + i];

                        for (int i = 0; i < len; i++)
                            payload[i] ^= mk[i % 4];
                    }

                    buf.RemoveRange(0, offset + maskLen + len);

                    if (op == 0x1)
                    {
                        if (w != null)
                            w.WriteLine("FRAME: " + Encoding.UTF8.GetString(payload));
                    }
                    else if (op == 0x8)
                    {
                        this.running = false;
                        break;
                    }
                    else if (op == 0x9)
                    {
                        this.SendPong(payload);
                    }
                }
            }

            if (w != null)
                w.Dispose();
        }
    }
}
