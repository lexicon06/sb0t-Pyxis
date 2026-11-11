using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using iconnect;

namespace WeatherExtension
{
    /// <summary>
    /// Extension DLL para sb0t Pyxis. Cuando un usuario escribe "iclima &lt;ciudad&gt;"
    /// en la sala, se consulta el clima, se busca la foto de la ciudad y se compone
    /// la tarjeta 800x400 en C# (ImageSharp) y se manda como scribble a la sala
    /// (y salas linkeadas). El render final es JPEG.
    ///
    /// Deploy: extension.dll + SixLabors.ImageSharp.Drawing.dll + SixLabors.Fonts.dll
    /// en &lt;Root&gt;\Extensions\WeatherExtension\, junto con weather.json.
    /// </summary>
    public class WeatherExtension : IExtension
    {
        private class WeatherConfig
        {
            public string apiKey { get; set; } = "";
            public string trigger { get; set; } = "iclima";
            public bool photo { get; set; } = true;
            public bool bingFallback { get; set; } = true;
            public int cooldownSeconds { get; set; } = 5;
            public int cacheMinutes { get; set; } = 5;
        }

        private sealed class CachedCard
        {
            public byte[] Jpeg;
            public int Height;
            public DateTime Expires;
        }

        private readonly IHostApp host;
        private readonly HttpClient http;
        private readonly string configPath;
        private WeatherConfig config;

        private readonly ConcurrentDictionary<string, CachedCard> cache =
            new ConcurrentDictionary<string, CachedCard>();
        private readonly ConcurrentDictionary<string, DateTime> cooldowns =
            new ConcurrentDictionary<string, DateTime>();
        private readonly SemaphoreSlim renderGate = new SemaphoreSlim(2, 2);

