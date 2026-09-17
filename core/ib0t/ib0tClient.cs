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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using iconnect;
using Heijden.DNS;
using System.IO;
using core;

namespace core.ib0t
{
    class ib0tClient : IClient, IUser, IQuarantined
    {
        public uint LastScribble { get; set; }
        public bool Ares { get; set; }
        public bool SupportsHTML { get { return false; } }
        public ushort ID { get; private set; }
        public IPAddress ExternalIP { get; set; }
        public IPAddress OriginalIP { get; set; }
        public String DNS { get; set; }
        public Guid Guid { get; set; }
        public ushort FileCount { get; set; }
        public ushort DataPort { get; set; }
        public IPAddress NodeIP { get; set; }
        public ushort NodePort { get; set; }
        public String OrgName { get; set; }
        public String Version { get; set; }
        public IPAddress LocalIP { get; set; }
        public bool Browsable { get; set; }
        public byte Age { get; set; }
        private byte[] avatar = new byte[] { };
        public byte[] OrgAvatar { get; set; }
        public byte Sex { get; set; }
        public byte Country { get; set; }
        public String Region { get; set; }
        public bool FastPing { get; set; }
        public bool Ghosting { get; set; }
        public List<String> IgnoreList { get; set; }
        public bool CustomClient { get; set; }
        public List<String> CustomClientTags { get; set; }
        public bool WebClient { get; private set; }
        public bool Owner { get; set; }
        public Encryption Encryption { get; set; }
        public bool Captcha { get; set; }
        public uint Cookie { get; set; }
        public uint JoinTime { get; set; }
        public String CaptchaWord { get; set; }
        public ulong IdleStart { get; set; }
        public bool Quarantined { get; set; }
        public ILink Link { get { return new UserLinkCredentials(); } }
        public byte[] Password { get; set; }
        public bool Extended { get; set; }
        public IFont Font { get; set; }
        public IPEndPoint LocalEP { get; set; }

        public Html5RequestEventArgs WebCredentials { get; set; }
        public Socket Sock { get; set; }
        public ulong Time { get; set; }
        public bool ProtoConnected { get; set; }
        public bool LoggedIn { get; set; }
        public FloodRecord FloodRecord { get; private set; }

        private int socket_health = 0;
        private ByteBuffer data_in = new ByteBuffer();
        private ConcurrentQueue<byte[]> data_out = new ConcurrentQueue<byte[]>();

        private byte[] recv_buffer = new byte[8192];

        // Security limits for untrusted web clients. Oversized frames are
        // dropped (not honoured and not buffered) instead of disconnecting
        // the user, so an attacker cannot exhaust server memory.
        private const int MAX_BUFFER_SIZE = 64 * 1024 * 1024;  // 64 MB buffered data
        private const int MAX_MESSAGE_SIZE = 30 * 1024 * 1024; // 30 MB per message
        private ILevel _level = ILevel.Regular;
        private String _name = String.Empty;
        private ushort _vroom = 0;
        private String _customname = String.Empty;
        private String personal_message = String.Empty;
        public bool AvatarReceived { get; set; }
        public ulong AvatarTimeout { get; set; }
        public bool DefaultAvatar { get; set; }
        private uint asn = 0;
        private Resolver dnsResolver = new Resolver();
        public bool IsInbizierMobile { get; set; }
        public bool IsInbizierWeb { get; set; }
        public ib0tClient(AresClient client, ulong time, ushort id)
        {
            this.WebClient = true;
            this.IsInbizierWeb = false;
            this.IsInbizierMobile= false;
            this.WebCredentials = new Html5RequestEventArgs();
            this.ID = id;
            this.avatar = null;
            this.fullavatar = null;
            this.Sock = client.Sock;
            this.WebCredentials.Key = String.Empty;
            this.data_in.AddRange(client.ReceiveDump);
            this.WebCredentials.Key1 = String.Empty;
            this.WebCredentials.Key2 = String.Empty;
            this.WebCredentials.KeyData = new byte[] { };
            this.WebCredentials.OldProto = false;
            this.WebCredentials.Host = String.Empty;
            this.WebCredentials.Origin = String.Empty;
            this.Time = time;
            this.ProtoConnected = false;
            this.ExternalIP = ((IPEndPoint)this.Sock.RemoteEndPoint).Address;
            this.LocalEP = (IPEndPoint)this.Sock.LocalEndPoint;
            this._vroom = 0;
            this.Version = String.Empty;
            this.IgnoreList = new List<String>();
            this.CustomClientTags = new List<String>();
            this.Encryption = new Encryption { Mode = EncryptionMode.Unencrypted };
            this.CaptchaWord = String.Empty;
            this.Captcha = !Settings.Get<bool>("captcha");
            this.DNS = client.DNS;
            this.JoinTime = Helpers.UnixTime;
            this.FloodRecord = new FloodRecord();
            this.Font = new AresFont();
            this.OrgAvatar = new byte[] { };
            // vvvvvvvvvvvvvvvvvv
            this.Extended = true;
        }

