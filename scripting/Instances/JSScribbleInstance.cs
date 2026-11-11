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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Threading;
using Jurassic;
using Jurassic.Library;

namespace scripting.Instances
{
    class JSScribbleInstance : ObjectInstance
    {
        private bool busy = false;

        public JSScribbleInstance(ObjectInstance prototype)
            : base(prototype)
        {
            this.PopulateFunctions();

            DefineProperty(Engine.Symbol.ToStringTag, new PropertyDescriptor("Scribble", PropertyAttributes.Sealed), true);
        }

        [JSProperty(Name = "src")]
        public String Source { get; set; }

        [JSProperty(Name = "oncomplete")]
        public UserDefinedFunction Callback { get; set; }

        [JSFunction(Name = "download", IsWritable = false, IsEnumerable = true)]
        public bool Download(object a)
        {
            if (this.busy)
                return false;

            Thread thread = new Thread(new ThreadStart(() =>
            {
                this.busy = true;
                String arg = String.Empty;

                if (!(a is Undefined) && a != null)
                    arg = a.ToString();

                Objects.JSScribbleImage result = new Objects.JSScribbleImage(this.Engine.Object.InstancePrototype)
                {
                    Callback = this.Callback,
                    Data = null,
                    ScriptName = this.Engine.UserData as string,
                    Arg = arg,
                    URL = this.Source
                };

                try
                {
                    WebRequest request = WebRequest.Create(this.Source);
                    List<byte> bytes_in = new List<byte>();

                    using (WebResponse response = request.GetResponse())
                    {
                        int received = 0;
                        byte[] buf = new byte[1024];

                        using (Stream stream = response.GetResponseStream())
                            while ((received = stream.Read(buf, 0, 1024)) > 0)
                                bytes_in.AddRange(buf.Take(received));
                    }

                    int img_x, img_y;

                    using (MemoryStream raw_ms = new MemoryStream(bytes_in.ToArray()))
                    using (SixLabors.ImageSharp.Image avatar_raw = SixLabors.ImageSharp.Image.Load(raw_ms))
                    {
                        img_x = avatar_raw.Width;
                        img_y = avatar_raw.Height;

                        if (img_x > 384)
                        {
                            img_x = 384;
                            img_y = avatar_raw.Height - (int)Math.Floor(Math.Floor((double)avatar_raw.Height / 100) * Math.Floor(((double)(avatar_raw.Width - 384) / avatar_raw.Width) * 100));
                        }

                        if (img_y > 384)
                        {
                            img_x -= (int)Math.Floor(Math.Floor((double)img_x / 100) * Math.Floor(((double)(img_y - 384) / img_y) * 100));
                            img_y = 384;
                        }
                    }

                    try
                    {
                        ImageUtils.ToSized(bytes_in.ToArray(), img_x, img_y, out byte[] _jpeg, out byte[] _png);
                        result.Data = _jpeg != null ? Server.Compression.Compress(_jpeg) : null;
                        result.DataPng = _png != null ? Server.Compression.Compress(_png) : null;
                    }
                    catch { }

                    result.Height = img_y;
                    bytes_in.Clear();
                }
                catch { }

                ScriptManager.Callbacks.Enqueue(result);
                this.busy = false;
            }));

            thread.Start();

            return true;
        }

        private String[] bad_chars = new String[]
        {
            "/",
            "\\",
            " ",
        };

        [JSFunction(Name = "load", IsWritable = false, IsEnumerable = true)]
        public Objects.JSScribbleImage Load(object a)
        {
            Objects.JSScribbleImage scr = new Objects.JSScribbleImage(this.Engine.Object.InstancePrototype);

            if (a is String || a is ConcatenatedString)
            {
                String filename = a.ToString();

                if (filename.Length > 1)
                    if (bad_chars.Count<String>(x => filename.Contains(x)) == 0)
                    {
                        String path = Path.Combine(Server.DataPath, this.Engine.UserData as string, "data", filename);

                        try
                        {
                            if (File.Exists(path))
                            {
                                byte[] data = File.ReadAllBytes(path);

                                try
                                {
                                    int img_x, img_y;

                                    using (MemoryStream raw_ms = new MemoryStream(data))
                                    using (SixLabors.ImageSharp.Image avatar_raw = SixLabors.ImageSharp.Image.Load(raw_ms))
                                    {
                                        img_x = avatar_raw.Width;
                                        img_y = avatar_raw.Height;

                                        if (img_x > 384)
                                        {
                                            img_x = 384;
                                            img_y = avatar_raw.Height - (int)Math.Floor(Math.Floor((double)avatar_raw.Height / 100) * Math.Floor(((double)(avatar_raw.Width - 384) / avatar_raw.Width) * 100));
                                        }

                                        if (img_y > 384)
                                        {
                                            img_x -= (int)Math.Floor(Math.Floor((double)img_x / 100) * Math.Floor(((double)(img_y - 384) / img_y) * 100));
                                            img_y = 384;
                                        }
                                    }

                                    ImageUtils.ToSized(data, img_x, img_y, out byte[] _jpeg, out byte[] _png);
                                    scr.Height = img_y;
                                    scr.Data = _jpeg != null ? Server.Compression.Compress(_jpeg) : null;
                                    scr.DataPng = _png != null ? Server.Compression.Compress(_png) : null;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
            }

            return scr;
        }
    }
}
