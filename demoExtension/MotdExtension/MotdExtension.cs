using System;
using System.IO;
using iconnect;
using SixLabors.ImageSharp;

namespace MotdExtension
{
    /// <summary>
    /// Demo DLL extension for sb0t Pyxis.
    /// Shows a "message of the day" image (scribble) to every user when they join.
    ///
    /// Deploy: put a file named "motd.jpg" (or motd.png) inside the extension data
    /// folder:  <Root>\Extensions\MotdExtension\
    /// The host creates that folder automatically and host.DataPath points to it.
    /// </summary>
    public class MotdExtension : IExtension
    {
        private readonly IHostApp host;
        private byte[] imageData;
        private int imageHeight;

        public MotdExtension(IHostApp host)
        {
            this.host = host;

            // host.DataPath = "<Root>\Extensions\MotdExtension\"
            string imagePath = Path.Combine(host.DataPath, "motd.jpg");

            if (File.Exists(imagePath))
            {
                // Read the raw image bytes once and reuse for every join.
                this.imageData = File.ReadAllBytes(imagePath);

                // Cross-platform way to read the real image height
                // (replaces the old System.Drawing.Bitmap which is Windows-only).
                try
                {
                    using (var img = SixLabors.ImageSharp.Image.Load(this.imageData))
                        this.imageHeight = img.Height;
                }
                catch
                {
                    this.imageHeight = 300; // fallback if we cannot decode it
                }

                host.WriteLog("MotdExtension: loaded motd.jpg (" + this.imageHeight + "px)");
            }
            else
            {
                host.WriteLog("MotdExtension: motd.jpg not found in " + host.DataPath);
            }
        }

        public void Joined(IUser client)
        {
            if (this.imageData == null || client == null)
                return;

            try
            {
                // Send the scribble directly through the IUser interface.
                // (replaces the old reflection call to client.Scribble2)
                client.Scribble(host.Room.BotName ?? "Server", this.imageData, this.imageHeight);
            }
            catch (Exception ex)
            {
                host.WriteLog("MotdExtension error: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Required IExtension members (the rest are unused by this demo).
        // ------------------------------------------------------------------

        public void ServerStarted() { }
        public void CycleTick() { }
        public void UnhandledProtocol(IUser client, bool custom, byte msg, byte[] packet) { }
        public bool Joining(IUser client) { return true; }
        public void Rejected(IUser client, RejectedMsg msg) { }
        public void Parting(IUser client) { }
        public void Parted(IUser client) { }
        public bool AvatarReceived(IUser client) { return true; }
        public bool PersonalMessageReceived(IUser client, String text) { return true; }
        public void TextReceived(IUser client, String text) { }
        public String TextSending(IUser client, String text) { return text; }
        public void TextSent(IUser client, String text) { }
        public void EmoteReceived(IUser client, String text) { }
        public String EmoteSending(IUser client, String text) { return text; }
        public void EmoteSent(IUser client, String text) { }
        public void PrivateSending(IUser client, IUser target, IPrivateMsg msg) { }
        public void PrivateSent(IUser client, IUser target) { }
        public void BotPrivateSent(IUser client, String text) { }
        public void Command(IUser client, String command, IUser target, String args) { }
        public bool Nick(IUser client, String name) { return true; }
        public void Help(IUser client) { }
        public void FileReceived(IUser client, String filename, String title, MimeType type) { }
        public bool Ignoring(IUser client, IUser target) { return true; }
        public void IgnoredStateChanged(IUser client, IUser target, bool ignored) { }
        public void InvalidLoginAttempt(IUser client) { }
        public void LoginGranted(IUser client) { }
        public void AdminLevelChanged(IUser client) { }
        public void InvalidRegistration(IUser client) { }
        public bool Registering(IUser client) { return true; }
        public void Registered(IUser client) { }
        public void Unregistered(IUser client) { }
        public void CaptchaSending(IUser client) { }
        public void CaptchaReply(IUser client, String reply) { }
        public bool VroomChanging(IUser client, ushort vroom) { return true; }
        public void VroomChanged(IUser client) { }
        public bool Flooding(IUser client, byte msg) { return false; }
        public void Flooded(IUser client) { }
        public bool ProxyDetected(IUser client) { return false; }
        public void Logout(IUser client) { }
        public void Idled(IUser client) { }
        public void Unidled(IUser client, uint seconds_away) { }
        public void BansAutoCleared() { }
        public void Dispose() { }
        public void Load() { }
        public void LinkError(ILinkError error) { }
        public void Linked() { }
        public void Unlinked() { }
        public void LeafJoined(ILeaf leaf) { }
        public void LeafParted(ILeaf leaf) { }
        public void LinkedAdminDisabled(ILeaf leaf, IUser client) { }

        // In sb0t Pyxis these properties are typed as "object" (no WPF).
        // Returning null is fine; a custom settings tab would use Avalonia controls.
        public object Icon { get { return null; } }
        public object GUI { get { return null; } }
    }
}