        public void SendHTML(String text) {
        }
        public void Scribble(String sender, String url)
        {
            if (!this.Extended || !this.ProtoConnected)
                return;
            this.QueuePacket(WebOutbound.ScribbleGifTo(this,sender, url));
        }
        public void SetLevel(ILevel level)
        {
            if (!this.LoggedIn)
                return;

            this.Registered = true;

            if (this.Quarantined)
                this.Unquarantine();

            this.Captcha = true;
            this.Level = level;
        }

        public void Scribble(String sender, byte[] img, int h)
        {

            if (!this.Extended || !this.ProtoConnected)
                return;

            byte[] buf = Zip.Decompress(img);
            String height = h.ToString();
            String base64 = Convert.ToBase64String(buf);

            int total = (base64.Length + 1023) / 1024;

            this.QueuePacket(WebOutbound.ScribbleHead(this, sender, total, height));

            for (int i = 0; i < total; i++)
            {
                int off = i * 1024;
                int len = Math.Min(1024, base64.Length - off);
                this.QueuePacket(WebOutbound.ScribbleBlock(this, base64.Substring(off, len)));
            }
        }
        public void Scribble2(String sender, String img, int h)
        {
            if (!this.Extended || !this.ProtoConnected)
                return;

            String height = h.ToString();
            String base64 = img;

            int total = (base64.Length + 29999) / 30000;

            this.QueuePacket(WebOutbound.ScribbleHead(this, sender, total, height));

            for (int i = 0; i < total; i++)
            {
                int off = i * 30000;
                int len = Math.Min(30000, base64.Length - off);
                this.QueuePacket(WebOutbound.ScribbleBlock(this, base64.Substring(off, len)));
            }
        }
        public void PmScribble(String sender, String img, int h)
        {
            if (!this.Extended || !this.ProtoConnected)
                return;
            if (!(this.IsInbizierMobile || this.IsInbizierWeb))
                return;
            String height = h.ToString();
            String base64 = img;

            int total = (base64.Length + 29999) / 30000;

            this.QueuePacket(WebOutbound.PmScribbleHead(this, sender, total, height));

            for (int i = 0; i < total; i++)
            {
                int off = i * 30000;
                int len = Math.Min(30000, base64.Length - off);
                this.QueuePacket(WebOutbound.PmScribbleBlock(this, sender, base64.Substring(off, len)));
            }
        }

        public void Scribble2(String sender, String img)
        {
            if (!this.Extended || !this.ProtoConnected)
                return;

            String base64 = img;

            int total = (base64.Length + 1023) / 1024;

            this.QueuePacket(WebOutbound.ScribbleSourceHead(this, sender, total));

            for (int i = 0; i < total; i++)
            {
                int off = i * 1024;
                int len = Math.Min(1024, base64.Length - off);
                this.QueuePacket(WebOutbound.ScribbleSourceBlock(this, base64.Substring(off, len)));
            }
        }
        public void Audio(String sender, String base64)
        {
            if (!this.Extended || !this.ProtoConnected)
                return;

            int total = (base64.Length + 29999) / 30000;

            this.QueuePacket(WebOutbound.AudioHead(this, sender, total));

            for (int i = 0; i < total; i++)
            {
                int off = i * 30000;
                int len = Math.Min(30000, base64.Length - off);
                this.QueuePacket(WebOutbound.AudioBlock(this, base64.Substring(off, len)));
            }
        }
        public void PmAudio(String sender, String base64)
        {
            if (!this.Extended || !this.ProtoConnected)
                return;

            if (!(this.IsInbizierMobile || this.IsInbizierWeb))
                return;

            int total = (base64.Length + 29999) / 30000;

            this.QueuePacket(WebOutbound.PmAudioHead(this, sender, total));

            for (int i = 0; i < total; i++)
            {
                int off = i * 30000;
                int len = Math.Min(30000, base64.Length - off);
                this.QueuePacket(WebOutbound.PmAudioBlock(this, sender, base64.Substring(off, len)));
            }
        }
        public void Nudge(String sender) { }

