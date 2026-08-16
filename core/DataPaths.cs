using System;
using System.IO;
using iconnect;

namespace core
{
    /// <summary>
    /// Central helper for the shared application data folder. Uses the same
    /// effective identity as SettingsStore so the desktop GUI and the web panel
    /// read/write the exact same files (Dat, Extensions, Avatars, logs, ...).
    /// </summary>
    public static class DataPaths
    {
        public static String Root
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "sb0t",
                    SettingsStore.EffectiveIdentity);
            }
        }

        public static String Dat
        {
            get
            {
                String dir = Path.Combine(Root, "Dat");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static String Extensions
        {
            get
            {
                String dir = Path.Combine(Root, "Extensions");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static String Avatars
        {
            get
            {
                String dir = Path.Combine(Root, "Avatars");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static String ChatLogs
        {
            get
            {
                String dir = Path.Combine(Root, "ChatLogs");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
