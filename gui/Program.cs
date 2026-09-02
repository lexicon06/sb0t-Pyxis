using System;
using Avalonia;

namespace gui
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Fuerza la identidad de datos a "sb0t" para que la carpeta de
            // datos sea siempre %APPDATA%/sb0t/sb0t (si no, depende del nombre
            // del ejecutable, ej. sb0t.exe).
            iconnect.SettingsStore.Identity = "sb0t";
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sb0t-startup.log"),
                    DateTime.Now + " Program.Main Identity=" + iconnect.SettingsStore.Identity
                    + " Effective=" + core.DataPaths.Root + Environment.NewLine);
            }
            catch { }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect();
        }
    }
}