        private bool _muzzled;
        public bool Muzzled
        {
            get { return this._muzzled; }
            set
            {
                this._muzzled = value;

                if (ServerCore.Linker.Busy && this.LoggedIn && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                    ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafUserUpdated(ServerCore.Linker, this));
            }
        }

        private bool _registered;
        public bool Registered
        {
            get { return this._registered; }
            set
            {
                this._registered = value;

                if (ServerCore.Linker.Busy && this.LoggedIn && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                    ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafUserUpdated(ServerCore.Linker, this));
            }
        }

        private bool _idled;
        public bool Idled
        {
            get { return this._idled; }
            set
            {
                this._idled = value;

                if (ServerCore.Linker.Busy && this.LoggedIn && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                    ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafUserUpdated(ServerCore.Linker, this));
            }
        }

        public bool Idle { get { return this.Idled; } }

        public void Release()
        {
            this.Unquarantine();
        }
        
        public void Unquarantine()
        {
            this.LoggedIn = false;
            this.Quarantined = false;
            Helpers.FakeRejoinSequence(this, true);

            if (ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafJoin(ServerCore.Linker, this));
        }

        public String CustomName
        {
            get
            {
                if (!Settings.Get<bool>("customnames"))
                    return String.Empty;

                return this._customname;
            }
            set
            {
                this._customname = value == null ? String.Empty : value;

                if (ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                    ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafCustomName(ServerCore.Linker, this));
            }
        }

        public void Ban()
        {
            if (!this.Owner)
                BanSystem.AddBan(this);

            this.Disconnect();
        }

        public void PM(String sender, String text)
        {
            if (!this.Extended)
                return;

            int len = Encoding.UTF8.GetByteCount(sender);

            if (len >= 2 && len <= 20)
            {
                len = Encoding.UTF8.GetByteCount(text);

                if (len > 0 && len <= 300)
                    this.QueuePacket(WebOutbound.PrivateTo(this, sender, text));
            }
        }

        public void Redirect(String hashlink) {
            //redirect only to inbizio users
            if (!(this.IsInbizierMobile || this.IsInbizierWeb))
            {
                return;
            }
            Room room = Hashlink.DecodeHashlink(hashlink);

            if (room != null && this.Level == ILevel.Regular)
            {
                byte[] buf;

                while (this.data_out.Count > 0)
                    if (!this.data_out.TryDequeue(out buf))
                        break;
                
                this.QueuePacket(WebOutbound.Redirect(this,room));
                this.Disconnect();
            }
        }
        public void RestoreAvatar()
        {
            if (this.rest_av != null)
                this.OrgAvatar = this.rest_av;

            this.Avatar = this.OrgAvatar;
        }

        public void SendEmote(String text)
        {
            if (!String.IsNullOrEmpty(text) && !this.Quarantined)
            {
                UserPool.AUsers.ForEachWhere(x => x.SendPacket(TCPOutbound.Emote(x, this.Name, text)),
                    x => x.LoggedIn && x.Vroom == this.Vroom && !x.IgnoreList.Contains(this.Name) && !x.Quarantined);

                UserPool.WUsers.ForEachWhere(x => x.QueuePacket(ib0t.WebOutbound.EmoteTo(x, this.Name, text)),
                    x => x.LoggedIn && x.Vroom == this.Vroom && !x.IgnoreList.Contains(this.Name) && !x.Quarantined);

                if (ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                    ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafEmoteText(ServerCore.Linker, this.Name, text));
            }
        }

        public void SendText(String text)
        {
            if (!String.IsNullOrEmpty(text) && !this.Quarantined)
            {
                UserPool.AUsers.ForEachWhere(x => x.SendPacket((String.IsNullOrEmpty(this.CustomName) || x.BlockCustomNames) ?
                    TCPOutbound.Public(x, this.Name, text) : TCPOutbound.NoSuch(x, this.CustomName + text)),
                    x => x.LoggedIn && x.Vroom == this.Vroom && !x.IgnoreList.Contains(this.Name) && !x.Quarantined);

                UserPool.WUsers.ForEachWhere(x => x.QueuePacket(String.IsNullOrEmpty(this.CustomName) ?
                    ib0t.WebOutbound.PublicTo(x, this.Name, text) : ib0t.WebOutbound.NoSuchTo(x, this.CustomName + text)),
                    x => x.LoggedIn && x.Vroom == this.Vroom && !x.IgnoreList.Contains(this.Name) && !x.Quarantined);

                LinkLeaf.LeafOutbound.RelayPublicText(this.Name, this.CustomName, text);
            }
        }

