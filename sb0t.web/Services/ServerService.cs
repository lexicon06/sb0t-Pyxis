using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using core;
using iconnect;

namespace sb0t.web.Services
{
    /// <summary>
    /// Singleton wrapper around ServerCore so the web panel and the server
    /// share the same instance and live state.
    /// </summary>
    public class ServerService
    {
        public ServerCore Server { get; }
        public event Action<string>? LogReceived;

        private readonly List<string> _logBuffer = new List<string>();
        private readonly object _logLock = new object();

        public ServerService()
        {
            Server = new ServerCore();
            ServerCore.LogUpdate += OnLog;
        }

        private void OnLog(object? sender, ServerLogEventArgs e)
        {
            string line = e.Message ?? "";
            if (e.Error != null)
                line += " : " + e.Error.Message;
            if (string.IsNullOrEmpty(line))
                return;
            lock (_logLock)
            {
                _logBuffer.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
                if (_logBuffer.Count > 500)
                    _logBuffer.RemoveAt(0);
            }
            LogReceived?.Invoke(line);
        }

        public IReadOnlyList<string> RecentLogs(int count = 100)
        {
            lock (_logLock)
                return _logBuffer.TakeLast(count).ToList();
        }

        public bool Running => Server.Running || (IsExternallyRunning() && IsPortInUse());

        /// <summary>
        /// True if the shared server status says the server is running and it
        /// was started by another process (e.g. the desktop GUI).
        ///
        /// The status file can go stale when the process that started the server
        /// dies without writing the "stopped" marker (crash / window close).
        /// To guard against that, we only trust the marker when the configured
        /// port is actually being served by another process. If the port is free
        /// it means the server is really down, so we report it as not running and
        /// clear the stale marker.
        /// </summary>
        public bool IsExternallyRunning()
        {
            var status = core.ServerCore.ReadServerStatus();
            if (status == null || !status.running)
                return false;
            if (string.Equals(status.source, "web", StringComparison.OrdinalIgnoreCase))
                return Server.Running;

            // Started by the GUI: only real if another process owns the port.
            if (IsPortInUse())
                return true;

            // The GUI is gone but left a stale "running" marker -> clean it up.
            core.ServerCore.WriteServerStatus(false);
            return false;
        }

        /// <summary>
        /// True if another process (e.g. the desktop GUI) is already listening on
        /// the configured server port.
        /// </summary>
        public bool IsPortInUse()
        {
            try
            {
                ushort port = Settings.Get<ushort>("port");
                if (port == 0)
                    port = 54321;

                var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                tcp.Start();
                tcp.Stop();
                return false; // port free
            }
            catch
            {
                return true; // port in use -> server likely running elsewhere
            }
        }

        public bool Start()
        {
            if (Server.Running)
                return true;
            if (IsExternallyRunning())
            {
                // Another process (the GUI) owns the port: it is already serving.
                // If it is stale this fixes the marker via IsExternallyRunning().
                return true;
            }
            if (IsPortInUse())
            {
                // A stuck process is holding the port -> kill it so we can
                // start a clean instance.
                KillServerProcess();
            }
            core.Extensions.ExtensionManager.Setup();
            foreach (var ext in iconnect.SettingsStore.Keys("extautorun"))
                core.Extensions.ExtensionManager.LoadPlugin(ext);
            return Server.Open();
        }

        public void Stop()
        {
            if (Server.Running)
            {
                Server.Close();
                return;
            }

            if (IsExternallyRunning())
            {
                // Server was started by the desktop GUI: first try a graceful
                // stop, then kill any process still holding the port so we can
                // guarantee a clean slate.
                core.ServerCore.WriteRemoteCommand("stop", null);
                KillServerProcess();
            }
        }

        /// <summary>
        /// Terminates any process (other than this one) that is holding the
        /// configured server port. Used to recover a stuck server that was
        /// started by the desktop GUI, so we can run a clean instance.
        /// </summary>
        private void KillServerProcess()
        {
            try
            {
                ushort port = Settings.Get<ushort>("port");
                if (port == 0)
                    port = 54321;

                int currentPid = Process.GetCurrentProcess().Id;

                // Only act if something external is really holding the port.
                bool inUse = false;
                var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                try
                {
                    tcp.Start();
                    tcp.Stop();
                }
                catch
                {
                    inUse = true;
                }

                if (inUse)
                {
                    // The only other server host on this machine is the GUI.
                    foreach (var p in Process.GetProcessesByName("sb0t"))
                    {
                        if (p.Id == currentPid)
                            continue;
                        try { p.Kill(); } catch { }
                    }
                }

                core.ServerCore.WriteServerStatus(false);
            }
            catch { }
        }

        public string WebPassword
        {
            get
            {
                string pwd = Settings.Get<string>("password", "web");
                if (string.IsNullOrEmpty(pwd))
                {
                    pwd = GeneratePassword();
                    Settings.Set("password", pwd, "web");
                }
                return pwd;
            }
            set { Settings.Set("password", value, "web"); }
        }

