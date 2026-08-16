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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using captcha;
using core.Extensions;
using core.Udp;
using iconnect;
using Newtonsoft.Json;
using System.Net.Security;

namespace core
{
    public class ServerCore
    {
        public static event EventHandler<ServerLogEventArgs> LogUpdate;
        internal static core.LinkLeaf.LinkClient Linker;

        /// <summary>
        /// Reference to the currently running server instance (used by remote
        /// commands like stop).
        /// </summary>
        internal static ServerCore Instance { get; set; }

        public ServerCore()
        {
            Events.InitializeCommandsExtension();
        }

        private TcpListenerEx tcp;
        private UdpListener udp;
        private Thread thread;
        private bool terminate = false;
        private bool _remoteStopRequested = false;

        private Dictionary<string, (ulong time, ulong count)> _connectFlood;

        public bool Running { get; private set; }

        public iconnect.ICommandDefault[] DefaultCommandLevels
        {
            get { return Events.DefaultCommandLevels; }
        }

        public iconnect.IUser[] ConnectedUsers
        {
            get
            {
                var list = new List<iconnect.IUser>();
                if (UserPool.AUsers != null)
                    foreach (var a in UserPool.AUsers)
                        if (a.LoggedIn)
                            list.Add(a.IUser);
                if (UserPool.WUsers != null)
                    foreach (var w in UserPool.WUsers)
                        if (w.LoggedIn)
                            list.Add(w.IUser);
                return list.ToArray();
            }
        }

        /// <summary>
        /// Writes the connected users to a shared JSON file so other processes
        /// (e.g. the web panel) can show the same live user list as the GUI.
        /// </summary>
        private static void WriteUsersSnapshot()
        {
            try
            {
                var list = new List<object>();
                if (UserPool.AUsers != null)
                    foreach (var a in UserPool.AUsers)
                        if (a.LoggedIn)
                            list.Add(new
                            {
                                name = a.Name ?? "",
                                level = a.Level.ToString(),
                                ip = a.ExternalIP?.ToString() ?? ""
                            });
                if (UserPool.WUsers != null)
                    foreach (var w in UserPool.WUsers)
                        if (w.LoggedIn)
                            list.Add(new
                            {
                                name = w.Name ?? "",
                                level = w.Level.ToString(),
                                ip = w.ExternalIP?.ToString() ?? ""
                            });

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(list);
                string file = Path.Combine(DataPaths.Root, "users.json");
                WriteSharedFile(file, json);
            }
            catch { }
        }

