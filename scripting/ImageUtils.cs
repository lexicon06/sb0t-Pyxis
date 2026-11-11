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
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace scripting
{
    internal static class ImageUtils
    {
        public static byte[] ToJpeg(byte[] raw, int width, int height)
        {
            using (MemoryStream ms = new MemoryStream(raw))
            using (Image image = Image.Load(ms))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Stretch
                }));

                using (MemoryStream out_ms = new MemoryStream())
                {
                    image.SaveAsJpeg(out_ms);
                    return out_ms.ToArray();
                }
            }
        }

        public static void Resize(byte[] raw, out int width, out int height)
        {
            using (MemoryStream ms = new MemoryStream(raw))
            using (Image image = Image.Load(ms))
            {
                width = image.Width;
                height = image.Height;
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

        // Genera el JPEG (para cb0t/Ares, sin alpha) y, si la imagen tiene
        // transparencia, tambien el PNG con alpha (para inbizier web/mobile).
        public static void ToSized(byte[] raw, int img_x, int img_y, out byte[] jpeg, out byte[] png)
        {
            jpeg = null;
            png = null;

            using (MemoryStream ms = new MemoryStream(raw))
            using (Image<Rgba32> image = Image.Load<Rgba32>(ms))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(img_x, img_y),
                    Mode = ResizeMode.Stretch
                }));

                bool hasAlpha = false;
                image.ProcessPixelRows(a =>
                {
                    for (int y = 0; y < a.Height && !hasAlpha; y++)
                    {
                        Span<Rgba32> row = a.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                            if (row[x].A < 255)
                            {
                                hasAlpha = true;
                                break;
                            }
                    }
                });

                // PNG con alpha (para web/mobile)
                if (hasAlpha)
                    using (MemoryStream png_ms = new MemoryStream())
                    {
                        image.SaveAsPng(png_ms);
                        png = png_ms.ToArray();
                    }

                // JPEG (para cb0t/Ares): aplanar sobre blanco respetando el alpha
                image.ProcessPixelRows(src =>
                {
                    for (int y = 0; y < src.Height; y++)
                    {
                        Span<Rgba32> row = src.GetRowSpan(y);

                        for (int x = 0; x < row.Length; x++)
                        {
                            Rgba32 p = row[x];
                            int a = p.A;
                            row[x] = new Rgba32(
                                (byte)((p.R * a + 255 * (255 - a)) / 255),
                                (byte)((p.G * a + 255 * (255 - a)) / 255),
                                (byte)((p.B * a + 255 * (255 - a)) / 255),
                                255);
                        }
                    }
                });

                using (MemoryStream out_ms = new MemoryStream())
                {
                    image.SaveAsJpeg(out_ms);
                    jpeg = out_ms.ToArray();
                }
            }
        }
    }
}
