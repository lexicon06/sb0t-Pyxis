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
    }
}