        /// <summary>
        /// Writes a shared file atomically (temp file + move) with a few retries
        /// so the GUI and web panel can write/read the same files concurrently
        /// without leaving partial contents behind.
        /// </summary>
        private static void WriteSharedFile(string file, string content)
        {
            string tmp = file + ".tmp";
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.WriteAllText(tmp, content);
                    File.Move(tmp, file, true);
                    return;
                }
                catch
                {
                    System.Threading.Thread.Sleep(30);
                }
            }
        }

        /// <summary>
        /// Reads the shared users snapshot written by whichever process is
        /// running the server (GUI or web panel).
        /// </summary>
        public static List<UserSnapshotRow> ReadUsersSnapshot()
        {
            try
            {
                string file = Path.Combine(DataPaths.Root, "users.json");
                if (!File.Exists(file))
                    return new List<UserSnapshotRow>();
                string json = File.ReadAllText(file);
                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UserSnapshotRow>>(json);
                return list ?? new List<UserSnapshotRow>();
            }
            catch { return new List<UserSnapshotRow>(); }
        }

        /// <summary>
        /// Lists all current bans so the web panel can show and unban users.
        /// </summary>
        public static List<BanSnapshotRow> GetBans()
        {
            var result = new List<BanSnapshotRow>();
            try
            {
                BanSystem.EnsureLoaded();
                BanSystem.Eval(b => result.Add(new BanSnapshotRow
                {
                    name = b.Name ?? "",
                    version = b.Version ?? "",
                    guid = b.Guid.ToString(),
                    ip = b.ExternalIP?.ToString() ?? ""
                }));
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Removes a ban matching the given user name.
        /// </summary>
        public static bool Unban(string name)
        {
            try
            {
                BanSystem.EnsureLoaded();
                bool found = false;
                BanSystem.Eval(b =>
                {
                    if (b.Name == name && !found)
                    {
                        b.Unban();
                        found = true;
                    }
                });
                return found;
            }
            catch { return false; }
        }

        /// <summary>
        /// Writes the shared server status so other processes (GUI / web panel)
        /// know whether the server is running and who started it.
        /// </summary>
        public static void WriteServerStatus(bool running)
        {
            try
            {
                var obj = new
                {
                    running = running,
                    source = IsWebHost ? "web" : "gui",
                    time = DateTime.UtcNow.ToString("o")
                };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
                File.WriteAllText(Path.Combine(DataPaths.Root, "server_status.json"), json);
            }
            catch { }
        }

        /// <summary>
        /// True when this process is the web panel host (not the desktop GUI).
        /// </summary>
        private static bool IsWebHost
        {
            get
            {
                string name = AppDomain.CurrentDomain.FriendlyName;
                return name.IndexOf("sb0t.web", StringComparison.OrdinalIgnoreCase) >= 0
                       || name.IndexOf("sb0tweb", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>
        /// Reads the shared server status file.
        /// </summary>
        public static ServerStatusInfo ReadServerStatus()
        {
            try
            {
                string file = Path.Combine(DataPaths.Root, "server_status.json");
                if (!File.Exists(file))
                    return null;
                string json = File.ReadAllText(file);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<ServerStatusInfo>(json);
            }
            catch { return null; }
        }

        /// <summary>
        /// Executes remote commands (e.g. kick from the web panel) that were
        /// written to a shared file by another process.
        /// </summary>
        private void ProcessRemoteCommands()
        {
            try
            {
                string file = Path.Combine(DataPaths.Root, "commands.json");
                if (!File.Exists(file))
                    return;

                string json = File.ReadAllText(file);
                // consume the file immediately so commands run only once
                try { File.Delete(file); } catch { }

                var cmds = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RemoteCommand>>(json);
                if (cmds == null)
                    return;

                foreach (var cmd in cmds)
                {
                    if (cmd == null || string.IsNullOrEmpty(cmd.action))
                        continue;

                    if (cmd.action == "kick" && !string.IsNullOrEmpty(cmd.name))
                    {
                        if (UserPool.AUsers != null)
                            foreach (var a in UserPool.AUsers)
                                if (a.LoggedIn && a.Name == cmd.name)
                                {
                                    a.Disconnect();
                                    break;
                                }
                        if (UserPool.WUsers != null)
                            foreach (var w in UserPool.WUsers)
                                if (w.LoggedIn && w.Name == cmd.name)
                                {
                                    w.Disconnect();
                                    break;
                                }
                    }
                    else if (cmd.action == "ban" && !string.IsNullOrEmpty(cmd.name))
                    {
                        // Force ban: add to the ban database even for the owner,
                        // then disconnect. (Ban() itself refuses to ban owners.)
                        if (UserPool.AUsers != null)
                            foreach (var a in UserPool.AUsers)
                                if (a.LoggedIn && a.Name == cmd.name)
                                {
                                    BanSystem.AddBan(a);
                                    a.Disconnect();
                                    break;
                                }
                        if (UserPool.WUsers != null)
                            foreach (var w in UserPool.WUsers)
                                if (w.LoggedIn && w.Name == cmd.name)
                                {
                                    BanSystem.AddBan(w);
                                    w.Disconnect();
                                    break;
                                }
                    }
                    else if (cmd.action == "stop")
                    {
                        // Remote stop: signal the running server instance.
                        this._remoteStopRequested = true;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes a remote command that the process running the server will
        /// pick up and execute (used by the web panel).
        /// </summary>
        public static void WriteRemoteCommand(string action, string name)
        {
            try
            {
                string file = Path.Combine(DataPaths.Root, "commands.json");

                var list = new List<RemoteCommand>();
                if (File.Exists(file))
                {
                    try
                    {
                        string existing = File.ReadAllText(file);
                        var old = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RemoteCommand>>(existing);
                        if (old != null)
                            list = old;
                    }
                    catch { }
                }

                list.Add(new RemoteCommand { action = action, name = name });
                WriteSharedFile(file, Newtonsoft.Json.JsonConvert.SerializeObject(list));
            }
            catch { }
        }

        public static void Log(String message)
        {
            LogUpdate(null, new ServerLogEventArgs {Message = message});
        }

        public static void Log(String message, Exception e)
        {
            LogUpdate(null, new ServerLogEventArgs {Message = message, Error = e});
        }

        public bool Open()
        {
            Settings.Reset();
            Time.Reset();
            this.tcp = new TcpListenerEx(new IPEndPoint(IPAddress.Any, Settings.Port));
            _connectFlood = new Dictionary<string, (ulong time, ulong count)>();

            try
            {
                this.tcp.Start();
            }
            catch (Exception e)
            {
                LogUpdate(this, new ServerLogEventArgs {Message = "TCP Listener", Error = e});
                return false;
            }

            byte[] udp_address = Settings.Get<byte[]>("udp_address");

            if (udp_address == null)
            {
                udp_address = System.Net.IPAddress.Any.GetAddressBytes();
                Settings.Set("udp_address", udp_address);
            }

            this.udp = new UdpListener(new IPEndPoint(new IPAddress(udp_address), Settings.Port));

            try
            {
                this.udp.Start();
            }
            catch (Exception e)
            {
                LogUpdate(this, new ServerLogEventArgs {Message = "UDP Listener", Error = e});
                return false;
            }

            LogUpdate(this,
                new ServerLogEventArgs
                {
                    Message = "Server initialized on port " + ((IPEndPoint) this.tcp.LocalEndpoint).Port
                });
            this.thread = new Thread(new ThreadStart(this.ServerThread));
            this.thread.Start();
            this.Running = true;
            Settings.RUNNING = true;
            Instance = this;
            WriteServerStatus(true);

            return true;
        }

        public void Close()
        {
            Settings.RUNNING = false;
            this.Running = false;
            this.terminate = true;
            if (Instance == this)
                Instance = null;
            WriteServerStatus(false);

            try
            {
                this.tcp.Stop();
            }
            catch
            {
            }

            try
            {
                this.udp.Stop();
            }
            catch
            {
            }

            UserPool.Destroy();
            core.LinkHub.LeafPool.Destroy();
            UdpChannelList.Stop();

            if (Linker != null)
                Linker.KillSocket();
        }

        private void ServerThread()
        {
            this.terminate = false;

            // Ensure the linking GUID exists (the GUI normally creates it on its
            // linking page; the web-hosted server needs it for hub/leaf mode).
            if (Settings.Get<byte[]>("guid") == null)
                Settings.Set("guid", Guid.NewGuid().ToByteArray());

            FilterImporter.DoTasks();
            ObSalt.Init();
            CaptchaManager.Load();
            FloodControl.Reset();
            Stats.Reset();
            UserPool.Build();
            core.LinkHub.LeafPool.Build();
            Captcha.Initialize();
            UserHistory.Initialize();
            Avatars.LoadFromDisk();
            AccountManager.LoadPasswords();
            BanSystem.LoadBans();
            IdleManager.Reset();
            Proxies.Start(Helpers.UnixTime);
            IgnoreManager.init();

            if (Settings.Get<bool>("roomsearch"))
                UdpChannelList.Start();

            ulong last_update_check = 0;
            ulong fast_ping_timer = Time.Now;
            ulong reset_floods_timer = Time.Now;
            ulong room_search_timer = (Time.Now - 1800000);
            ulong connect_flood_expire = Time.Now + 300000;
            bool can_web_chat = Settings.Get<bool>("enabled", "web");
            core.LinkHub.LinkMode link_mode = (core.LinkHub.LinkMode) Settings.Get<int>("link_mode");
            Linker = new LinkLeaf.LinkClient();

            if (link_mode == LinkHub.LinkMode.Hub)
                Linker.ConnectLocal();

            Events.ServerStarted();

            while (true)
            {
                if (this.terminate)
                    break;

                ulong time = Time.Now;

                if (time > (last_update_check + (30 * 60 * 1000)))
                {
                    last_update_check = time;
                    CheckLatestVersion();
                }

                if (time > (fast_ping_timer + 2000))
                {
                    fast_ping_timer = time;

                    UserPool.AUsers.ForEachWhere(x => x.SendPacket(TCPOutbound.FastPing()),
                        x => x.LoggedIn && x.FastPing);

                    BanSystem.AutoClearBans();
                    Avatars.CheckAvatars(time);
                    WriteUsersSnapshot();
                    ProcessRemoteCommands();

                    if (_remoteStopRequested)
                        break;
                }

                if (time > (reset_floods_timer + 60000))
                {
                    reset_floods_timer = time;
                    FloodControl.Reset();
                    Proxies.Updater(Helpers.UnixTime);

                    foreach(var item in _connectFlood.Where(kvp => kvp.Value.time < (time + connect_flood_expire)).ToList())
                    {
                        _connectFlood.Remove(item.Key);
                    }

                }

                this.udp.ServiceUdp(time);
                this.CheckTCPListener(time);
                this.ServiceAresSockets(time);
                this.ServiceLeaves(link_mode, time);
                this.ServiceWW();
                Linker.Service(time);

                if (can_web_chat)
                {
                    this.ServiceWebSockets(time);

                }

                if (time > (room_search_timer + 1800000))
                {
                    room_search_timer = time;

                    if (Settings.Get<bool>("roomsearch"))
                        UdpChannelList.Update();
                }

                Events.CycleTick();
                Thread.Sleep(25);
            }

            if (_remoteStopRequested)
            {
                // Remote stop requested from another process (e.g. the GUI):
                // finish cleanup exactly like Close() does.
                Settings.RUNNING = false;
                this.Running = false;
                this.terminate = true;
                try { this.tcp.Stop(); } catch { }
                try { this.udp.Stop(); } catch { }
                UserPool.Destroy();
                core.LinkHub.LeafPool.Destroy();
                UdpChannelList.Stop();
                if (Linker != null)
                    Linker.KillSocket();
                Instance = null;
                WriteServerStatus(false);
            }
        }

        private void CheckTCPListener(ulong time)
        {
            if (!this.tcp.Active)
                return;

            for (int i = 0; i < 5; i++)
            {
                if (!this.tcp.Pending())
                    return;

                Socket sock = this.tcp.AcceptSocket();
                Stream networkStream = new NetworkStream(sock);
                Stream sslStream = new SslStream(networkStream);
                // NOTE: When using a reverse proxy, this IP will be the proxy's IP, not the real client IP.
                // The real client IP is resolved later during the WebSocket handshake
                // by reading X-Forwarded-For/X-Real-IP headers in ib0tClient.LoadProtocol().
                // Therefore, flood tracking here may record the proxy IP instead of the real client.
                string ipAddress = ((IPEndPoint)sock.RemoteEndPoint).Address.ToString();
                if (_connectFlood.ContainsKey(ipAddress))
                {
                    if (_connectFlood[ipAddress].count >= 3)
                    {
                        var item = _connectFlood[ipAddress];
                        item.time = time;
                        _connectFlood[ipAddress] = item;


                        sock.Dispose();
                        return;
                    }
                }

                if (!this.udp.IsTcpChecker(sock))
                    UserPool.CreateAresClient(sock, time);
            }
        }

        private void ServiceWW()
        {
            foreach (WebWorker w in UserPool.WW)
                w.DoSocketTasks();

            UserPool.WW.RemoveAll(x => x.Dead);
        }

        private void ServiceWebSockets(ulong time)
        {
            foreach (ib0t.ib0tClient client in UserPool.WUsers)
            {
                client.SendReceive();

                if (!client.ProtoConnected)
                {
                    if (client.LoadProtocol())
                    {
                        try
                        {
                            byte[] packet = ib0t.WebSockets.Html5HandshakeReplyPacket(client.WebCredentials);
                            client.QueuePacket(packet);
                            client.ProtoConnected = true;
                            client.Time = time;
                            continue;
                        }
                        catch
                        {
                            client.SocketConnected = false;
                        }
                    }

                    if (!client.SocketConnected || (client.Time + 10000) < time)
                        client.Disconnect();
                }
                else
                {
                    if (!client.LoggedIn)
                        if (!client.SocketConnected || (client.Time + 15000) < time)
                        {
                            client.Disconnect();
                            continue;
                        }

                    String message;

                    while ((message = client.NextMessage) != null && client.SocketConnected)
                    {
                        int i = message.IndexOf(":");

                        if (i == -1)
                        {
                            Log("bad protocol from " + client.ID);
                            client.Disconnect();
                            break;
                        }

                        String ident = message.Substring(0, i);
                        String args = message.Substring(i + 1);

                        try
                        {
                            ib0t.WebProcessor.Evaluate(client, ident, args, time);
                        }
                        catch (Exception e)
                        {
                            client.Disconnect();
                            Log("packet read fail from " + client.ExternalIP + " " + ident, e);
                            break;
                        }
                    }

                    if (!client.SocketConnected || (client.Time + 120000) < time)
                        client.Disconnect();
                }
            }

            UserPool.WUsers.ForEachWhere(x => x.Disconnect(), x => !x.SocketConnected);
            UserPool.WUsers.RemoveAll(x => !x.SocketConnected);
        }

        private void ServiceAresSockets(ulong time)
        {
            foreach (AresClient client in UserPool.AUsers)
                if (!String.IsNullOrEmpty(client.DNS))
                {
                    if (client.IsHTML)
                    {
                        if (Settings.Get<bool>("enabled", "web"))
                            UserPool.CreateIb0tClient(client, time);
                        else
                            client.Disconnect();
                        break;
                    }

                    if (client.IsWebWorker)
                    {
                        UserPool.CreateWW(client);
                        client.Disconnect();
                        break;
                    }

                    TCPPacket packet = null;

                    while ((packet = client.NextReceivedPacket) != null && client.SocketConnected)
                        if (packet.Msg == TCPMsg.MSG_CHAT_CLIENTCOMPRESSED)
                            client.InsertUnzippedData(Zip.Decompress(packet.Packet.ToArray()));
                        else
                            try
                            {
                                TCPProcessor.Eval(client, packet, time);

                                if (client.IsLeaf)
                                    break;
                            }
                            catch (Exception e)
                            {
                                client.Disconnect();

                                if (packet.Msg != TCPMsg.MSG_CHAT_ADVANCED_FEATURES_PROTOCOL)
                                    Log("packet read fail from " + client.ExternalIP + " " + packet.Msg, e);
                                else if (packet.Packet.ToArray().Length >= 3)
                                    Log(
                                        "packet read fail from " + client.ExternalIP + " " +
                                        (TCPMsg)packet.Packet.ToArray()[2], e);
                                else
                                    Log("packet read fail from " + client.ExternalIP + " (Advanced Protocol Exploit)", e);


                                break;
                            }

                    if (client.IsLeaf)
                        continue;

                    client.SendReceive();

                    if (client.SocketConnected)
                        client.EnforceRules(time);

                    if (client.SocketConnected)
                        continue;

                    string ipAddress = client.SocketAddr.ToString();
                    if (_connectFlood.ContainsKey(ipAddress))
                    {
                        var item = _connectFlood[ipAddress];
                        _connectFlood[ipAddress] = (time, item.count + 1);
                    }
                    else
                        _connectFlood[ipAddress] = (time, 1);
                }

            UserPool.AUsers.ForEachWhere(x => x.Disconnect(), x => !x.SocketConnected);
            UserPool.AUsers.RemoveAll(x => !x.SocketConnected || x.IsLeaf);
        }

        private void ServiceLeaves(core.LinkHub.LinkMode mode, ulong time)
        {
            foreach (core.LinkHub.Leaf leaf in core.LinkHub.LeafPool.Leaves)
            {
                core.LinkHub.LinkPacket packet = null;

                while ((packet = leaf.NextReceivedPacket) != null && leaf.SocketConnected)
                    try
                    {
                        core.LinkHub.HubProcessor.Eval(leaf, packet.Msg, packet.Packet, time, mode);
                    }
                    catch (Exception e)
                    {
                        leaf.Disconnect();
                        Log("packet read fail from leaf " + leaf.Ident + " " + packet.Msg, e);
                        break;
                    }

                leaf.SendReceive();

                if (leaf.SocketConnected)
                    leaf.EnforceRules(time);
            }

            core.LinkHub.LeafPool.Leaves.ForEachWhere(x => x.Disconnect(), x => !x.SocketConnected);
            core.LinkHub.LeafPool.Leaves.RemoveAll(x => !x.SocketConnected);
        }

        private static async void CheckLatestVersion()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add(
                        "User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36"
                    );

                    using (var response = await client.GetAsync(Settings.VERSION_CHECK_URL))
                    using (var content = response.Content)
                    {
                        {
                            var result = await content.ReadAsStringAsync();

                            if (result == null)
                            {
                                return;
                            }

                            string version = null;

                            try
                            {
                                var json = Newtonsoft.Json.Linq.JArray.Parse(result);
                                if (json.Count > 0)
                                    version = (string)json[0]["tag_name"];
                            }
                            catch
                            {
                                // Response is not an array (e.g. an API error object).
                            }

                            if (string.IsNullOrEmpty(version))
                                return;

                            version = version.ToLower().Replace("v", "").Trim();


                            if (new Version(Settings.VERSION_NUMBER).CompareTo(new Version(version)) >= 0)
                            {
                                return;
                            }

                            String message =
                                "A new version(v" + version + ") of sb0t is available, download it at " +
                                Settings.RELEASE_URL;

                            // ares users
                            UserPool.AUsers.ForEachWhere(x =>
                            {
                                if (x.Level >= ILevel.Host)
                                {
                                    x.Print(message);
                                    x.PM(Settings.Get<String>("bot"), message);
                                }
                            }, x => x.LoggedIn && !x.Quarantined && x.Owner);

                            // web users
                            UserPool.WUsers.ForEachWhere(x =>
                            {
                                if (x.Level >= ILevel.Host)
                                    x.Print(message);
                                x.PM(Settings.Get<String>("bot"), message);
                            }, x => x.LoggedIn && !x.Quarantined && x.Owner);
                
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
        }
    }

    public class ServerLogEventArgs : EventArgs
    {
        public String Message { get; set; }
        public Exception Error { get; set; }
    }

    class TcpListenerEx : TcpListener
    {
        public TcpListenerEx(IPEndPoint addr) : base(addr) { }
        
        public new bool Active
        {
            get
            {
                return base.Active;
            }
        } 
    }

    public class UserSnapshotRow
    {
        public string name { get; set; }
        public string level { get; set; }
        public string ip { get; set; }
    }

    public class RemoteCommand
    {
        public string action { get; set; }
        public string name { get; set; }
    }

    public class ServerStatusInfo
    {
        public bool running { get; set; }
        public string source { get; set; }
        public string time { get; set; }
    }

    public class BanSnapshotRow
    {
        public string name { get; set; }
        public string version { get; set; }
        public string guid { get; set; }
        public string ip { get; set; }
    }
}