        public void Topic(String text)
        {
            if (text != null && this.ProtoConnected)
                if (Encoding.UTF8.GetByteCount(text) <= 500)
                    this.QueuePacket(WebOutbound.TopicFirstTo(this, text));
        }

        public void URL(String address, String text)
        {
            if (this.ProtoConnected)
                if (address != null && text != null)
                    this.QueuePacket(WebOutbound.UrlTo(this, address, text));
        }

        public IUser IUser
        {
            get { return this; }
        }

        public bool Connected
        {
            get { return this.SocketConnected; }
        }

        public bool Encrypted
        {
            get { return false; }
        }

        public bool Cloaked
        {
            get { return false; }
            set { }
        }

        public String Name
        {
            get { return this._name; }
            set
            {
                if (this.SocketConnected && Helpers.NameAvailable(this, value))
                    if (!this.LoggedIn)
                        this._name = value;
                    else
                    {
                        if (this.Quarantined)
                            return;

                        this.LoggedIn = false;
                        LinkLeaf.LinkUser other = null;

                        if (ServerCore.Linker.Busy)
                            foreach (LinkLeaf.Leaf leaf in ServerCore.Linker.Leaves)
                            {
                                other = leaf.Users.Find(x => x.Vroom == this.Vroom && x.Name == this.Name && !x.Link.Visible);

                                if (other != null)
                                {
                                    other.LinkCredentials.Visible = true;
                                    break;
                                }
                            }

                        UserPool.AUsers.ForEachWhere(x => x.SendPacket(other == null ? TCPOutbound.Part(x, this) : TCPOutbound.UpdateUserStatus(x, other)),
                            x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                        UserPool.WUsers.ForEachWhere(x => x.QueuePacket(other == null ? ib0t.WebOutbound.PartTo(x, this.Name) : ib0t.WebOutbound.UpdateTo(x, other.Name, other.Level)),
                            x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                        this._name = value;
                        Helpers.FakeRejoinSequence(this, false);
                    }
            }
        }

        public ushort Vroom
        {
            get { return this._vroom; }
            set
            {
                if (this.SocketConnected)
                    if (Events.VroomChanging(this, value))
                    {
                        if (!this.LoggedIn)
                            this._vroom = value;
                        else
                        {
                            if (this.Quarantined)
                                return;

                            this.LoggedIn = false;
                            LinkLeaf.LinkUser other = null;

                            if (ServerCore.Linker.Busy)
                                foreach (LinkLeaf.Leaf leaf in ServerCore.Linker.Leaves)
                                {
                                    other = leaf.Users.Find(x => x.Vroom == this.Vroom && x.Name == this.Name && !x.Link.Visible);

                                    if (other != null)
                                    {
                                        other.LinkCredentials.Visible = true;
                                        break;
                                    }
                                }

                            UserPool.AUsers.ForEachWhere(x => x.SendPacket(other == null ? TCPOutbound.Part(x, this) : TCPOutbound.UpdateUserStatus(x, other)),
                                x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                            UserPool.WUsers.ForEachWhere(x => x.QueuePacket(other == null ? ib0t.WebOutbound.PartTo(x, this.Name) : ib0t.WebOutbound.UpdateTo(x, other.Name, other.Level)),
                                x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                            this._vroom = value;
                            Helpers.FakeRejoinSequence(this, false);
                        }

                        Events.VroomChanged(this);
                    }
            }
        }

        public ILevel Level
        {
            get { return this._level; }
            set
            {
                if (value != this._level)
                {
                    this._level = value;

                    if (this.LoggedIn)
                    {
                        UserPool.AUsers.ForEachWhere(x => x.SendPacket(TCPOutbound.UpdateUserStatus(x, this)),
                            x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                        UserPool.WUsers.ForEachWhere(x => x.QueuePacket(WebOutbound.UpdateTo(x, this.Name, this._level)),
                            x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                        if (ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                            ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafUserUpdated(ServerCore.Linker, this));
                    }

                    Events.AdminLevelChanged(this);
                }
            }
        }

        public void Print(object text)
        {
            if (this.ProtoConnected)
                this.QueuePacket(WebOutbound.NoSuchTo(this, text.ToString()));
        }

        public String _pmsg = String.Empty;
        public String PersonalMessage
        {
            get { return this._pmsg; }
            set {
                if (value == null)
                {
                    value = String.Empty;
                }
                else
                {
                    this._pmsg = value;
                }
            }
        }
        private byte[] rest_av = null;
        public byte[] Avatar
        {
            get { 
                if(this.avatar==null||this.avatar.Length==0)
                    return Resource1.web;
                return this.avatar; }
            set
            {
                if (value == null)
                {
                    value = new byte[] { };

                    if (this.avatar != null)
                        if (this.avatar.Length >= 10)
                            this.rest_av = this.avatar;
                }

                if (value.Length < 10)
                {
                    this.avatar = new byte[] { };
                    this.AvatarReceived = false;
                }
                else
                {
                    this.avatar = value;
                }
            }
        }
        private byte[] fullavatar = null;
        public byte[] FullAvatar
        {
            get
            {
                if (this.fullavatar == null||this.fullavatar.Length==0)
                    return Resource1.web;
                return this.fullavatar;
            }
            set
            {
                if (value == null)
                {
                    value = new byte[] { };

                    if (this.fullavatar != null)
                        if (this.fullavatar.Length >= 10)
                            this.rest_av = this.fullavatar;
                }

                if (value.Length < 10)
                {
                    this.fullavatar = new byte[] { };
                    this.AvatarReceived = false;
                }
                else
                {
                    this.fullavatar = value;
                }
            }
        }
        public byte[] Scale(byte[] raw)
        {
            byte[] result = null;

            try
            {
                result = ImageUtils.ScaleJpeg(raw, 48, 48);
            }
            catch
            {

            }
            return result;
        }
        public void BinaryWrite(byte[] data)
        {
            this.QueuePacket(data);
        }

        public void Disconnect()
        {
            while (this.data_out.Count > 0)
            {
                try
                {
                    byte[] packet;

                    if (this.data_out.TryPeek(out packet))
                    {
                        this.Sock.Send(packet);
                        Stats.DataSent += (ulong)packet.Length;

                        while (!this.data_out.TryDequeue(out packet))
                            continue;
                    }
                    else break;
                }
                catch { break; }
            }

            try { this.Sock.Disconnect(false); }
            catch { }
            try { this.Sock.Shutdown(SocketShutdown.Both); }
            catch { }
            try { this.Sock.Close(); }
            catch { }
            try { this.Sock.Dispose(); }
            catch { }

            this.SocketConnected = false;
            this.SendDepart();
            this.LoggedIn = false;
        }

        public void SendDepart()
        {
            if (this.LoggedIn && !this.Quarantined)
            {
                this.LoggedIn = false;
                Events.Parting(this);

                LinkLeaf.LinkUser other = null;

                if (ServerCore.Linker.Busy)
                    foreach (LinkLeaf.Leaf leaf in ServerCore.Linker.Leaves)
                    {
                        other = leaf.Users.Find(x => x.Vroom == this.Vroom && x.Name == this.Name && !x.Link.Visible);

                        if (other != null)
                        {
                            other.LinkCredentials.Visible = true;
                            break;
                        }
                    }

                UserPool.AUsers.ForEachWhere(x => x.SendPacket(other == null ? TCPOutbound.Part(x, this) : TCPOutbound.UpdateUserStatus(x, other)),
                    x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                UserPool.WUsers.ForEachWhere(x => x.QueuePacket(other == null ? ib0t.WebOutbound.PartTo(x, this.Name) : ib0t.WebOutbound.UpdateTo(x, other.Name, other.Level)),
                    x => x.LoggedIn && x.Vroom == this.Vroom && !x.Quarantined);

                if (ServerCore.Linker.Busy && ServerCore.Linker.LoginPhase == LinkLeaf.LinkLogin.Ready)
                    ServerCore.Linker.SendPacket(LinkLeaf.LeafOutbound.LeafPart(ServerCore.Linker, this));

                Events.Parted(this);
            }
        }

        public void QueuePacket(byte[] data)
        {
            this.data_out.Enqueue(data);
        }

        public bool SocketConnected
        {
            get { return this.socket_health < 10; }
            set { this.socket_health = value ? 0 : 10; }
        }

        private byte[] out_scratch = new byte[32768];
        private byte[] out_pending = null;
        private int out_pending_offset = 0;

        private bool FlushPending()
        {
            if (this.out_pending == null)
                return true;

            while (this.out_pending_offset < this.out_pending.Length)
            {
                try
                {
                    int sent = this.Sock.Send(this.out_pending, this.out_pending_offset,
                        this.out_pending.Length - this.out_pending_offset, SocketFlags.None);

                    if (sent <= 0)
                        return false;

                    Stats.DataSent += (ulong)sent;
                    this.out_pending_offset += sent;
                }
                catch { return false; }
            }

            this.out_pending = null;
            this.out_pending_offset = 0;
            return true;
        }

        private void FlushSend()
        {
            if (!this.FlushPending())
                return;

            while (this.data_out.Count > 0)
            {
                byte[] packet;
                int count = 0;

                while (this.data_out.Count > 0 && this.data_out.TryPeek(out packet))
                {
                    if (packet == null)
                    {
                        this.data_out.TryDequeue(out packet);
                        continue;
                    }

                    if (packet.Length > this.out_scratch.Length || count + packet.Length > this.out_scratch.Length)
                        break;

                    this.data_out.TryDequeue(out packet);
                    Buffer.BlockCopy(packet, 0, this.out_scratch, count, packet.Length);
                    count += packet.Length;
                }

                if (count == 0)
                {
                    if (!this.data_out.TryDequeue(out packet) || packet == null)
                        break;

                    this.out_pending = packet;
                    this.out_pending_offset = 0;

                    if (!this.FlushPending())
                        return;

                    continue;
                }

                int offset = 0;

                while (offset < count)
                {
                    try
                    {
                        int sent = this.Sock.Send(this.out_scratch, offset, count - offset, SocketFlags.None);

                        if (sent <= 0)
                            break;

                        Stats.DataSent += (ulong)sent;
                        offset += sent;
                    }
                    catch { break; }
                }

                if (offset < count)
                {
                    this.out_pending = new byte[count - offset];
                    Buffer.BlockCopy(this.out_scratch, offset, this.out_pending, 0, count - offset);
                    this.out_pending_offset = 0;
                    return;
                }
            }
        }

        public void SendReceive()
        {
            this.FlushSend();

            byte[] buffer = this.recv_buffer;
            int received = 0;
            SocketError e = SocketError.Success;

            try { received = this.Sock.Receive(buffer, 0, buffer.Length, SocketFlags.None, out e); }
            catch { }

            if (received == 0)
                this.socket_health = e == SocketError.WouldBlock ? 0 : (this.socket_health + 1);
            else
            {
                this.socket_health = 0;
                this.data_in.AddRange(new ArraySegment<byte>(buffer, 0, received));
                Stats.DataReceived += (ulong)received;

                // Guard against a client flooding the buffer without ever
                // completing a parseable frame. Drop the excess data instead
                // of disconnecting the user.
                if (this.data_in.Count > MAX_BUFFER_SIZE)
                    this.data_in.Clear();
            }
        }

        private bool pending_keydata = false;

        internal bool LoadProtocol()
        {
            if (this.pending_keydata)
            {
                if (this.data_in.Count >= 8)
                {
                    this.WebCredentials.KeyData = this.data_in.GetRange(0, 8);
                    this.data_in.RemoveRange(0, 8);
                    return true;
                }
            }
            else
            {
                String forwardedForIP = null;
                String realIP = null;

                while (this.data_in.CanTakeLine())
                {
                    String str = this.data_in.TakeLine();
                    String up = str.ToUpper();

                    if (up.StartsWith("SEC-WEBSOCKET-KEY:"))
                    {
                        str = str.Substring(18);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        this.WebCredentials.Key = str;
                    }
                    else if (up.StartsWith("SEC-WEBSOCKET-KEY1:"))
                    {
                        str = str.Substring(19);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        this.WebCredentials.Key1 = str;
                    }
                    else if (up.StartsWith("SEC-WEBSOCKET-KEY2:"))
                    {
                        str = str.Substring(19);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        this.WebCredentials.Key2 = str;
                    }
                    else if (up.StartsWith("HOST:"))
                    {
                        str = str.Substring(5);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        this.WebCredentials.Host = str;
                    }
                    else if (up.StartsWith("ORIGIN:"))
                    {
                        str = str.Substring(7);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        this.WebCredentials.Origin = str;
                    }
                    else if (up.StartsWith("X-FORWARDED-FOR:"))
                    {
                        str = str.Substring(16);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        forwardedForIP = str;
                    }
                    else if (up.StartsWith("X-REAL-IP:"))
                    {
                        str = str.Substring(10);

                        if (str.StartsWith(" "))
                            str = str.Substring(1);

                        realIP = str;
                    }
                    else if (String.IsNullOrEmpty(str.Trim()))
                    {
                        this.ApplyForwardedIP(realIP, forwardedForIP);

                        if (this.WebCredentials.Key1.Length > 0)
                        {
                            this.pending_keydata = true;
                            this.WebCredentials.OldProto = true;
                        }
                        else return true;
                    }
                }
            }

            return false;
        }

        private void ApplyForwardedIP(String realIP, String forwardedForIP)
        {
            IPAddress remoteAddr = ((IPEndPoint)this.Sock.RemoteEndPoint).Address;

            if (!TrustedProxyManager.IsTrustedProxy(remoteAddr))
                return;

            IPAddress parsed;

            if (!String.IsNullOrEmpty(realIP) && IPAddress.TryParse(realIP.Trim(), out parsed))
            {
                this.ExternalIP = parsed;
                return;
            }

            if (!String.IsNullOrEmpty(forwardedForIP))
            {
                String first = forwardedForIP.Split(',')[0].Trim();

                if (IPAddress.TryParse(first, out parsed))
                    this.ExternalIP = parsed;
            }
        }

        internal String NextMessage
        {
            get { return this.WebCredentials.OldProto ? this.CheckPacketOld() : this.CheckPacketNew(); }
        }

        private String CheckPacketOld()
        {
            if (this.data_in.Count >= 2)
            {
                int index = this.data_in.IndexOf(255);

                if (index > 0)
                {
                    if (index > MAX_MESSAGE_SIZE)
                    {
                        // Drop the oversized message without disconnecting.
                        this.data_in.RemoveRange(0, (index + 1));
                        return null;
                    }

                    byte[] buffer = new byte[index - 1];
                    this.data_in.CopyTo(1, buffer, 0, index - 1);
                    this.data_in.RemoveRange(0, (index + 1));
                    return Encoding.UTF8.GetString(buffer);
                }
            }

            return null;
        }

        private String CheckPacketNew()
        {
            if (this.data_in.Count >= 6)
            {
                int max_size = this.data_in.Count;
                byte msg = this.data_in[0];
                int len = (this.data_in[1] & 127);
                byte[] buffer;

                if (len == 126)
                {
                    if (max_size < 8)
                        return null;

                    len = (this.data_in[2] << 8) | this.data_in[3];

                    if (max_size < (8 + len))
                        return null;

                    this.data_in.RemoveRange(0, 4);
                }
                else if (len == 127)
                {
                    if (max_size < 14)
                        return null;

                    buffer = new byte[8];
                    this.data_in.CopyTo(2, buffer, 0, 8);
                    Array.Reverse(buffer);
                    ulong len64 = BitConverter.ToUInt64(buffer, 0);

                    // Reject absurdly large frames before casting to int to
                    // avoid an overflow making the size check pass. Drop the
                    // oversized frame without disconnecting the user.
                    if (len64 > MAX_MESSAGE_SIZE)
                    {
                        this.data_in.Clear();
                        return null;
                    }

                    len = (int)len64;

                    if (max_size < (14 + len))
                        return null;

                    this.data_in.RemoveRange(0, 10);
                }
                else
                {
                    if (max_size < (6 + len))
                        return null;

                    this.data_in.RemoveRange(0, 2);
                }
                if (this.data_in.Count >= (len + 4))
                {
                    byte[] key = new byte[4];
                    this.data_in.CopyTo(0, key, 0, 4);
                    byte[] text = new byte[len];
                    this.data_in.CopyTo(4, text, 0, len);
                    this.data_in.RemoveRange(0, (len + 4));

                    for (int i = 0; i < text.Length; i++)
                        text[i] ^= key[i % 4];

                    switch (msg)
                    {
                        case 136:
                            this.SocketConnected = false;
                            return null;

                        case 129:
                            return Encoding.UTF8.GetString(text);

                        default:
                            return null;
                    }
                }
            }

            return null;
        }

        public uint GetASN()
        {
            if (asn != 0)
                return asn;

            asn = AsnLookup.Get(OriginalIP);

            return asn;
        }
    }
}