        public WeatherExtension(IHostApp host)
        {
            this.host = host;
            this.configPath = Path.Combine(host.DataPath, "weather.json");

            var handler = new SocketsHttpHandler { UseProxy = false };
            this.http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            try
            {
                this.http.DefaultRequestHeaders.UserAgent.ParseAdd("sb0t-weather-extension/1.0");
            }
            catch { }

            LoadConfig();
            host.WriteLog("WeatherExtension: listo (trigger '" + this.config.trigger + "', sin API key)");
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    config = JsonSerializer.Deserialize<WeatherConfig>(File.ReadAllText(configPath))
                             ?? new WeatherConfig();
                }
                else
                {
                    config = new WeatherConfig();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                host.WriteLog("WeatherExtension: config invalida, usando defaults -> " + ex.Message);
                config = new WeatherConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(configPath,
                    JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                host.WriteLog("WeatherExtension: no pude guardar weather.json -> " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Disparador
        // ------------------------------------------------------------------
        public void TextReceived(IUser client, string text)
        {
            if (client == null || string.IsNullOrWhiteSpace(text))
                return;

            string trigger = (config.trigger ?? "iclima").Trim().ToLowerInvariant();
            if (trigger.Length == 0) trigger = "iclima";

            string trimmed = text.TrimStart();
            string lower = trimmed.ToLowerInvariant();

            string query = null;
            if (lower == trigger)
                query = "";
            else if (lower.StartsWith(trigger + " "))
                query = trimmed.Substring(trigger.Length + 1).Trim();
            else if (lower == "/" + trigger)
                query = "";
            else if (lower.StartsWith("/" + trigger + " "))
                query = trimmed.Substring(trigger.Length + 2).Trim();

            if (query == null)
                return;

            if (query.Length == 0)
            {
                SafePrint(client, "Uso: " + trigger + " <ciudad>");
                return;
            }

            string key = !string.IsNullOrEmpty(client.Name) ? client.Name : client.Guid.ToString();
            DateTime now = DateTime.UtcNow;
            if (cooldowns.TryGetValue(key, out DateTime last) &&
                (now - last).TotalSeconds < Math.Max(0, config.cooldownSeconds))
            {
                double left = Math.Max(0, config.cooldownSeconds - (now - last).TotalSeconds);
                SafePrint(client, "Espera " + Math.Ceiling(left) + "s para pedir otro clima.");
                return;
            }
            cooldowns[key] = now;

            ushort vroom = client.Vroom;
            Task.Run(() => HandleAsync(client, vroom, query));
        }

        private void HandleAsync(IUser client, ushort vroom, string query)
        {
            try
            {
                byte[] jpeg = BuildCard(query, out string error);

                if (jpeg == null)
                {
                    SafePrint(client, error ?? "No se pudo generar la tarjeta.");
                    return;
                }

                Broadcast(vroom, jpeg);
            }
            catch (Exception ex)
            {
                host.WriteLog("WeatherExtension: error -> " + ex.GetType().Name + ": " + ex.Message);
                SafePrint(client, "Error generando la tarjeta de clima.");
            }
        }

        private byte[] BuildCard(string query, out string error)
        {
            error = null;
            string key = query.ToLowerInvariant().Trim();

            if (cache.TryGetValue(key, out CachedCard card) && card.Expires > DateTime.UtcNow)
                return card.Jpeg;

            renderGate.Wait();
            try
            {
                if (cache.TryGetValue(key, out card) && card.Expires > DateTime.UtcNow)
                    return card.Jpeg;

                WeatherApiResponse data = WeatherCard.Fetch(http, config.apiKey, query, out error);
                if (data == null)
                    return null;

                byte[] icon = WeatherCard.DownloadIcon(http, data.Current?.Condition?.Icon);

                byte[] photo = null;
                string credito = null;
                if (config.photo)
                    photo = WeatherCard.FotoCiudad(http, host.DataPath, data.Location.Name, data.Location.Region,
                                                  data.Location.Country, false, config.bingFallback, out credito);

                byte[] jpeg = WeatherCard.Render(data, icon, photo, credito);

                cache[key] = new CachedCard
                {
                    Jpeg = jpeg,
                    Height = 400,
                    Expires = DateTime.UtcNow.AddMinutes(Math.Max(1, config.cacheMinutes)),
                };

                return jpeg;
            }
            catch (Exception ex)
            {
                error = "Error generando la tarjeta: " + ex.Message;
                return null;
            }
            finally
            {
                renderGate.Release();
            }
        }

        private void Broadcast(ushort vroom, byte[] jpeg)
        {
            string bot = host.Room?.BotName ?? "Server";
            if (string.IsNullOrEmpty(bot)) bot = "Server";

            string base64 = Convert.ToBase64String(jpeg);
            byte[] compressed = host.Compression.Compress(jpeg);

            host.Users.All(u =>
            {
                try
                {
                    if (u == null || !u.Connected || u.Quarantined)
                        return;

                    // Usuario de una sala linkeada: se manda via el relay del link.
                    // No filtramos por Vroom (no se sincroniza por el link) y el
                    // destino decide como entregarlo (cb0t/web).
                    bool linked = u.Link != null && u.Link.IsLinked;
                    if (linked)
                    {
                        u.Scribble(bot, compressed, 400);
                        return;
                    }

                    // Locales: solo la misma sala virtual.
                    if (u.Vroom != vroom)
                        return;

                    if (u.IsInbizierWeb || u.IsInbizierMobile)
                        u.Scribble2(bot, base64, 400);
                    else if (!u.WebClient && u.CustomClient)
                        u.Scribble(bot, compressed, 400);
                }
                catch { }
            });
        }

        private static void SafePrint(IUser client, string text)
        {
            try { client?.Print(text); } catch { }
        }

        // ------------------------------------------------------------------
        // IExtension
        // ------------------------------------------------------------------
        public void Load() { }

        public void ServerStarted() { }

        public void CycleTick() { }

        public void UnhandledProtocol(IUser client, bool custom, byte msg, byte[] packet) { }

        public bool Joining(IUser client) { return true; }

        public void Joined(IUser client) { }

        public void Rejected(IUser client, RejectedMsg msg) { }

        public void Parting(IUser client) { }

        public void Parted(IUser client) { }

        public bool AvatarReceived(IUser client) { return true; }

        public bool PersonalMessageReceived(IUser client, string text) { return true; }

        public string TextSending(IUser client, string text) { return text; }

        public void TextSent(IUser client, string text) { }

        public void EmoteReceived(IUser client, string text) { }

        public string EmoteSending(IUser client, string text) { return text; }

        public void EmoteSent(IUser client, string text) { }

        public void PrivateSending(IUser client, IUser target, IPrivateMsg msg) { }

        public void PrivateSent(IUser client, IUser target) { }

        public void BotPrivateSent(IUser client, string text) { }

        public void Command(IUser client, string command, IUser target, string args) { }

        public bool Nick(IUser client, string name) { return true; }

        public void Help(IUser client)
        {
            SafePrint(client, "Clima: escribe '" + (config.trigger ?? "iclima") + " <ciudad>' en la sala.");
        }

        public void FileReceived(IUser client, string filename, string title, MimeType type) { }

        public bool Ignoring(IUser client, IUser target) { return true; }

        public void IgnoredStateChanged(IUser client, IUser target, bool ignored) { }

        public void InvalidLoginAttempt(IUser client) { }

        public void LoginGranted(IUser client) { }

        public void AdminLevelChanged(IUser client) { }

        public void InvalidRegistration(IUser client) { }

        public bool Registering(IUser client) { return true; }

        public void Registered(IUser client) { }

        public void Unregistered(IUser client) { }

        public void CaptchaSending(IUser client) { }

        public void CaptchaReply(IUser client, string reply) { }

        public bool VroomChanging(IUser client, ushort vroom) { return true; }

        public void VroomChanged(IUser client) { }

        public bool Flooding(IUser client, byte msg) { return false; }

        public void Flooded(IUser client) { }

        public bool ProxyDetected(IUser client) { return false; }

        public void Logout(IUser client) { }

        public void Idled(IUser client) { }

        public void Unidled(IUser client, uint seconds_away) { }

        public void BansAutoCleared() { }

        public void Dispose()
        {
            try { http.Dispose(); } catch { }
            try { renderGate.Dispose(); } catch { }
        }

        public void LinkError(ILinkError error) { }

        public void Linked() { }

        public void Unlinked() { }

        public void LeafJoined(ILeaf leaf) { }

        public void LeafParted(ILeaf leaf) { }

        public void LinkedAdminDisabled(ILeaf leaf, IUser client) { }

        public object Icon { get { return null; } }

        public object GUI { get { return null; } }
    }
}
