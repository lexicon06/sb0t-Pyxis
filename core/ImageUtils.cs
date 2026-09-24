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
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace core
{
    internal static class ImageUtils
    {
        public static byte[] ScaleJpeg(byte[] raw, int width, int height)
        {
            return ScaleJpeg(raw, width, height, 69);
        }

        public static byte[] ScaleJpeg(byte[] raw, int width, int height, int quality)
        {
            using (MemoryStream ms = new MemoryStream(raw))
            using (Image image = Image.Load(ms))
            {
                // Recorte tipo "object-position: top center" (crop anclado arriba,
                // centrado horizontalmente) para que nunca deforme avatares cuadrados.
                image.Mutate(x => x
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(width, height),
                        Mode = ResizeMode.Crop,
                        Position = AnchorPositionMode.Top
                    })
                    .BackgroundColor(Color.White));

                using (MemoryStream out_ms = new MemoryStream())
                {
                    image.SaveAsJpeg(out_ms, new JpegEncoder { Quality = quality });
                    return out_ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Formatea cualquier avatar AL ESTILO DE ARES: recorte top-center a 48x48
        /// y compresion JPEG 69. Devuelve null si no se puede (imagen rota).
        /// </summary>
        public static byte[] AvatarAres(byte[] raw)
        {
            try
            {
                return ScaleJpeg(raw, 48, 48, 69);
            }
            catch
            {
                return null;
            }
        }

        public static byte[] ToSizedJpeg(byte[] raw, int img_x, int img_y)
        {
            using (MemoryStream ms = new MemoryStream(raw))
            using (Image image = Image.Load(ms))
            {
                image.Mutate(x => x
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(img_x, img_y),
                        Mode = ResizeMode.Stretch
                    })
                    .BackgroundColor(Color.White));

                using (MemoryStream out_ms = new MemoryStream())
                {
                    image.SaveAsJpeg(out_ms);
                    return out_ms.ToArray();
                }
            }
        }
    }
}
