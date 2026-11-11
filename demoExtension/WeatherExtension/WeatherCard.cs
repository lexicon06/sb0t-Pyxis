using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using IOPath = System.IO.Path;

namespace WeatherExtension
{
    // ------------------------------------------------------------------
    // Modelo de respuesta de WeatherAPI (solo lo que usamos)
    // ------------------------------------------------------------------
    internal sealed class WeatherApiResponse
    {
        [JsonPropertyName("location")] public WeatherLocation Location { get; set; }
        [JsonPropertyName("current")] public WeatherCurrent Current { get; set; }
    }

    internal sealed class WeatherLocation
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("region")] public string Region { get; set; }
        [JsonPropertyName("country")] public string Country { get; set; }
        [JsonPropertyName("localtime")] public string LocalTime { get; set; }
    }

    internal sealed class WeatherCurrent
    {
        [JsonPropertyName("temp_c")] public double TempC { get; set; }
        [JsonPropertyName("feelslike_c")] public double FeelsLikeC { get; set; }
        [JsonPropertyName("humidity")] public double Humidity { get; set; }
        [JsonPropertyName("wind_kph")] public double WindKph { get; set; }
        [JsonPropertyName("condition")] public WeatherCondition Condition { get; set; }
    }

    internal sealed class WeatherCondition
    {
        [JsonPropertyName("text")] public string Text { get; set; }
        [JsonPropertyName("code")] public int? Code { get; set; }
        [JsonPropertyName("icon")] public string Icon { get; set; }
    }

    /// <summary>
    /// Port de weather-cards/main.py a C#/.NET: consulta WeatherAPI, busca la foto
    /// de la ciudad y compone la tarjeta 800x400. La salida final es JPEG (opaca).
    /// </summary>
    internal static class WeatherCard
    {
        private const int W = 800;
        private const int H = 400;

        private static readonly JsonSerializerOptions JsonOpts =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private static readonly Dictionary<string, string> Creditos = new Dictionary<string, string>
        {
            { "wikipedia", "Foto: Wikipedia" },
            { "bing", "Foto: Bing" },
        };

        // Archivos que NO son postal: banderas, escudos, mapas, logos...
        private static readonly string[] ArchivosMalos =
        {
            "bandera", "flag_", "_flag", "flag.", "flag-", "escudo", "coat_of_arms",
            "coat-of-arms", "coatofarms", "wappen", "blason", "blazon", "logo",
            "svg", "mapa", "map_", "_map.", "seal_", "_seal", "emblema", "isotipo",
            "moneda", "qr_", "icono", "icon_", "banner", "patron",
        };

        // ------------------------------------------------------------------
        // Fuentes embebidas (Inter)
        // ------------------------------------------------------------------
        private static readonly object FontLock = new object();
        private static bool fontsReady;
        private static FontFamily interFamily;

        private static Stream OpenResource(string fileName)
        {
            var asm = typeof(WeatherCard).Assembly;
            string full = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            return full == null ? null : asm.GetManifestResourceStream(full);
        }

        private static void EnsureFonts()
        {
            if (fontsReady)
                return;

            lock (FontLock)
            {
                if (fontsReady)
                    return;

                try
                {
                    FontCollection col = new FontCollection();
                    using (Stream s = OpenResource("Inter-Bold.ttf")) if (s != null) col.Add(s);
                    using (Stream s = OpenResource("Inter-Regular.ttf")) if (s != null) col.Add(s);

                    if (col.TryGet("Inter", out FontFamily fam))
                        interFamily = fam;
                    else
                        interFamily = SystemFonts.Families.First();
                }
                catch
                {
                    interFamily = SystemFonts.Families.First();
                }

                fontsReady = true;
            }
        }

        private static Font GetFont(float size, FontStyle style)
        {
            EnsureFonts();
            return interFamily.CreateFont(size, style);
        }

        // ------------------------------------------------------------------
        // HTTP helpers
        // ------------------------------------------------------------------
        private static string AbsUrl(string u)
        {
            if (string.IsNullOrEmpty(u)) return null;
            if (u.StartsWith("//")) return "https:" + u;
            if (u.StartsWith("http://") || u.StartsWith("https://")) return u;
            return "https://" + u.TrimStart('/');
        }

        private static byte[] Get(HttpClient http, string url)
        {
            try
            {
                HttpResponseMessage r = http.GetAsync(url).GetAwaiter().GetResult();
                if (r.IsSuccessStatusCode)
                {
                    byte[] b = r.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    return b.Length > 0 ? b : null;
                }
            }
            catch { }
            return null;
        }

        private static JsonDocument GetJson(HttpClient http, string url)
        {
            try
            {
                string s = http.GetStringAsync(url).GetAwaiter().GetResult();
                return JsonDocument.Parse(s);
            }
            catch { return null; }
        }

        private static string Md5(string s)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------
        // Clima
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Clima (Open-Meteo: gratis, sin API key, geocoding UTF-8)
        // ------------------------------------------------------------------
        private static string GetString(JsonElement e, string prop)
            => e.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static double GetDouble(JsonElement e, string prop)
            => e.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

        private static string FirstNonNull(params string[] values)
        {
            foreach (string v in values)
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            return null;
        }

        private static double ParseD(string s)
            => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0;

        private static string CleanCity(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            s = s.Trim();
            foreach (string suf in new[] { " ciudad", " city", " town" })
                if (s.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - suf.Length).Trim();
                    break;
                }
            return s;
        }

        // Nominatim (OpenStreetMap): gratis, UTF-8, buena relevancia de ciudades.
        // Su politica pide <= 1 request/segundo -> lo respetamos.
        private static readonly object NominatimLock = new object();
        private static DateTime lastNominatim = DateTime.MinValue;

        private static JsonDocument Nominatim(HttpClient http, string query)
        {
            lock (NominatimLock)
            {
                double since = (DateTime.UtcNow - lastNominatim).TotalMilliseconds;
                if (since < 1100 && since > 0)
                    Thread.Sleep((int)(1100 - since));
                lastNominatim = DateTime.UtcNow;
            }

            return GetJson(http, "https://nominatim.openstreetmap.org/search?q=" +
                Uri.EscapeDataString(query) + "&format=jsonv2&limit=5&addressdetails=1");
        }

        // WMO weather code -> codigo de icono de weather CDN (day/night)
        private static int WmoIcono(int code)
        {
            switch (code)
            {
                case 0: case 1: return 1000;   // despejado
                case 2: return 1003;           // parcialmente nublado
                case 3: return 1006;           // nublado
                case 45: return 1135;          // niebla
                case 48: return 1147;          // niebla helada
                case 51: case 53: return 1153; // llovizna
                case 55: return 1150;
                case 56: return 1168;          // llovizna helada
                case 57: return 1171;
                case 61: return 1183;          // lluvia ligera
                case 63: return 1189;          // lluvia moderada
                case 65: return 1195;          // lluvia intensa
                case 66: return 1198;          // lluvia helada
                case 67: return 1201;
                case 71: return 1213;          // nieve ligera
                case 73: return 1219;          // nieve moderada
                case 75: return 1225;          // nieve intensa
                case 77: return 1237;          // granos de hielo
                case 80: return 1240;          // chubascos
                case 81: return 1243;
                case 82: return 1246;
                case 85: return 1255;          // chubascos de nieve
                case 86: return 1258;
                case 95: return 1273;          // tormenta
                case 96: case 99: return 1279; // tormenta con granizo
                default: return 1003;
            }
        }

        public static WeatherApiResponse Fetch(HttpClient http, string apiKey, string query, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(query))
            {
                error = "Escribe una ciudad.";
                return null;
            }

            // 1) Geocoding (Nominatim): ciudad verificada, con Unicode (acentos, enie, etc.)
            string name = null, region = null, country = null;
            double lat = 0, lon = 0;

            using (JsonDocument geo = Nominatim(http, query))
            {
                if (geo == null)
                {
                    error = "No se pudo contactar el buscador de ciudades.";
                    return null;
                }
                if (geo.RootElement.ValueKind != JsonValueKind.Array || geo.RootElement.GetArrayLength() == 0)
                {
                    error = "Ciudad no encontrada.";
                    return null;
                }

                JsonElement r0 = geo.RootElement[0];

                // Elegir el resultado mas importante (lugar poblado grande):
                // mayor importance, y a igualdad menor place_rank (admin > ciudad > suburbio).
                double bestImp = -1;
                int bestRank = int.MaxValue;
                foreach (JsonElement it in geo.RootElement.EnumerateArray())
                {
                    double imp = GetDouble(it, "importance");
                    int rank = (int)GetDouble(it, "place_rank");
                    if (imp > bestImp || (imp == bestImp && rank < bestRank))
                    {
                        r0 = it;
                        bestImp = imp;
                        bestRank = rank;
                    }
                }

                lat = ParseD(GetString(r0, "lat"));
                lon = ParseD(GetString(r0, "lon"));

                if (r0.TryGetProperty("address", out JsonElement addr))
                {
                    name = FirstNonNull(GetString(addr, "city"), GetString(addr, "town"),
                                        GetString(addr, "village"), GetString(addr, "municipality"),
                                        GetString(addr, "hamlet"));
                    region = FirstNonNull(GetString(addr, "state"), GetString(addr, "state_district"),
                                          GetString(addr, "county"));
                    country = GetString(addr, "country");
                }

                if (string.IsNullOrEmpty(name))
                {
                    string display = GetString(r0, "display_name");
                    if (!string.IsNullOrEmpty(display))
                        name = display.Split(',')[0].Trim();
                }
            }

            name = CleanCity(name);

            if (string.IsNullOrEmpty(name))
            {
                error = "Ciudad no encontrada.";
                return null;
            }

            // 2) Clima actual
            string wurl = "https://api.open-meteo.com/v1/forecast?latitude=" +
                lat.ToString("0.####", CultureInfo.InvariantCulture) + "&longitude=" +
                lon.ToString("0.####", CultureInfo.InvariantCulture) +
                "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,is_day&timezone=auto";

            double temp = 0, feels = 0, hum = 0, wind = 0;
            int wmo = 3, isDay = 1;
            string time = null;

            using (JsonDocument w = GetJson(http, wurl))
            {
                if (w == null)
                {
                    error = "No se pudo contactar la API de clima.";
                    return null;
                }
                if (!w.RootElement.TryGetProperty("current", out JsonElement cur))
                {
                    error = "Respuesta de clima invalida.";
                    return null;
                }

                temp = GetDouble(cur, "temperature_2m");
                feels = GetDouble(cur, "apparent_temperature");
                hum = GetDouble(cur, "relative_humidity_2m");
                wind = GetDouble(cur, "wind_speed_10m");
                wmo = (int)GetDouble(cur, "weather_code");
                isDay = (int)GetDouble(cur, "is_day");
                time = GetString(cur, "time");
            }

            int iconCode = WmoIcono(wmo);
            string localTime = string.IsNullOrEmpty(time) ? "" : time.Replace('T', ' ');

            return new WeatherApiResponse
            {
                Location = new WeatherLocation
                {
                    Name = name,
                    Region = region ?? "",
                    Country = country ?? "",
                    LocalTime = localTime,
                },
                Current = new WeatherCurrent
                {
                    TempC = temp,
                    FeelsLikeC = feels,
                    Humidity = hum,
                    WindKph = wind,
                    Condition = new WeatherCondition
                    {
                        Text = null,   // se traduce por codigo
                        Code = iconCode,
                        Icon = "https://cdn.weatherapi.com/weather/64x64/" +
                               (isDay == 1 ? "day" : "night") + "/" + iconCode + ".png",
                    },
                },
            };
        }

        public static byte[] DownloadIcon(HttpClient http, string iconUrl)
        {
            string url = AbsUrl(iconUrl);
            return url == null ? null : Get(http, url);
        }

        // ------------------------------------------------------------------
        // Foto de la ciudad
        // ------------------------------------------------------------------
        private static bool EsPostal(string nombreArchivo)
        {
            string n = nombreArchivo.ToLowerInvariant();
            return !ArchivosMalos.Any(m => n.Contains(m));
        }

        private static byte[] ValidarFoto(byte[] b, int minW, int minH)
        {
            if (b == null || b.Length == 0) return null;

            try
            {
                using (Image im = Image.Load(b))
                {
                    if (im.Width < minW || im.Height < minH)
                        return null;

                    if (im.Width > 1400)
                        im.Mutate(x => x.Resize(1400, 0));

                    using (MemoryStream ms = new MemoryStream())
                    {
                        im.SaveAsJpeg(ms, new JpegEncoder { Quality = 85 });
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        private static byte[] WikiResumen(HttpClient http, string titulo, string lang)
        {
            JsonDocument d = GetJson(http,
                "https://" + lang + ".wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(titulo));
            if (d == null) return null;

            using (d)
            {
                if (d.RootElement.TryGetProperty("type", out JsonElement ty) &&
                    ty.GetString() == "disambiguation")
                    return null;

                if (!d.RootElement.TryGetProperty("originalimage", out JsonElement orig))
                    return null;
                if (!orig.TryGetProperty("source", out JsonElement srcEl))
                    return null;

                string src = srcEl.GetString();
                if (string.IsNullOrEmpty(src)) return null;

                string archivo = Uri.UnescapeDataString(src.Split('?')[0].Split('/').Last());
                archivo = Regex.Replace(archivo, @"^\d+px-", "");
                if (!EsPostal(archivo)) return null;

                int width = orig.TryGetProperty("width", out JsonElement wEl) ? wEl.GetInt32() : 0;
                if (width > 1400)
                {
                    JsonDocument api = GetJson(http,
                        "https://commons.wikimedia.org/w/api.php?action=query&titles=File:" +
                        Uri.EscapeDataString(archivo) +
                        "&prop=imageinfo&iiprop=url|size&iiurlwidth=1280&format=json");
                    try
                    {
                        using (api)
                        {
                            JsonElement page = api.RootElement.GetProperty("query").GetProperty("pages")
                                .EnumerateObject().First().Value;
                            JsonElement ii = page.GetProperty("imageinfo")[0];
                            string thumb = ii.TryGetProperty("thumburl", out JsonElement tu) ? tu.GetString() : null;
                            string url2 = ii.TryGetProperty("url", out JsonElement u2) ? u2.GetString() : null;
                            src = thumb ?? url2 ?? src;
                        }
                    }
                    catch { }
                }

                return ValidarFoto(Get(http, src), 600, 400);
            }
        }

        private static List<string> WikiTitulos(HttpClient http, string nombre, string pais, string lang)
        {
            List<string> outList = new List<string> { nombre };
            if (!string.IsNullOrEmpty(pais))
            {
                outList.Add(nombre + ", " + pais);
                outList.Add(nombre + " " + pais);
            }
            outList.Add(nombre + " City");

            JsonDocument d = GetJson(http,
                "https://" + lang + ".wikipedia.org/w/api.php?action=query&list=search&srsearch=" +
                Uri.EscapeDataString((nombre + " " + pais).Trim()) +
                "&srnamespace=0&srlimit=5&format=json");

            string baseLower = nombre.ToLowerInvariant();
            if (d != null)
            {
                using (d)
                {
                    try
                    {
                        foreach (JsonElement hit in d.RootElement.GetProperty("query").GetProperty("search").EnumerateArray())
                        {
                            string titulo = hit.GetProperty("title").GetString() ?? "";
                            string tLower = titulo.ToLowerInvariant();
                            if (tLower.StartsWith(baseLower))
                            {
                                string resto = titulo.Substring(nombre.Length);
                                if (resto.StartsWith(",") || resto.StartsWith(" ("))
                                    outList.Add(titulo);
                            }
                        }
                    }
                    catch { }
                }
            }

            HashSet<string> seen = new HashSet<string>();
            List<string> finales = new List<string>();
            foreach (string t in outList)
                if (seen.Add(t.ToLowerInvariant()))
                    finales.Add(t);
            return finales;
        }

        private static byte[] FotoWikipedia(HttpClient http, string nombre, string pais)
        {
            foreach (string lang in new[] { "es", "en" })
                foreach (string titulo in WikiTitulos(http, nombre, pais, lang))
                {
                    byte[] foto = WikiResumen(http, titulo, lang);
                    if (foto != null)
                        return foto;
                }
            return null;
        }

        private static bool MismoLugar(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();
            return a.Contains(b) || b.Contains(a);
        }

        // "<ciudad> <region>" y si la region repite la ciudad, "<ciudad> <pais>".
        private static string LugarFoto(string nombre, string region, string pais)
        {
            if (!string.IsNullOrWhiteSpace(region) && !MismoLugar(region, nombre))
                return (nombre + " " + region).Trim();
            if (!string.IsNullOrWhiteSpace(pais) && !MismoLugar(pais, nombre))
                return (nombre + " " + pais).Trim();
            return nombre;
        }

        private static byte[] FotoBing(HttpClient http, string place)
        {
            string q = Uri.EscapeDataString(("real photo of " + place).Trim());
            return ValidarFoto(Get(http, "https://th.bing.com/th?q=" + q), 320, 220);
        }

        public static byte[] FotoCiudad(HttpClient http, string cacheDir, string nombre, string region, string pais,
                                        bool forzar, bool bingFallback, out string credito)
        {
            credito = null;
            string key = Md5((nombre + "|" + region + "|" + pais).ToLowerInvariant());
            string photoDir = IOPath.Combine(cacheDir, "photos");
            try { Directory.CreateDirectory(photoDir); } catch { }

            string marcador = IOPath.Combine(photoDir, key + ".none");

            if (!forzar)
            {
                foreach (string origen in new[] { "bing", "wikipedia" })
                {
                    string p = IOPath.Combine(photoDir, key + "." + origen + ".jpg");
                    try
                    {
                        if (File.Exists(p) && new FileInfo(p).Length > 1000)
                        {
                            credito = Creditos[origen];
                            return File.ReadAllBytes(p);
                        }
                    }
                    catch { }
                }

                try
                {
                    if (File.Exists(marcador) &&
                        (DateTime.UtcNow - File.GetLastWriteTimeUtc(marcador)).TotalSeconds < 3600)
                        return null;
                }
                catch { }
            }

            string place = LugarFoto(nombre, region, pais);
            var buscadores = new List<(string origen, Func<byte[]> fn)>
            {
                // Primero Bing (rapido): "real photo of <ciudad> <region|pais>".
                ("bing", () => FotoBing(http, place)),
                ("wikipedia", () => FotoWikipedia(http, nombre, pais)),
            };

            foreach (var (origen, fn) in buscadores)
            {
                byte[] foto = null;
                try { foto = fn(); } catch { }

                if (foto != null)
                {
                    try { File.WriteAllBytes(IOPath.Combine(photoDir, key + "." + origen + ".jpg"), foto); } catch { }
                    try { if (File.Exists(marcador)) File.Delete(marcador); } catch { }
                    credito = Creditos[origen];
                    return foto;
                }
            }

            try { File.WriteAllText(marcador, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()); } catch { }
            return null;
        }

        // ------------------------------------------------------------------
        // Composicion de la tarjeta
        // ------------------------------------------------------------------
        private static Image<Rgba32> Gradiente(int w, int h)
        {
            Image<Rgba32> img = new Image<Rgba32>(w, h);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)h;
                byte r = (byte)(25 + (10 - 25) * t);
                byte g = (byte)(32 + (14 - 32) * t);
                byte b = (byte)(60 + (26 - 60) * t);
                for (int x = 0; x < w; x++)
                    img[x, y] = new Rgba32(r, g, b, 255);
            }
            return img;
        }

        private static Image<Rgba32> FondoPostal(byte[] fotoBytes, int w, int h)
        {
            Image<Rgba32> im = Image.Load<Rgba32>(fotoBytes);

            // cubrir el lienzo y recortar centrado
            float s = Math.Max(w / (float)im.Width, h / (float)im.Height);
            int nw = Math.Max(w, (int)Math.Round(im.Width * s));
            int nh = Math.Max(h, (int)Math.Round(im.Height * s));
            im.Mutate(x => x.Resize(nw, nh).Brightness(0.72f));

            int left = (nw - w) / 2;
            int top = (nh - h) / 2;
            im.Mutate(x => x.Crop(new Rectangle(left, top, w, h)));

            // overlay horizontal: izquierda legible, derecha se ve la foto
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)Math.Max(w - 1, 1);
                float a = (205 - 115 * t) / 255f;
                for (int y = 0; y < h; y++)
                {
                    Rgba32 px = im[x, y];
                    im[x, y] = new Rgba32(
                        (byte)(8 * a + px.R * (1 - a)),
                        (byte)(12 * a + px.G * (1 - a)),
                        (byte)(24 * a + px.B * (1 - a)),
                        255);
                }
            }

            // banda inferior
            for (int y = 0; y < h; y++)
            {
                float t = y < h * 0.58f ? 0f : (y - h * 0.58f) / (h * 0.42f);
                float a = (145 * t) / 255f;
                if (a <= 0f) continue;
                for (int x = 0; x < w; x++)
                {
                    Rgba32 px = im[x, y];
                    im[x, y] = new Rgba32(
                        (byte)(8 * a + px.R * (1 - a)),
                        (byte)(12 * a + px.G * (1 - a)),
                        (byte)(24 * a + px.B * (1 - a)),
                        255);
                }
            }

            return im;
        }

        private static IPath RoundedRect(RectangleF r, float radius)
        {
            float x = r.X, y = r.Y, w = r.Width, h = r.Height, rad = radius;
            PathBuilder pb = new PathBuilder();
            pb.MoveTo(new PointF(x + rad, y));
            pb.LineTo(new PointF(x + w - rad, y));
            pb.QuadraticBezierTo(new Vector2(x + w, y), new Vector2(x + w, y + rad));
            pb.LineTo(new PointF(x + w, y + h - rad));
            pb.QuadraticBezierTo(new Vector2(x + w, y + h), new Vector2(x + w - rad, y + h));
            pb.LineTo(new PointF(x + rad, y + h));
            pb.QuadraticBezierTo(new Vector2(x, y + h), new Vector2(x, y + h - rad));
            pb.LineTo(new PointF(x, y + rad));
            pb.QuadraticBezierTo(new Vector2(x, y), new Vector2(x + rad, y));
            pb.CloseFigure();
            return pb.Build();
        }

        private static string Num(double v)
        {
            return v == Math.Floor(v)
                ? ((long)v).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public static byte[] Render(WeatherApiResponse data, byte[] iconBytes, byte[] photoBytes, string fotoCredito)
        {
            Image<Rgba32> img;
            if (photoBytes != null && photoBytes.Length > 0)
            {
                try { img = FondoPostal(photoBytes, W, H); }
                catch { img = Gradiente(W, H); }
            }
            else
            {
                img = Gradiente(W, H);
            }

            string hora = "";
            if (!string.IsNullOrEmpty(data.Location.LocalTime))
            {
                string[] parts = data.Location.LocalTime.Split(' ');
                if (parts.Length > 1) hora = parts[1];
            }

            var metrics = new (string label, string value)[]
            {
                ("HUMEDAD", Math.Round(data.Current.Humidity).ToString(CultureInfo.InvariantCulture) + "%"),
                ("VIENTO", Num(data.Current.WindKph) + " km/h"),
                ("SENSACION", Math.Round(data.Current.FeelsLikeC).ToString(CultureInfo.InvariantCulture) + "\u00B0C"),
                ("HORA LOCAL", hora),
            };

            Font fCiudad = GetFont(34, FontStyle.Bold);
            Font fPais = GetFont(18, FontStyle.Regular);
            Font fTemp = GetFont(96, FontStyle.Bold);
            Font fLabel = GetFont(16, FontStyle.Regular);
            Font fVal = GetFont(22, FontStyle.Bold);
            Font fEstado = GetFont(22, FontStyle.Regular);
            Font fHora = GetFont(16, FontStyle.Regular);

            img.Mutate(ctx =>
            {
                IPath border = RoundedRect(new RectangleF(20, 20, W - 40, H - 40), 28);
                ctx.Fill(Color.FromRgba(255, 255, 255, 18), border);
                ctx.Draw(Color.FromRgba(255, 255, 255, 40), 1f, border);

                if (iconBytes != null && iconBytes.Length > 0)
                {
                    try
                    {
                        using (Image<Rgba32> ic = Image.Load<Rgba32>(iconBytes))
                        {
                            ic.Mutate(x => x.Resize(140, 140));
                            ctx.DrawImage(ic, new Point(W - 200, 60), 1f);
                        }
                    }
                    catch { }
                }

                ctx.DrawText(new RichTextOptions(fCiudad) { Origin = new PointF(50, 50) },
                    data.Location.Name ?? "", Color.White);
                ctx.DrawText(new RichTextOptions(fPais) { Origin = new PointF(50, 95) },
                    (data.Location.Region ?? "") + ", " + (data.Location.Country ?? ""), Color.FromRgb(180, 190, 210));

                ctx.DrawText(new RichTextOptions(fTemp) { Origin = new PointF(50, 150) },
                    Math.Round(data.Current.TempC).ToString(CultureInfo.InvariantCulture) + "\u00B0C", Color.White);

                ctx.DrawText(new RichTextOptions(fEstado) { Origin = new PointF(50, 260) },
                    ConditionNames.Traducir(data.Current.Condition?.Text, data.Current.Condition?.Code),
                    Color.FromRgb(200, 210, 230));

                int y0 = 300;
                int boxW = (W - 100) / metrics.Length;
                for (int i = 0; i < metrics.Length; i++)
                {
                    int x = 50 + i * boxW;
                    ctx.DrawText(new RichTextOptions(fLabel) { Origin = new PointF(x, y0) },
                        metrics[i].label, Color.FromRgb(150, 160, 185));
                    ctx.DrawText(new RichTextOptions(fVal) { Origin = new PointF(x, y0 + 22) },
                        metrics[i].value, Color.White);
                }

                ctx.DrawText(new RichTextOptions(fHora) { Origin = new PointF(50, H - 45) },
                    "open-meteo.com", Color.FromRgb(120, 130, 155));

                if (!string.IsNullOrEmpty(fotoCredito))
                {
                    FontRectangle size = TextMeasurer.MeasureSize(fotoCredito, new RichTextOptions(fHora));
                    ctx.DrawText(new RichTextOptions(fHora) { Origin = new PointF(W - 50 - size.Width, H - 45) },
                        fotoCredito, Color.FromRgb(120, 130, 155));
                }
            });

            using (MemoryStream ms = new MemoryStream())
            {
                img.SaveAsJpeg(ms, new JpegEncoder { Quality = 87 });
                return ms.ToArray();
            }
        }
    }
}
