using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace gui
{
    public partial class App : Application
    {
        private static string _theme = "light";

        public override void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                string msg = "UNHANDLED EXCEPTION: " + (ex?.ToString() ?? "unknown");
                try
                {
                    string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sb0t-startup.log");
                    System.IO.File.AppendAllText(path, DateTime.Now + " " + msg + Environment.NewLine);
                }
                catch { }
                try
                {
                    core.ServerLog.Error("UNHANDLED EXCEPTION", ex);
                }
                catch { }
            };
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Log("OnFrameworkInitializationCompleted start");
            try
            {
                string theme = "light";
                try
                {
                    string saved = core.Settings.Get<string>("theme");
                    if (!String.IsNullOrEmpty(saved))
                        theme = saved;
                }
                catch { }
                ApplyTheme(theme);
                Log("ApplyTheme done theme=" + theme);

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var win = new MainWindow();
                    desktop.MainWindow = win;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    Log("MainWindow created");
                    if (Environment.GetEnvironmentVariable("SB0T_SHOT") == "1")
                    {
                        win.Opened += async (s, e) =>
                        {
                            await System.Threading.Tasks.Task.Delay(1500);
                            try
                            {
                                win.UpdateLayout();
                                var px = new Avalonia.PixelSize(Math.Max(1, (int)win.ClientSize.Width), Math.Max(1, (int)win.ClientSize.Height));
                                var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(px);
                                rtb.Render(win);
                                var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "opencode");
                                System.IO.Directory.CreateDirectory(dir);
                                var path = System.IO.Path.Combine(dir, "sb0t_light.png");
                                using (var fs = System.IO.File.Create(path))
                                    rtb.Save(fs);
                                Log("SHOT saved to " + path);
                                // also try dark
                                ApplyTheme("dark");
                                await System.Threading.Tasks.Task.Delay(800);
                                win.UpdateLayout();
                                var rtb2 = new Avalonia.Media.Imaging.RenderTargetBitmap(px);
                                rtb2.Render(win);
                                var path2 = System.IO.Path.Combine(dir, "sb0t_dark.png");
                                using (var fs = System.IO.File.Create(path2))
                                    rtb2.Save(fs);
                                Log("SHOT dark saved to " + path2);
                                ApplyTheme("light");
                                Environment.Exit(0);
                            }
                            catch (Exception ex) { Log("SHOT EX " + ex); Environment.Exit(1); }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Log("EXCEPTION: " + ex);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void Log(string msg)
        {
            try
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sb0t-startup.log");
                System.IO.File.AppendAllText(path, DateTime.Now + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        public static void ApplyTheme(string theme)
        {
            if (Application.Current == null)
                return;

            _theme = theme == "dark" ? "dark" : "light";
            bool dark = _theme == "dark";

            var res = Application.Current.Resources;
            res["AppBackground"] = GetBrush(res, dark ? "DarkAppBackground" : "LightAppBackground");
            res["AppCardBackground"] = GetBrush(res, dark ? "DarkAppCardBackground" : "LightAppCardBackground");
            res["AppAccent"] = GetBrush(res, dark ? "DarkAppAccent" : "LightAppAccent");
            res["AppAccentHover"] = GetBrush(res, dark ? "DarkAppAccentHover" : "LightAppAccentHover");
            res["AppAccentPressed"] = GetBrush(res, dark ? "DarkAppAccentPressed" : "LightAppAccentPressed");
            res["AppSuccess"] = GetBrush(res, dark ? "DarkAppSuccess" : "LightAppSuccess");
            res["AppError"] = GetBrush(res, dark ? "DarkAppError" : "LightAppError");
            res["AppText"] = GetBrush(res, dark ? "DarkAppText" : "LightAppText");
            res["AppTextSecondary"] = GetBrush(res, dark ? "DarkAppTextSecondary" : "LightAppTextSecondary");
            res["AppBorder"] = GetBrush(res, dark ? "DarkAppBorder" : "LightAppBorder");
            res["AppCardBorder"] = GetBrush(res, dark ? "DarkAppCardBorder" : "LightAppCardBorder");
            res["AppHeaderBackground"] = GetBrush(res, dark ? "DarkAppHeaderBackground" : "LightAppHeaderBackground");
            res["AppStatusBarBackground"] = GetBrush(res, dark ? "DarkAppStatusBarBackground" : "LightAppStatusBarBackground");
            res["AppSelection"] = GetBrush(res, dark ? "DarkAppSelection" : "LightAppSelection");

            Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public static string CurrentTheme { get { return _theme; } }

        private static IBrush GetBrush(IResourceDictionary res, string key)
        {
            object o;
            if (res.TryGetResource(key, null, out o) && o is IBrush)
                return (IBrush)o;
            return Brushes.Transparent;
        }
    }
}
