using System;
using System.Collections.Generic;
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

        public bool Running => Server.Running || IsExternallyRunning();

        /// <summary>
        /// True if the shared server status says the server is running and it
        /// was started by another process (e.g. the desktop GUI).
        /// </summary>
        public bool IsExternallyRunning()
        {
            var status = core.ServerCore.ReadServerStatus();
            return status != null && status.running && !string.Equals(status.source, "web", StringComparison.OrdinalIgnoreCase);
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
                return true;
            core.Extensions.ExtensionManager.Setup();
            foreach (var ext in iconnect.SettingsStore.Keys("extautorun"))
                core.Extensions.ExtensionManager.LoadPlugin(ext);
            return Server.Open();
        }

        public void Stop()
        {
            if (Server.Running)
                Server.Close();
            else if (IsExternallyRunning())
                core.ServerCore.WriteRemoteCommand("stop", null);
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
