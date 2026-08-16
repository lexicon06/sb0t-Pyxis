using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace gui.Services
{
    internal static class ImageUtils
    {
        public static byte[] ResizeToJpeg(byte[] raw, int width, int height)
        {
            using (MemoryStream ms = new MemoryStream(raw))
            using (Image image = Image.Load(ms))
            {
                image.Mutate(x => x
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(width, height),
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

        public static byte[] ReadBytes(string path)
        {
            return File.ReadAllBytes(path);
        }
    }
}