        public bool CheckPassword(string password)
        {
            string pwd = Settings.Get<string>("password", "web");
            return !string.IsNullOrEmpty(pwd) && pwd == password;
        }

        private static string GeneratePassword()
        {
            var chars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
            var rnd = new Random();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 12; i++)
                sb.Append(chars[(int)Math.Floor(rnd.NextDouble() * chars.Length)]);
            return sb.ToString();
        }

        // ================= USERS =================

        public IEnumerable<UserRow> Users
        {
            get
            {
                var result = new List<UserRow>();
                // Prefer the shared snapshot (written by whichever process runs
                // the server) so the web shows the same live users as the GUI.
                var snap = core.ServerCore.ReadUsersSnapshot();
                if (snap.Count > 0)
                {
                    foreach (var u in snap)
                        result.Add(new UserRow
                        {
                            Name = u.name ?? "",
                            Level = u.level ?? "",
                            Ip = u.ip ?? ""
                        });
                }
                else
                {
                    foreach (var u in Server.ConnectedUsers)
                        result.Add(new UserRow
                        {
                            Name = u.Name ?? "",
                            Level = u.Level.ToString(),
                            Ip = u.ExternalIP?.ToString() ?? ""
                        });
                }
                return result;
            }
        }

        public void Kick(string name)
        {
            // Try direct (if this process runs the server), and also write a
            // remote command so the server process (GUI) executes it.
            if (Server.Running)
                foreach (var u in Server.ConnectedUsers)
                    if (u.Name == name)
                    {
                        u.Disconnect();
                        return;
                    }
            core.ServerCore.WriteRemoteCommand("kick", name);
        }

        public void Ban(string name)
        {
            // Write a remote command so the server process (web or GUI)
            // executes the force-ban (owner included) and persists it.
            core.ServerCore.WriteRemoteCommand("ban", name);
        }

        // ================= BANS =================

        public IEnumerable<BanRow> Bans
        {
            get
            {
                var result = new List<BanRow>();
                foreach (var b in core.ServerCore.GetBans())
                    result.Add(new BanRow
                    {
                        Name = b.name ?? "",
                        Version = b.version ?? "",
                        Guid = b.guid ?? "",
                        Ip = b.ip ?? ""
                    });
                return result;
            }
        }

        public void Unban(string name)
        {
            core.ServerCore.Unban(name);
        }

        // ================= GENERIC SETTINGS =================

        public T Get<T>(string key, params string[] sub)
        {
            return Settings.Get<T>(key, sub);
        }

        public void Set(string key, object value, params string[] sub)
        {
            Settings.Set(key, value, sub);
        }

        // ================= COMMANDS / ADMIN =================

        public IEnumerable<CommandRow> CommandLevels
        {
            get
            {
                var result = new List<CommandRow>();
                foreach (var c in Server.DefaultCommandLevels)
                {
                    ILevel l = GetCommandLevel(c.Name);
                    if (l == (ILevel)255)
                    {
                        l = c.Level;
                        SetCommandLevel(c.Name, c.Level);
                    }
                    result.Add(new CommandRow
                    {
                        Name = c.Name,
                        Level = LevelToString(l)
                    });
                }
                return result;
            }
        }

        public void SetCommandLevel(string name, ILevel level)
        {
            Settings.Set(name, (byte)level, "commands");
            Settings.Set(name + "_set", 1, "commands");
        }

        public ILevel GetCommandLevel(string command)
        {
            byte value = Settings.Get<byte>(command, "commands");
            if (Settings.Get<int>(command + "_set", "commands") == 1)
                return (ILevel)value;
            return (ILevel)255;
        }

        public static string LevelToString(ILevel level)
        {
            switch (level)
            {
                case ILevel.Administrator: return "Administrator";
                case ILevel.Host: return "Host";
                case ILevel.Moderator: return "Moderator";
                case (ILevel)254: return "Disabled";
                default: return "Regular";
            }
        }

        public static ILevel StringToLevel(string level)
        {
            switch (level)
            {
                case "Administrator": return ILevel.Administrator;
                case "Host": return ILevel.Host;
                case "Moderator": return ILevel.Moderator;
                case "Disabled": return (ILevel)254;
                case "Regular": return ILevel.Regular;
                default: return (ILevel)255;
            }
        }

        public static string[] CommandOptions => new[] { "Regular", "Moderator", "Administrator", "Host", "Disabled" };

        // ================= LINKING / TRUSTED LEAVES =================

        public IEnumerable<TrustedLeafRow> TrustedLeaves
        {
            get
            {
                core.LinkHub.TrustedLeavesManager.Init();
                var result = new List<TrustedLeafRow>();
                foreach (var item in core.LinkHub.TrustedLeavesManager.Items)
                    result.Add(new TrustedLeafRow { Name = item.Name ?? "", Guid = item.Guid });
                return result;
            }
        }

