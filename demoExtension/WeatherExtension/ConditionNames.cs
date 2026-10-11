using System;
using System.Collections.Generic;

namespace WeatherExtension
{
    /// <summary>
    /// Traduccion de las condiciones de WeatherAPI (texto/codigo -> espanol).
    /// Port de las tablas de weather-cards/main.py.
    /// </summary>
    internal static class ConditionNames
    {
        // Lista oficial de condiciones: https://www.weatherapi.com/docs/weather_conditions.json
        private static readonly Dictionary<string, string> ESTADOS_TEXTO =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "sunny", "Soleado" },
            { "clear", "Despejado" },
            { "partly cloudy", "Parcialmente nublado" },
            { "cloudy", "Nublado" },
            { "overcast", "Cubierto" },
            { "haze", "Calima" },
            { "dust haze", "Calima polvorienta" },
            { "blowing dust", "Polvo en el viento" },
            { "dust storm", "Tormenta de polvo" },
            { "sandstorm", "Tormenta de arena" },
            { "severe sandstorm", "Tormenta de arena intensa" },
            { "mist", "Neblina" },
            { "smoke", "Humo" },
            { "smoky haze", "Calima humosa" },
            { "smog", "Smog" },
            { "severe smog", "Smog intenso" },
            { "saharan dust", "Polvo del Sahara" },
            { "dust", "Polvo" },
            { "patchy rain possible", "Posibles lluvias" },
            { "patchy snow possible", "Posibles nevadas" },
            { "patchy sleet possible", "Posible aguanieve" },
            { "patchy freezing drizzle possible", "Posible llovizna helada" },
            { "thundery outbreaks possible", "Posibles tormentas" },
            { "blowing snow", "Nieve en el viento" },
            { "blizzard", "Ventisca" },
            { "fog", "Niebla" },
            { "freezing fog", "Niebla helada" },
            { "patchy light drizzle", "Llovizna ligera a intervalos" },
            { "light drizzle", "Llovizna ligera" },
            { "freezing drizzle", "Llovizna helada" },
            { "heavy freezing drizzle", "Llovizna helada intensa" },
            { "patchy light rain", "Lluvia ligera a intervalos" },
            { "light rain", "Lluvia ligera" },
            { "moderate rain at times", "Lluvia moderada a ratos" },
            { "moderate rain", "Lluvia moderada" },
            { "heavy rain at times", "Lluvia intensa a ratos" },
            { "heavy rain", "Lluvia intensa" },
            { "light freezing rain", "Lluvia helada ligera" },
            { "moderate or heavy freezing rain", "Lluvia helada moderada o intensa" },
            { "light sleet", "Aguanieve ligera" },
            { "moderate or heavy sleet", "Aguanieve moderada o intensa" },
            { "patchy light snow", "Nieve ligera a intervalos" },
            { "light snow", "Nieve ligera" },
            { "patchy moderate snow", "Nieve moderada a intervalos" },
            { "moderate snow", "Nieve moderada" },
            { "patchy heavy snow", "Nieve intensa a intervalos" },
            { "heavy snow", "Nieve intensa" },
            { "ice pellets", "Granos de hielo" },
            { "light rain shower", "Chubascos ligeros" },
            { "moderate or heavy rain shower", "Chubascos moderados o intensos" },
            { "torrential rain shower", "Chubascos torrenciales" },
            { "light sleet showers", "Chubascos de aguanieve ligeros" },
            { "moderate or heavy sleet showers", "Chubascos de aguanieve moderados o intensos" },
            { "light snow showers", "Chubascos de nieve ligeros" },
            { "moderate or heavy snow showers", "Chubascos de nieve moderados o intensos" },
            { "light showers of ice pellets", "Chubascos ligeros de granos de hielo" },
            { "moderate or heavy showers of ice pellets", "Chubascos de granos de hielo moderados o intensos" },
            { "patchy light rain with thunder", "Lluvia ligera con tormenta" },
            { "moderate or heavy rain with thunder", "Lluvia moderada o intensa con tormenta" },
            { "patchy light snow with thunder", "Nieve ligera con tormenta" },
            { "moderate or heavy snow with thunder", "Nieve moderada o intensa con tormenta" },
            { "patchy rain nearby", "Posibles lluvias" },
            { "patchy snowy nearby", "Posibles nevadas" },
            { "patchy snow nearby", "Posibles nevadas" },
            { "patchy sleet nearby", "Posible aguanieve" },
            { "patchy freezing drizzle nearby", "Posible llovizna helada" },
            { "patchy drizzle", "Llovizna a intervalos" },
            { "thundery outbreaks", "Posibles tormentas" },
            { "moderate or heavy snow", "Nieve moderada o intensa" },
            { "light freezing shower", "Chubascos helados ligeros" },
            { "moderate or heavy freezing shower", "Chubascos helados moderados o intensos" },
            { "light shower", "Chubascos ligeros" },
            { "moderate or heavy shower", "Chubascos moderados o intensos" },
            { "light snow shower", "Chubascos de nieve ligeros" },
            { "moderate or heavy snow shower", "Chubascos de nieve moderados o intensos" },
        };

        private static readonly Dictionary<int, string> ESTADOS_CODIGO = new Dictionary<int, string>
        {
            { 1000, "Soleado / Despejado" },
            { 1003, "Parcialmente nublado" },
            { 1006, "Nublado" },
            { 1009, "Cubierto" },
            { 1012, "Calima" },
            { 1015, "Calima polvorienta" },
            { 1018, "Polvo en el viento" },
            { 1021, "Tormenta de polvo" },
            { 1024, "Tormenta de arena" },
            { 1027, "Tormenta de arena intensa" },
            { 1030, "Neblina" },
            { 1033, "Humo" },
            { 1036, "Calima humosa" },
            { 1039, "Smog" },
            { 1042, "Smog intenso" },
            { 1045, "Polvo del Sahara" },
            { 1048, "Polvo" },
            { 1063, "Posibles lluvias" },
            { 1066, "Posibles nevadas" },
            { 1069, "Posible aguanieve" },
            { 1072, "Posible llovizna helada" },
            { 1087, "Posibles tormentas" },
            { 1114, "Nieve en el viento" },
            { 1117, "Ventisca" },
            { 1135, "Niebla" },
            { 1147, "Niebla helada" },
            { 1150, "Llovizna ligera a intervalos" },
            { 1153, "Llovizna ligera" },
            { 1168, "Llovizna helada" },
            { 1171, "Llovizna helada intensa" },
            { 1180, "Lluvia ligera a intervalos" },
            { 1183, "Lluvia ligera" },
            { 1186, "Lluvia moderada a ratos" },
            { 1189, "Lluvia moderada" },
            { 1192, "Lluvia intensa a ratos" },
            { 1195, "Lluvia intensa" },
            { 1198, "Lluvia helada ligera" },
            { 1201, "Lluvia helada moderada o intensa" },
            { 1204, "Aguanieve ligera" },
            { 1207, "Aguanieve moderada o intensa" },
            { 1210, "Nieve ligera a intervalos" },
            { 1213, "Nieve ligera" },
            { 1216, "Nieve moderada a intervalos" },
            { 1219, "Nieve moderada" },
            { 1222, "Nieve intensa a intervalos" },
            { 1225, "Nieve intensa" },
            { 1237, "Granos de hielo" },
            { 1240, "Chubascos ligeros" },
            { 1243, "Chubascos moderados o intensos" },
            { 1246, "Chubascos torrenciales" },
            { 1249, "Chubascos de aguanieve ligeros" },
            { 1252, "Chubascos de aguanieve moderados o intensos" },
            { 1255, "Chubascos de nieve ligeros" },
            { 1258, "Chubascos de nieve moderados o intensos" },
            { 1261, "Chubascos ligeros de granos de hielo" },
            { 1264, "Chubascos de granos de hielo moderados o intensos" },
            { 1273, "Lluvia ligera con tormenta" },
            { 1276, "Lluvia moderada o intensa con tormenta" },
            { 1279, "Nieve ligera con tormenta" },
            { 1282, "Nieve moderada o intensa con tormenta" },
        };

        private static readonly (string, string)[] SUBCADENAS =
        {
            ("thunder", "Tormenta electrica"),
            ("blizzard", "Ventisca"),
            ("freezing fog", "Niebla helada"),
            ("fog", "Niebla"),
            ("mist", "Neblina"),
            ("sleet", "Aguanieve"),
            ("ice pellet", "Granos de hielo"),
            ("hail", "Granizo"),
            ("drizzle", "Llovizna"),
            ("snow", "Nieve"),
            ("rain", "Lluvia"),
            ("shower", "Chubascos"),
            ("overcast", "Cubierto"),
            ("cloud", "Nublado"),
            ("sunny", "Soleado"),
            ("clear", "Despejado"),
            ("haze", "Calima"),
            ("smog", "Smog"),
            ("smoke", "Humo"),
            ("dust", "Polvo"),
            ("sand", "Tormenta de arena"),
        };

        private static readonly (string, string)[] PROBABLES =
        {
            ("thunder", "Posibles tormentas"),
            ("rain", "Posibles lluvias"),
            ("snow", "Posibles nevadas"),
            ("sleet", "Posible aguanieve"),
            ("drizzle", "Posible llovizna"),
            ("shower", "Posibles chubascos"),
            ("hail", "Posible granizo"),
            ("fog", "Posible niebla"),
        };

        public static string Traducir(string txt, int? code)
        {
            string t = (txt ?? string.Empty).Trim().ToLowerInvariant();

            if (ESTADOS_TEXTO.TryGetValue(t, out string byText))
                return byText;

            if (code.HasValue && ESTADOS_CODIGO.TryGetValue(code.Value, out string byCode))
                return byCode;

            if (t.Contains("patchy") || t.Contains("possible") || t.Contains("nearby") || t.Contains("chance"))
            {
                foreach (var (key, val) in PROBABLES)
                    if (t.Contains(key))
                        return val;
            }

            foreach (var (key, val) in SUBCADENAS)
                if (t.Contains(key))
                    return val;

            string trimmed = (txt ?? string.Empty).Trim();
            return trimmed.Length == 0 ? trimmed : char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        }
    }
}
