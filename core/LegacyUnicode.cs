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

using System;
using System.Text;

namespace core
{
    /// <summary>
    /// Los clientes Ares antiguos (y algunos forks sin actualizar) decodifican el
    /// UTF-8 con un decoder propio (UTF8BufToWideCharBuf) que rompe las secuencias
    /// de 4 bytes (caracteres fuera del BMP, U+10000..U+10FFFF: emojis, letras
    /// matematicas/estilizadas, indicadores regionales, etc.). El resultado es
    /// basura tipo "醇ݖ猏" en lugar del caracter correcto.
    ///
    /// Este helper hace, ANTES de serializar:
    ///   1) Normaliza con NFKC: los caracteres estilizados (𝐉, 𝕁, Ⓗ, Ａ) se
    ///      convierten a su forma base (J, J, H, A). Los acentos BMP (enie, tildes)
    ///      se mantienen.
    ///   2) Lo que siga fuera del BMP (emojis reales, sin equivalente) se reemplaza
    ///      por '?' para que el cliente no muestre bytes corruptos.
    ///
    /// Los caracteres BMP (incluidos acentos como la enie) quedan intactos.
    /// </summary>
    internal static class LegacyUnicode
    {
        // Paleta de colores Ares (misma tabla que Helpers.acols): indices 00..46.
        private static readonly int[][] acols_rgb = new int[][]
        {
            new[]{255,255,255}, new[]{0,0,0},     new[]{0,0,128},   new[]{0,128,0},
            new[]{255,0,0},     new[]{128,0,0},   new[]{128,0,128}, new[]{255,165,0},
            new[]{255,255,0},   new[]{0,255,0},   new[]{0,128,128}, new[]{0,255,255},
            new[]{0,0,255},     new[]{255,0,255}, new[]{128,128,128}, new[]{192,192,192},
            new[]{255,69,0},    new[]{139,69,19}, new[]{0,139,139}, new[]{75,0,130},
            new[]{220,20,60},   new[]{34,139,34}, new[]{153,50,204}, new[]{255,105,180},
            new[]{47,79,79},    new[]{176,196,222}, new[]{124,252,0}, new[]{32,178,170},
            new[]{222,184,135}, new[]{127,255,0}, new[]{184,134,11}, new[]{139,0,139},
            new[]{0,191,255},   new[]{255,215,0}, new[]{240,128,128}, new[]{147,112,219},
            new[]{128,128,0},   new[]{219,112,147}, new[]{188,143,143}, new[]{46,139,87},
            new[]{106,90,205},  new[]{0,255,127}, new[]{255,99,71},  new[]{238,130,238},
            new[]{245,222,179}, new[]{154,205,50}
        };

        // Los clientes Ares legacy solo soportan los primeros 16 colores (00..15).
        private const int AresLegacyColors = 16;

        // Equivalencia explicita paleta extendida (16..45) -> paleta base (0..15).
        // Definida a mano para que cada color caiga donde se espera (los verdes en
        // verde, los azules en azul, etc.). El indice es la posicion en la tabla.
        private static readonly int[] color_map = new int[]
        {
            // 16..23
            4,   // OrangeRed       -> Red
            5,   // SaddleBrown     -> Maroon
            10,  // DarkCyan        -> Teal
            6,   // Indigo          -> Purple
            4,   // Crimson         -> Red
            3,   // ForestGreen     -> Green
            6,   // DarkOrchid      -> Purple
            13,  // HotPink         -> Fuchsia
            // 24..31
            14,  // DarkSlateGray   -> Gray
            15,  // LightSteelBlue  -> Silver
            3,   // LawnGreen       -> Green
            10,  // LightSeaGreen   -> Teal
            7,   // BurlyWood       -> Orange
            9,   // Chartreuse      -> Lime
            7,   // DarkGoldenrod   -> Orange
            6,   // DarkMagenta     -> Purple
            // 32..39
            11,  // DeepSkyBlue     -> Aqua
            8,   // Gold            -> Yellow
            4,   // LightCoral      -> Red
            6,   // MediumPurple    -> Purple
            3,   // Olive           -> Green
            13,  // PaleVioletRed   -> Fuchsia
            14,  // RosyBrown       -> Gray
            3,   // SeaGreen        -> Green
            // 40..45
            12,  // SlateBlue       -> Blue
            3,   // SpringGreen     -> Green
            4,   // Tomato          -> Red
            13,  // Violet          -> Fuchsia
            15,  // Wheat           -> Silver
            3    // YellowGreen     -> Green
        };

        public static String Sanitize(String text)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            // 0) Colores fuera de la paleta que soporta Ares legacy (>15): aproximar.
            text = ApproximateColors(text);

            // 1) Normalizacion de compatibilidad: 𝐉 -> J, Ａ -> A, etc.
            String normalized;

            try
            {
                normalized = text.Normalize(NormalizationForm.FormKC);
            }
            catch
            {
                // Si la cadena tuviera surrogates sueltos, Normalize tira excepcion.
                normalized = text;
            }

            // 2) Reemplazar lo que siga fuera del BMP (surrogate pairs).
            bool hasSurrogate = false;

            for (int i = 0; i < normalized.Length; i++)
                if (Char.IsSurrogate(normalized[i]))
                {
                    hasSurrogate = true;
                    break;
                }

            if (!hasSurrogate)
                return normalized;

            StringBuilder sb = new StringBuilder(normalized.Length);

            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];

                if (Char.IsHighSurrogate(c))
                {
                    if (i + 1 < normalized.Length && Char.IsLowSurrogate(normalized[i + 1]))
                    {
                        sb.Append('?');
                        i++;
                    }
                    else
                    {
                        sb.Append('?');
                    }
                }
                else if (Char.IsLowSurrogate(c))
                {
                    sb.Append('?');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Recorre el texto buscando codigos de color Ares (\x03 = letra, \x05 = fondo)
        /// seguidos de dos digitos (00..46). Los clientes Ares legacy solo soportan los
        /// 16 primeros colores, asi que cualquier indice > 15 se reemplaza por el color
        /// base (0..15) mas cercano en RGB. Los indices <= 15 se dejan igual.
        /// </summary>
        private static String ApproximateColors(String text)
        {
            // Camino rapido: si no hay \x03 ni \x05, no hay nada que hacer.
            bool hasColor = false;

            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\x03' || text[i] == '\x05')
                {
                    hasColor = true;
                    break;
                }

            if (!hasColor)
                return text;

            StringBuilder sb = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if ((c == '\x03' || c == '\x05') && i + 2 < text.Length + 1 && i + 2 <= text.Length)
                {
                    // Necesitamos dos digitos a continuacion.
                    if (i + 2 < text.Length &&
                        Char.IsDigit(text[i + 1]) && Char.IsDigit(text[i + 2]))
                    {
                        int index = (text[i + 1] - '0') * 10 + (text[i + 2] - '0');

                        if (index >= 0 && index < acols_rgb.Length)
                        {
                            if (index < AresLegacyColors)
                            {
                                // Soportado tal cual.
                                sb.Append(c);
                                sb.Append(text[i + 1]);
                                sb.Append(text[i + 2]);
                            }
                            else
                            {
                                // Indice de la paleta extendida: usar la equivalencia.
                                int near = color_map[index - AresLegacyColors];

                                sb.Append(c);
                                sb.Append((char)('0' + (near / 10)));
                                sb.Append((char)('0' + (near % 10)));
                            }

                            i += 2;
                            continue;
                        }

                        // Indice fuera de la paleta: se descarta el codigo entero
                        // (Ares no entenderia un codigo invalido).
                        i += 2;
                        continue;
                    }
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