        public bool AddTrustedLeaf(string name, Guid guid)
        {
            return core.LinkHub.TrustedLeavesManager.AddItem(new core.LinkHub.TrustedLeafItem { Name = name, Guid = guid });
        }

        public void RemoveTrustedLeaf(string name)
        {
            var item = core.LinkHub.TrustedLeavesManager.Items.FirstOrDefault(x => x.Name == name);
            if (item != null)
                core.LinkHub.TrustedLeavesManager.RemoveItem(item);
        }

        public string LeafIdentifier
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                byte[] guid = Settings.Get<byte[]>("guid");
                if (guid == null)
                {
                    guid = Guid.NewGuid().ToByteArray();
                    Settings.Set("guid", guid);
                }
                byte[] name = System.Text.Encoding.UTF8.GetBytes(Settings.Get<string>("name") ?? "");
                sb.Append((char)name.Length);
                sb.Append(System.Text.Encoding.UTF8.GetString(name));
                sb.Append(System.Text.Encoding.UTF8.GetString(guid));
                var arr = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                Array.Reverse(arr);
                return "sblnk://" + Convert.ToBase64String(arr);
            }
        }

        // ================= PROXIES =================

        public IEnumerable<string> TrustedProxies => core.TrustedProxyManager.GetTrustedProxies();

        public void AddTrustedProxy(string ip)
        {
            core.TrustedProxyManager.AddTrustedProxy(ip);
        }

        public void RemoveTrustedProxy(string ip)
        {
            core.TrustedProxyManager.RemoveTrustedProxy(ip);
        }

        // ================= AVATARS =================

        public void UpdateServerAvatar(byte[] data)
        {
            string dir = core.DataPaths.Avatars;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "server"), data);
            Avatars.UpdateServerAvatar(data);
        }

        public void UpdateDefaultAvatar(byte[] data)
        {
            string dir = core.DataPaths.Avatars;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "default"), data);
            Avatars.UpdateDefaultAvatar(data);
        }

        public byte[] GetServerAvatar()
        {
            string p = System.IO.Path.Combine(core.DataPaths.Avatars, "server");
            return System.IO.File.Exists(p) ? System.IO.File.ReadAllBytes(p) : null;
        }

        public byte[] GetDefaultAvatar()
        {
            string p = System.IO.Path.Combine(core.DataPaths.Avatars, "default");
            return System.IO.File.Exists(p) ? System.IO.File.ReadAllBytes(p) : null;
        }

        // ================= EXTENSIONS =================

        /// <summary>
        /// Enumerates the Extensions folder and reports which extensions are
        /// installed, which are currently mounted, and which are set to auto-load.
        /// </summary>
        public IEnumerable<ExtensionRow> Extensions
        {
            get
            {
                var loaded = new HashSet<string>(core.Extensions.ExtensionManager.Plugins.Select(p => p.Name));
                var autoRun = new HashSet<string>(iconnect.SettingsStore.Keys("extautorun"));
                var result = new List<ExtensionRow>();

                string dir = core.DataPaths.Extensions;
                if (System.IO.Directory.Exists(dir))
                {
                    foreach (var folder in System.IO.Directory.GetDirectories(dir))
                    {
                        string name = System.IO.Path.GetFileName(folder);
                        if (System.IO.File.Exists(System.IO.Path.Combine(folder, "extension.dll")))
                        {
                            result.Add(new ExtensionRow
                            {
                                Name = name,
                                Loaded = loaded.Contains(name),
                                AutoRun = autoRun.Contains(name)
                            });
                        }
                    }
                }
                return result;
            }
        }

        /// <summary>Mount (load) an extension by folder name. Returns false on failure.</summary>
        public bool LoadExtension(string name)
        {
            return core.Extensions.ExtensionManager.LoadPlugin(name) != null;
        }

        /// <summary>Unmount (unload) an extension by folder name.</summary>
        public void UnloadExtension(string name)
        {
            core.Extensions.ExtensionManager.UnloadPlugin(name);
        }

        /// <summary>Add or remove an extension from the startup auto-load list.</summary>
        public void SetExtensionAutoRun(string name, bool enabled)
        {
            if (enabled)
                iconnect.SettingsStore.Set(name, 0, "extautorun");
            else
                iconnect.SettingsStore.Delete(name, "extautorun");
        }
    }

    public class ExtensionRow
    {
        public string Name { get; set; } = "";
        public bool Loaded { get; set; }
        public bool AutoRun { get; set; }
    }

    public class UserRow
    {
        public string Name { get; set; } = "";
        public string Level { get; set; } = "";
        public string Ip { get; set; } = "";
    }

    public class CommandRow
    {
        public string Name { get; set; } = "";
        public string Level { get; set; } = "";
    }

    public class TrustedLeafRow
    {
        public string Name { get; set; } = "";
        public Guid Guid { get; set; }
    }

    public class BanRow
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Guid { get; set; } = "";
        public string Ip { get; set; } = "";
    }
}
