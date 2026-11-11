using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using iconnect;

namespace AudioLibraryExtension
{
    public class AudioLibrary : IExtension
    {
        private IHostApp Host { get; set; }
        private string configPath = "audios_config.json";
        private string soundsPath = "sounds";
        private AudioConfig config;
        private Dictionary<string, string> audioCache = new Dictionary<string, string>();
        private Dictionary<string, bool> downloadInProgress = new Dictionary<string, bool>();
        private object cacheLock = new object();

        private static readonly HttpClient httpClient = CreateHttpClient();
        private static string curlExecutable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "curl.exe" : "curl";
        private static string extensionDataPath;

        private static string ResolveCurlExecutable()
        {
            string[] candidates;
            if (!string.IsNullOrEmpty(extensionDataPath))
                candidates = new[] { Path.Combine(extensionDataPath, "curl.exe"), Path.Combine(AppContext.BaseDirectory, "curl.exe") };
            else
                candidates = new[] { Path.Combine(AppContext.BaseDirectory, "curl.exe") };

            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch { }
            }

            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "curl.exe" : "curl";
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler { UseProxy = false };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            return client;
        }

        private static byte[] DownloadBytes(string url)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Referrer = new Uri("https://www.myinstants.com/");
                    using (var response = httpClient.Send(request, HttpCompletionOption.ResponseContentRead))
                    {
                        response.EnsureSuccessStatusCode();
                        return response.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception)
            {
                return DownloadBytesWithCurl(url);
            }
        }

        private static byte[] DownloadBytesWithCurl(string url)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "sb0t_audio_" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = curlExecutable,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                psi.ArgumentList.Add("-sS");
                psi.ArgumentList.Add("-L");
                psi.ArgumentList.Add("--compressed");
                psi.ArgumentList.Add("--connect-timeout");
                psi.ArgumentList.Add("15");
                psi.ArgumentList.Add("-A");
                psi.ArgumentList.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("https://www.myinstants.com/");
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add(tmp);
                psi.ArgumentList.Add(url);

                using (Process p = Process.Start(psi))
                {
                    Task<string> errTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(60000))
                    {
                        p.Kill();
                        throw new Exception("curl agotó el tiempo (60s).");
                    }
                    string err = errTask.GetAwaiter().GetResult();
                    if (p.ExitCode != 0)
                        throw new Exception("curl falló: " + err.Trim());
                }

                if (File.Exists(tmp))
                {
                    byte[] data = File.ReadAllBytes(tmp);
                    if (data.Length == 0)
                        throw new Exception("El recurso descargado está vacío.");
                    return data;
                }
                throw new Exception("curl no generó el archivo.");
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch { }
            }
        }

        private static bool IsAudioBytes(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;

            if (data[0] == 'I' && data[1] == 'D' && data[2] == '3') return true;
            if (data[0] == 0xFF && (data[1] & 0xE0) == 0xE0) return true;
            if (data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S') return true;
            if (data[0] == 'f' && data[1] == 'L' && data[2] == 'a' && data[3] == 'C') return true;
            if (data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3) return true;
            if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E') return true;
            if (data[4] == 'f' && data[5] == 't' && data[6] == 'y' && data[7] == 'p') return true;
            if (data[4] == 'w' && data[5] == 'e' && data[6] == 'b' && data[7] == 'm') return true;

            string head = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 16)).ToLowerInvariant();
            if (head.Contains("<html") || head.Contains("<!doctype") || head.Contains("<body"))
                return false;

            return false;
        }

        private static byte[] DownloadAudioBytes(string url)
        {
            byte[] data = null;

            try
            {
                data = DownloadBytesWithCurl(url);
            }
            catch (Exception) { data = null; }

            if (data == null || !IsAudioBytes(data))
            {
                try
                {
                    data = DownloadBytes(url);
                }
                catch (Exception) { data = null; }
            }

            if (data != null && IsAudioBytes(data))
                return data;

            throw new Exception("El archivo descargado no es audio válido (posible bloqueo de Cloudflare a la IP del server). Descarga el mp3 y colócalo en la carpeta sounds del server.");
        }

        private static string DownloadPageText(string url)
        {
            try
            {
                return Encoding.UTF8.GetString(DownloadBytesWithCurl(url));
            }
            catch (Exception)
            {
                return Encoding.UTF8.GetString(DownloadBytes(url));
            }
        }

        public AudioLibrary(IHostApp host)
        {
            this.Host = host;
            this.configPath = Path.Combine(this.Host.DataPath, "audios_config.json");
            this.soundsPath = Path.Combine(this.Host.DataPath, "sounds");
            extensionDataPath = this.Host.DataPath;
            curlExecutable = ResolveCurlExecutable();
            Directory.CreateDirectory(this.soundsPath);
            LoadConfig();
            SyncFromDisk();
            this.Host.WriteLog($"Audio Library Extension loaded. {config.Audios.Count} audios in list. Sounds folder: {this.soundsPath}");
        }

        public void ServerStarted()
        {
            this.Host.WriteLog("Audio Library Extension: Server started");
        }

        private bool IsAudioCached(string url)
        {
            lock (cacheLock)
            {
                return audioCache.ContainsKey(url);
            }
        }

        private int GetCacheSizeKB()
        {
            lock (cacheLock)
            {
                int totalSize = 0;
                foreach (var kvp in audioCache)
                {
                    totalSize += kvp.Value.Length;
                }
                return totalSize / 1024;
            }
        }

        private string GetAudioKey(AudioEntry audio)
        {
            if (!string.IsNullOrEmpty(audio.FilePath))
                return audio.FilePath;
            return audio.Url;
        }

        private string LoadAudioBase64(AudioEntry audio)
        {
            if (!string.IsNullOrEmpty(audio.FilePath) && File.Exists(audio.FilePath))
            {
                byte[] data = File.ReadAllBytes(audio.FilePath);
                return Convert.ToBase64String(data);
            }
            return DownloadAndConvertToBase64(audio.Url);
        }

        private bool IsAudioFile(string path)
        {
            string[] audioExtensions = { ".mp3", ".wav", ".ogg", ".m4a", ".mp4", ".aac", ".webm", ".flac" };
            return audioExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        private void SyncFromDisk()
        {
            string[] files = Directory.GetFiles(this.soundsPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                if (!IsAudioFile(file))
                    continue;

                bool known = config.Audios.Any(a => a.FilePath != null && a.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase));
                if (!known)
                {
                    config.Audios.Add(new AudioEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Url = null,
                        FilePath = file,
                        Owner = "System",
                        IsPublic = true,
                        AddedBy = "System",
                        AddedDate = DateTime.Now
                    });
                }
            }

            config.Audios.RemoveAll(a => a.FilePath != null && !File.Exists(a.FilePath));
            SaveConfig();
        }

        private void CacheAudioInBackground(AudioEntry audio)
        {
            string key = GetAudioKey(audio);
            if (IsAudioCached(key) || downloadInProgress.ContainsKey(key))
                return;

            try
            {
                downloadInProgress[key] = true;
                string base64 = LoadAudioBase64(audio);

                if (!string.IsNullOrEmpty(base64))
                {
                    lock (cacheLock)
                    {
                        audioCache[key] = base64;
                    }
                }
            }
            catch (Exception ex)
            {
                this.Host.WriteLog($"Error cargando audio '{audio.Name}': {ex.Message}");
            }
            finally
            {
                downloadInProgress.Remove(key);
            }
        }

        private string DownloadAndConvertToBase64(string url)
        {
            try
            {
                byte[] audioData = DownloadAudioBytes(url);
                return Convert.ToBase64String(audioData);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error descargando audio: {ex.Message}");
            }
        }

        public void Dispose()
        {
            lock (cacheLock)
            {
                audioCache.Clear();
            }
            downloadInProgress.Clear();
            this.Host.WriteLog("Audio Library Extension: Disposed");
        }

        private void LoadConfig()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<AudioConfig>(json);
                    this.Host.WriteLog($"Config loaded from {configPath}");
                }
                catch (Exception ex)
                {
                    this.Host.WriteLog($"Error loading config: {ex.Message}");
                    config = new AudioConfig();
                }
            }
            else
            {
                this.Host.WriteLog($"Config file not found, creating default...");
                config = new AudioConfig();
                SaveConfig();
                this.Host.WriteLog($"Created default config. Audios come from the sounds folder.");
            }
        }

        private void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                this.Host.WriteLog($"Error saving config: {ex.Message}");
            }
        }

        public void Command(IUser client, string cmd, IUser target, string args)
        {
            this.Host.WriteLog($"Audio Library Command received: /{cmd} from {client.Name}");

            try
            {
                SyncFromDisk();
                string command = cmd.ToLower();

                if (command.StartsWith("play "))
                {
                    string url = cmd.Substring(5).Trim();
                    HandlePlayFromUrl(client, url);
                    return;
                }

                if (command == "audios" || command == "audiolist")
                {
                    HandleListAudios(client);
                    return;
                }

                if (command.StartsWith("audios "))
                {
                    return;
                }

                if (command.StartsWith("audio"))
                {
                    if (cmd.Length > 5)
                    {
                        if (cmd[5] == ' ')
                        {
                            string idStr = cmd.Substring(6).Trim();
                            HandlePlayAudioById(client, idStr);
                        }
                        else
                        {
                            HandleListAudios(client);
                        }
                    }
                    else
                    {
                        HandleListAudios(client);
                    }
                    return;
                }

                if (command.StartsWith("pmaudio "))
                {
                    string parameters = cmd.Substring(8).Trim();
                    HandlePmAudioSubstring(client, parameters);
                    return;
                }

                if (command.StartsWith("addaudio "))
                {
                    string parameters = cmd.Substring(9).Trim();
                    HandleAddAudioSubstring(client, parameters);
                    return;
                }

                if (command.StartsWith("removeaudio "))
                {
                    string idStr = cmd.Substring(12).Trim();
                    HandleRemoveAudioSubstring(client, idStr);
                    return;
                }

                if (command == "audiocache")
                {
                    HandleCacheInfo(client);
                    return;
                }
                else if (command == "precache")
                {
                    HandlePreCache(client);
                    return;
                }
                else if (command == "debug")
                {
                    HandleDebug(client);
                    return;
                }
                else if (command == "testlibaudio")
                {
                    HandleTestLibraryAudio(client);
                    return;
                }
                else if (command == "help")
                {
                    Help(client);
                    return;
                }
                else if (command == "clearcache")
                {
                    ClearCache();
                    client.Print("✅ Cache limpiado.");
                    return;
                }
            }
            catch (Exception ex)
            {
                client.Print($"❌ Error: {ex.Message}");
            }
        }

        private void HandlePlayFromUrl(IUser client, string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    client.Print("❌ URL requerida. Uso: /play <url>");
                    return;
                }

                client.Print("🔄 Procesando URL...");

                string audioUrl = null;

                if (IsValidAudioUrl(url))
                {
                    audioUrl = url;
                    client.Print($"✅ URL es un archivo de audio directo.");
                }
                else if (url.Contains("myinstants.com"))
                {
                    audioUrl = ExtractAudioUrlFromMyInstants(url);
                    if (audioUrl != null)
                    {
                        client.Print($"✅ Extraído audio de página MyInstants: {audioUrl}");
                    }
                    else
                    {
                        client.Print("❌ No se pudo extraer un enlace de audio de la página MyInstants.");
                        return;
                    }
                }
                else
                {
                    client.Print("❌ URL no válida. Debe ser un enlace directo a audio (.mp3, .wav, etc.) o una página de MyInstants.");
                    return;
                }

                client.Print($"🔄 Descargando audio desde: {audioUrl}");

                string base64Audio = DownloadAndConvertToBase64(audioUrl);

                if (!string.IsNullOrEmpty(base64Audio))
                {
                    lock (cacheLock)
                    {
                        audioCache[audioUrl] = base64Audio;
                    }

                    SendAudioToAllBase64(client.Name, client.Vroom, base64Audio);
                    client.Print("✅ Audio desde URL enviado a la sala.");
                }
                else
                {
                    client.Print("❌ No se pudo cargar el audio desde la URL.");
                }
            }
            catch (Exception ex)
            {
                client.Print($"❌ Error: {ex.Message}");
            }
        }

        private string ExtractAudioUrlFromMyInstants(string url)
        {
            if (!url.Contains("myinstants.com"))
                return null;

            try
            {
                this.Host.WriteLog($"Extrayendo audio de MyInstants: {url}");
                string html = DownloadPageText(url);

                    var preloadRegex = new Regex(@"preloadAudioUrl\s*=\s*['""]([^'""]+)['""]");
                    var preloadMatch = preloadRegex.Match(html);

                    if (preloadMatch.Success)
                    {
                        string audioPath = preloadMatch.Groups[1].Value.Trim();
                        this.Host.WriteLog($"Encontrado preloadAudioUrl: {audioPath}");

                        if (audioPath.StartsWith("/"))
                        {
                            if (audioPath.StartsWith("//"))
                            {
                                return "https:" + audioPath;
                            }
                            return "https://www.myinstants.com" + audioPath;
                        }
                        else if (audioPath.StartsWith("media/"))
                        {
                            return "https://www.myinstants.com/" + audioPath;
                        }

                        return audioPath;
                    }

                    var audioRegex = new Regex(@"<audio[^>]+src=['""]([^'""]+)['""]");
                    var audioMatch = audioRegex.Match(html);

                    if (audioMatch.Success)
                    {
                        string audioSrc = audioMatch.Groups[1].Value.Trim();
                        this.Host.WriteLog($"Encontrado elemento audio con src: {audioSrc}");
                        return MakeAbsoluteUrl(audioSrc, url);
                    }

                    var sourceRegex = new Regex(@"<source[^>]+src=['""]([^'""]+)['""][^>]+type=['""]audio/");
                    var sourceMatch = sourceRegex.Match(html);

                    if (sourceMatch.Success)
                    {
                        string audioSrc = sourceMatch.Groups[1].Value.Trim();
                        this.Host.WriteLog($"Encontrado elemento source con src: {audioSrc}");
                        return MakeAbsoluteUrl(audioSrc, url);
                    }

                    var mp3Regex = new Regex(@"href=['""]([^'""]*\.mp3)['""]");
                    var mp3Matches = mp3Regex.Matches(html);

                    foreach (Match match in mp3Matches)
                    {
                        string mp3Url = match.Groups[1].Value.Trim();
                        if (mp3Url.Contains("media/sounds/"))
                        {
                            this.Host.WriteLog($"Encontrado enlace MP3: {mp3Url}");
                            return MakeAbsoluteUrl(mp3Url, url);
                        }
                    }

                    if (mp3Matches.Count > 0)
                    {
                        string mp3Url = mp3Matches[0].Groups[1].Value.Trim();
                        this.Host.WriteLog($"Encontrado enlace MP3: {mp3Url}");
                        return MakeAbsoluteUrl(mp3Url, url);
                    }

                    this.Host.WriteLog($"No se encontró audio en la página MyInstants: {url}");
                    return null;
            }
            catch (Exception ex)
            {
                this.Host.WriteLog($"Error extrayendo URL de audio de MyInstants: {ex.Message}");
                return null;
            }
        }

        private string MakeAbsoluteUrl(string relativeUrl, string baseUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return null;

            if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://"))
                return relativeUrl;

            if (relativeUrl.StartsWith("//"))
                return "https:" + relativeUrl;

            if (relativeUrl.StartsWith("/"))
            {
                Uri baseUri;
                try
                {
                    baseUri = new Uri(baseUrl);
                }
                catch
                {
                    baseUri = new Uri("https://www.myinstants.com");
                }
                return $"{baseUri.Scheme}://{baseUri.Host}{relativeUrl}";
            }

            Uri uri = new Uri(baseUrl);
            string basePath = uri.AbsoluteUri.Substring(0, uri.AbsoluteUri.LastIndexOf('/') + 1);
            return basePath + relativeUrl;
        }

        private bool IsValidAudioUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            string[] audioExtensions = { ".mp3", ".wav", ".ogg", ".m4a", ".mp4", ".aac", ".webm", ".flac" };
            string extension = Path.GetExtension(url).ToLower();

            if (audioExtensions.Contains(extension))
                return true;

            if (url.Contains("media/sounds/"))
                return true;

            if (url.StartsWith("data:audio/"))
                return true;

            if (url.Contains(".mp3?") || url.Contains(".wav?") || url.Contains(".ogg?"))
                return true;

            return false;
        }

        private void HandlePlayAudioById(IUser client, string audioIdStr)
        {
            try
            {
                if (config.Audios.Count == 0)
                {
                    client.Print("❌ No hay audios disponibles.");
                    return;
                }

                if (!int.TryParse(audioIdStr, out int userFriendlyId) || userFriendlyId < 1 || userFriendlyId > config.Audios.Count)
                {
                    client.Print($"❌ ID inválido. Usa /audios para ver la lista (IDs válidos: 1-{config.Audios.Count}).");
                    return;
                }

                int actualId = userFriendlyId - 1;
                var audio = config.Audios[actualId];
                string key = GetAudioKey(audio);

                if (!IsAudioCached(key))
                {
                    string base64Audio = LoadAudioBase64(audio);
                    if (!string.IsNullOrEmpty(base64Audio))
                    {
                        lock (cacheLock)
                        {
                            audioCache[key] = base64Audio;
                        }
                    }
                }

                string cachedAudio;
                lock (cacheLock)
                {
                    cachedAudio = audioCache[key];
                }

                if (!string.IsNullOrEmpty(cachedAudio))
                {
                    SendAudioToAllBase64(client.Name, client.Vroom, cachedAudio);
                    client.Print($"✅ Audio '{audio.Name}' enviado a la sala.");
                }
                else
                {
                    client.Print("❌ Error al cargar el audio.");
                }
            }
            catch (Exception ex)
            {
                client.Print($"❌ Error: {ex.Message}");
            }
        }

        private void HandlePmAudioSubstring(IUser client, string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                client.Print("❌ Uso: /pmaudio <id> <usuario>");
                return;
            }

            string[] parts = parameters.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || !int.TryParse(parts[0], out int id) || id < 1 || id > config.Audios.Count)
            {
                client.Print($"❌ ID inválido. Uso: /pmaudio <id> <usuario> (IDs: 1-{config.Audios.Count})");
                return;
            }

            var audio = config.Audios[id - 1];
            string targetUsername = parts[1];

            if (!IsAudioCached(GetAudioKey(audio)))
            {
                client.Print($"🔄 Descargando '{audio.Name}'...");
                DownloadAndSendPmAudio(client, targetUsername, audio);
            }
            else
            {
                try
                {
                    SendPmAudioToUser(client, targetUsername, audio);
                }
                catch (Exception ex)
                {
                    client.Print($"❌ Error: {ex.Message}");
                }
            }
        }

        private void HandleAddAudioSubstring(IUser client, string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                client.Print("❌ Uso: /addaudio <url> <nombre>");
                return;
            }

            string[] parts = parameters.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                client.Print("❌ Formato incorrecto. Uso: /addaudio <url> <nombre>");
                return;
            }

            string url = parts[0].Trim();
            string name = parts[1].Trim();

            client.Print($"🔄 Procesando URL: {url}");

            string audioUrl = null;

            if (IsValidAudioUrl(url))
            {
                audioUrl = url;
                client.Print($"✅ URL es un archivo de audio directo.");
            }
            else if (url.Contains("myinstants.com"))
            {
                audioUrl = ExtractAudioUrlFromMyInstants(url);
                if (audioUrl != null)
                {
                    client.Print($"✅ Extraído audio de página MyInstants: {audioUrl}");
                }
                else
                {
                    client.Print("❌ No se pudo extraer un enlace de audio de la página MyInstants.");
                    return;
                }
            }
            else
            {
                client.Print("❌ URL no válida. Debe ser un enlace directo a audio (.mp3, .wav, etc.) o una página de MyInstants.");
                return;
            }

            if (config.Audios.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                client.Print("❌ Ya existe un audio con ese nombre. Usa un nombre diferente.");
                return;
            }

            if (config.Audios.Any(a => !string.IsNullOrEmpty(a.Url) && a.Url.Equals(audioUrl, StringComparison.OrdinalIgnoreCase)))
            {
                client.Print("❌ Ya existe un audio con esa URL.");
                return;
            }

            client.Print($"🔄 Probando descarga del audio...");
            try
            {
                DownloadAudioBytes(audioUrl);
            }
            catch (Exception ex)
            {
                client.Print($"❌ No se pudo acceder al audio: {ex.Message}");
                return;
            }

            config.Audios.Add(new AudioEntry
            {
                Name = name,
                Url = audioUrl,
                Owner = client.Name,
                IsPublic = true,
                AddedBy = client.Name,
                AddedDate = DateTime.Now
            });

            SaveConfig();

            Task.Run(() =>
            {
                try
                {
                    CacheAudioInBackground(config.Audios.Last());
                    this.Host.WriteLog($"Audio '{name}' pre-cached.");
                }
                catch (Exception ex)
                {
                    this.Host.WriteLog($"Audio '{name}' added but could not be pre-cached: {ex.Message}");
                }
            });

            client.Print($"✅ Audio '{name}' agregado exitosamente. URL: {audioUrl}");
        }

        private void HandleRemoveAudioSubstring(IUser client, string idStr)
        {
            if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out int id) || id < 1 || id > config.Audios.Count)
            {
                client.Print($"❌ ID inválido. Usa /audios para ver IDs válidos (1-{config.Audios.Count}).");
                return;
            }

            int actualId = id - 1;
            var audio = config.Audios[actualId];

            if (audio.Owner != client.Name && client.Level < ILevel.Moderator)
            {
                client.Print("❌ Solo el dueño puede eliminar este audio.");
                return;
            }

            string key = GetAudioKey(audio);

            lock (cacheLock)
            {
                if (audioCache.ContainsKey(key))
                {
                    audioCache.Remove(key);
                }
            }

            if (!string.IsNullOrEmpty(audio.FilePath) && File.Exists(audio.FilePath))
            {
                try
                {
                    File.Delete(audio.FilePath);
                }
                catch (Exception ex)
                {
                    this.Host.WriteLog($"Error borrando archivo de audio: {ex.Message}");
                }
            }

            config.Audios.RemoveAt(actualId);
            SaveConfig();
            client.Print($"✅ Audio '{audio.Name}' eliminado.");
        }

        private void HandleListAudios(IUser client)
        {
            if (config.Audios.Count == 0)
            {
                client.Print("📭 No hay audios disponibles.");
                return;
            }

            client.Print("🎵 Lista de audios disponibles:");
            for (int i = 0; i < config.Audios.Count; i++)
            {
                var audio = config.Audios[i];
                string status = IsAudioCached(GetAudioKey(audio)) ? "✅" : "⬇️";
                string ownerBadge = audio.Owner == client.Name ? "👑 " : "";
                client.Print($"{i + 1}. {status} {ownerBadge}{audio.Name} - Dueño: {audio.Owner}");
            }
        }

        private void DownloadAndSendPmAudio(IUser client, string targetIdentifier, AudioEntry audio)
        {
            try
            {
                string base64Audio = LoadAudioBase64(audio);
                string key = GetAudioKey(audio);

                if (!string.IsNullOrEmpty(base64Audio))
                {
                    lock (cacheLock)
                    {
                        audioCache[key] = base64Audio;
                    }

                    SendPmAudioToUserBase64(client, targetIdentifier, audio, base64Audio);
                }
            }
            catch (Exception ex)
            {
                client.Print($"❌ Error descargando '{audio.Name}': {ex.Message}");
            }
        }

        private void SendPmAudioToUser(IUser sender, string targetIdentifier, AudioEntry audio)
        {
            try
            {
                string base64Audio;
                lock (cacheLock)
                {
                    if (!audioCache.TryGetValue(GetAudioKey(audio), out base64Audio))
                        throw new Exception("Audio no encontrado en caché.");
                }

                SendPmAudioToUserBase64(sender, targetIdentifier, audio, base64Audio);
            }
            catch (Exception ex)
            {
                sender.Print($"❌ Error: {ex.Message}");
            }
        }

        private void HandleCacheInfo(IUser client)
        {
            lock (cacheLock)
            {
                int cachedCount = audioCache.Count;
                int totalSize = GetCacheSizeKB();
                client.Print($"📊 Cache: {cachedCount} audios, {totalSize} KB");
            }
        }

        private void HandlePreCache(IUser client)
        {
            client.Print("🔄 Precargando todos los audios...");

            Task.Run(() =>
            {
                try
                {
                    int cachedCount = 0;
                    foreach (var audio in config.Audios)
                    {
                        if (!IsAudioCached(GetAudioKey(audio)))
                        {
                            CacheAudioInBackground(audio);
                            cachedCount++;
                        }
                        Thread.Sleep(200);
                    }
                    client.Print($"✅ Precarga completada. {cachedCount} audios cargados en caché.");
                }
                catch (Exception ex)
                {
                    client.Print($"❌ Error en precarga: {ex.Message}");
                }
            });
        }

        private void HandleDebug(IUser client)
        {
            client.Print("=== 🐛 Debug Information ===");
            client.Print($"• Extension loaded: {config != null}");
            client.Print($"• Audio count: {config?.Audios.Count ?? 0}");
            client.Print($"• Cache size: {GetCacheSizeKB()} KB");
            client.Print($"• Cached audios: {audioCache.Count}");
        }

        private void HandleTestLibraryAudio(IUser client)
        {
            try
            {
                if (config.Audios.Count == 0)
                {
                    client.Print("❌ No hay audios en la biblioteca.");
                    return;
                }

                var audio = config.Audios[0];
                client.Print($"🎵 Probando audio: {audio.Name}");

                if (!IsAudioCached(GetAudioKey(audio)))
                {
                    client.Print("🔄 Cargando para prueba...");
                    string base64 = LoadAudioBase64(audio);

                    if (!string.IsNullOrEmpty(base64))
                    {
                        SendAudioToAllBase64(client.Name, client.Vroom, base64);
                        client.Print($"✅ Audio de prueba '{audio.Name}' enviado.");
                    }
                    else
                    {
                        client.Print("❌ No se pudo descargar el audio.");
                    }
                }
                else
                {
                    string base64;
                    lock (cacheLock)
                    {
                        base64 = audioCache[GetAudioKey(audio)];
                    }
                    SendAudioToAllBase64(client.Name, client.Vroom, base64);
                    client.Print($"✅ Audio de prueba '{audio.Name}' enviado desde caché.");
                }
            }
            catch (Exception ex)
            {
                client.Print($"❌ Error en prueba: {ex.Message}");
            }
        }

        private void SendAudioToAllBase64(string senderName, ushort senderVroom, string base64Audio)
        {
            try
            {
                int sentCount = 0;

                Host.Users.All(user =>
                {
                    if (!user.Connected || user.Quarantined)
                        return;
                    if (user.Vroom != senderVroom)
                        return;

                    bool linked = user.Link != null && user.Link.IsLinked;
                    bool inbizier = user.IsInbizierWeb || user.IsInbizierMobile;

                    if (!linked && !inbizier)
                        return;

                    user.Audio(senderName, base64Audio);
                    sentCount++;
                });

                if (sentCount == 0)
                {
                    throw new Exception("No se encontraron usuarios compatibles para enviar el audio.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error enviando audio: {ex.Message}");
            }
        }

        private void SendPmAudioToUserBase64(IUser sender, string targetIdentifier, AudioEntry audio, string base64Audio)
        {
            try
            {
                IUser targetUser = null;

                if (ushort.TryParse(targetIdentifier, out ushort userId))
                {
                    targetUser = Host.Users.Find(u => u.Connected && u.ID == userId);
                }
                else
                {
                    targetUser = Host.Users.Find(u => u.Connected && u.Name.Equals(targetIdentifier, StringComparison.OrdinalIgnoreCase));
                }

                if (targetUser == null)
                {
                    sender.Print($"❌ Usuario '{targetIdentifier}' no encontrado.");
                    return;
                }

                var targetName = targetUser.Name;
                if (!(targetUser.IsInbizierWeb || targetUser.IsInbizierMobile))
                {
                    sender.Print($"❌ {targetName} no puede recibir audios.");
                    return;
                }

                if (targetUser.IgnoreList != null && targetUser.IgnoreList.Contains(sender.Name))
                {
                    sender.Print($"❌ {targetName} te está ignorando.");
                    return;
                }

                var pmAudioMethod = targetUser.GetType().GetMethod("PmAudio", new[] { typeof(string), typeof(string) });
                pmAudioMethod?.Invoke(targetUser, new object[] { sender.Name, base64Audio });

                sender.Print($"✅ Audio '{audio.Name}' enviado en privado a {targetName}.");
            }
            catch (Exception ex)
            {
                sender.Print($"❌ Error: {ex.Message}");
            }
        }

        private void ClearCache()
        {
            lock (cacheLock)
            {
                audioCache.Clear();
            }
            downloadInProgress.Clear();
        }

        #region Helper Classes
        private class AudioConfig
        {
            public List<AudioEntry> Audios { get; set; } = new List<AudioEntry>();
        }

        private class AudioEntry
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string FilePath { get; set; }
            public string Owner { get; set; }
            public bool IsPublic { get; set; } = true;
            public string AddedBy { get; set; }
            public DateTime AddedDate { get; set; }
        }
        #endregion

        #region IExtension Implementation
        public void Load() { }
        public void CycleTick() { }
        public void UnhandledProtocol(IUser client, bool custom, byte msg, byte[] packet) { }
        public bool Joining(IUser client) => true;
        public void Joined(IUser client) { }
        public void Rejected(IUser client, RejectedMsg msg) { }
        public void Parting(IUser client) { }
        public void Parted(IUser client) { }
        public bool AvatarReceived(IUser client) => true;
        public bool PersonalMessageReceived(IUser client, string text) => true;
        public void TextReceived(IUser client, string text) { }
        public string TextSending(IUser client, string text) => text;
        public void TextSent(IUser client, string text) { }
        public void EmoteReceived(IUser client, string text) { }
        public string EmoteSending(IUser client, string text) => text;
        public void EmoteSent(IUser client, string text) { }
        public void PrivateSending(IUser client, IUser target, IPrivateMsg msg) { }
        public void PrivateSent(IUser client, IUser target) { }
        public void BotPrivateSent(IUser client, string text) { }
        public bool Nick(IUser client, string name) => true;

        public void Help(IUser client)
        {
            client.Print("=== 🎵 Audio Library Extension ===");
            client.Print("/audios - Muestra la lista de audios disponibles");
            client.Print("/audio - Muestra la lista de audios (atajo)");
            client.Print("/audio <id> - Reproduce un audio en la sala");
            client.Print("/play <url> - Reproduce un audio directamente desde una URL");
            client.Print("/pmaudio <id> <usuario> - Envía audio en privado");
            client.Print("/addaudio <url> <nombre> - Añade audio desde URL o MyInstants");
            client.Print("/removeaudio <id> - Elimina audio (solo dueño)");
            client.Print("/audiocache - Info del caché");
            client.Print("/precache - Precarga todos los audios");
            client.Print("/clearcache - Limpia la cache");
            client.Print("/testlibaudio - Prueba el primer audio");
            client.Print("/debug - Información de depuración");
            client.Print("/help - Muestra esta ayuda");
            client.Print("💾 Los mp3 de la carpeta 'sounds' del plugin se cargan solos (/removeaudio los borra del disco)");
        }

        public void FileReceived(IUser client, string filename, string title, MimeType type) { }
        public bool Ignoring(IUser client, IUser target) => true;
        public void IgnoredStateChanged(IUser client, IUser target, bool ignored) { }
        public void InvalidLoginAttempt(IUser client) { }
        public void LoginGranted(IUser client) { }
        public void AdminLevelChanged(IUser client) { }
        public void InvalidRegistration(IUser client) { }
        public bool Registering(IUser client) => true;
        public void Registered(IUser client) { }
        public void Unregistered(IUser client) { }
        public void CaptchaSending(IUser client) { }
        public void CaptchaReply(IUser client, string reply) { }
        public bool VroomChanging(IUser client, ushort vroom) => true;
        public void VroomChanged(IUser client) { }
        public bool Flooding(IUser client, byte msg) => false;
        public void Flooded(IUser client) { }
        public bool ProxyDetected(IUser client) => false;
        public void Logout(IUser client) { }
        public void Idled(IUser client) { }
        public void Unidled(IUser client, uint seconds_away) { }
        public void BansAutoCleared() { }
        public void LinkError(ILinkError error) { }
        public void Linked() { }
        public void Unlinked() { }
        public void LeafJoined(ILeaf leaf) { }
        public void LeafParted(ILeaf leaf) { }
        public void LinkedAdminDisabled(ILeaf leaf, IUser client) { }

        // In sb0t Pyxis these properties are typed as "object" (no WPF).
        public object Icon { get { return null; } }
        public object GUI { get { return null; } }
        #endregion
    }
}