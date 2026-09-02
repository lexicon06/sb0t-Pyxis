using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using core;
using gui.Models;
using gui.Services;

namespace gui
{
    public partial class MainWindow : Window
    {
        public static bool SETTING_UP { get; set; }

        private bool do_once = true;
        private ServerCore server { get; set; }
        private bool _hidden = false;
        private bool _running = false;
        private static Mutex mutex;
        private Services.TrayIcon tray;
        private System.Collections.ObjectModel.ObservableCollection<CommandItem> _commands = new System.Collections.ObjectModel.ObservableCollection<CommandItem>();
        private DispatcherTimer _statusTimer;
        private bool _externalRunning = false;

        public MainWindow()
        {
            InitializeComponent();

            bool first;
            mutex = new Mutex(true, AppDomain.CurrentDomain.FriendlyName, out first);

            if (!first)
            {
                System.Console.WriteLine("sb0t is already running.");
                Environment.Exit(-1);
            }

            SETTING_UP = true;

            this.server = new ServerCore();
            ServerCore.LogUpdate += this.LogUpdate;
            GUILabels.Setup(this);

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(2);
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            this.listView1.ItemsSource = _commands;

            this.tray = new Services.TrayIcon(this.TryGetPlatformHandle().Handle);
            this.tray.LeftClicked += () =>
            {
                if (this._hidden)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this._hidden = false;
                }
            };
            this.tray.Show("sb0t");

            ApplySavedLanguage();
        }

        private void ApplySavedLanguage()
        {
            if (GUILabels.IsSpanish)
                GUILabels.SetSpanish(this);
            else
                GUILabels.SetEnglish(this);
        }

        private void Window_Opened(object sender, EventArgs e)
        {
            if (this.do_once)
            {
                this.do_once = false;
                this.SetupValues();
                SETTING_UP = false;
            }
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            // Close directly. The old confirmation used ThingLing.Controls.MessageBox
            // which is compiled against Avalonia 0.10 and throws with Avalonia 11,
            // leaving the server stuck and the status stale. Allow the window to
            // close so Window_Closed performs a clean server shutdown.
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (this.server.Running)
                this.server.Close();
            this.tray.Dispose();
            Environment.Exit(-1);
        }

        private void LogUpdate(object sender, ServerLogEventArgs e)
        {
            String str = "";

            if (!String.IsNullOrEmpty(e.Message))
                str = e.Message;

            if (e.Error != null)
                str += "\r\n" + e.Error.Message + "\r\n" + e.Error.StackTrace;

            try
            {
                String path = core.DataPaths.Root;

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                path += "\\serverlog.txt";

                using (StreamWriter writer = File.Exists(path) ? File.AppendText(path) : File.CreateText(path))
                    writer.WriteLine(DateTime.UtcNow + " " + str);
            }
            catch { }
        }

        private void RunMode(bool running)
        {
            this._running = running;
            this.textBox1.IsEnabled = !running;
            this.textBox2.IsEnabled = !running;
            this.textBox3.IsEnabled = !running;
            this.textBox5.IsEnabled = !running;
            this.textBox6.IsEnabled = !running;
            this.checkBox1.IsEnabled = !running;
            this.checkBox25.IsEnabled = !running;
            this.checkBox2.IsEnabled = !running;
            this.checkBox3.IsEnabled = !running;
            this.checkBox4.IsEnabled = !running;
            this.checkBox5.IsEnabled = !running;
            this.checkBox6.IsEnabled = !running;
            this.checkBox7.IsEnabled = !running;
            this.checkBox8.IsEnabled = !running;
            this.checkBox9.IsEnabled = !running;
            this.checkBox10.IsEnabled = !running;
            this.checkBox11.IsEnabled = !running;
            this.button3.IsEnabled = running;
            this.button4.IsEnabled = running;
            this.comboBox2.IsEnabled = running ? false : (bool)this.checkBox8.IsChecked;
            this.comboBox3.IsEnabled = !running;
            this.comboBox4.IsEnabled = !running;
            this.checkBox20.IsEnabled = !running;
            this.checkBox22.IsEnabled = !running;
            this.checkBox23.IsEnabled = !running;
            this.checkBox30.IsEnabled = !running;
            this.checkBox18.IsEnabled = !running;
        }

        private void ServerStartStop(object sender, RoutedEventArgs e)
        {
            if (_externalRunning)
            {
                // The server is running in the web panel: send a remote stop.
                core.ServerCore.WriteRemoteCommand("stop", null);
                return;
            }

            if (!_running)
            {
                if (this.server.Open())
                {
                    this.button1.Content = GUILabels.IsSpanish ? GUILabels.spanish["button1b"] : GUILabels.english["button1b"];
                    this.statusLabel.Text = GUILabels.IsSpanish ? GUILabels.spanish["statuslabelb"] : GUILabels.english["statuslabelb"];
                    this.statusLabel.Foreground = (IBrush)Application.Current.Resources["AppSuccess"];
                    this.RunMode(true);
                }
                else
                    ShowMessage(GUILabels.IsSpanish ? GUILabels.spanish["mboxa"] : GUILabels.english["mboxa"]);
            }
            else
            {
                this.server.Close();
                this.button1.Content = GUILabels.IsSpanish ? GUILabels.spanish["button1a"] : GUILabels.english["button1a"];
                this.statusLabel.Text = GUILabels.IsSpanish ? GUILabels.spanish["statuslabela"] : GUILabels.english["statuslabela"];
                this.statusLabel.Foreground = (IBrush)Application.Current.Resources["AppTextSecondary"];
                this.RunMode(false);
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            // Reflect the shared server status written by the web panel.
            var status = core.ServerCore.ReadServerStatus();
            bool external = status != null && status.running && !string.Equals(status.source, "gui", StringComparison.OrdinalIgnoreCase);

            if (external == _externalRunning)
                return;

            _externalRunning = external;

            if (external)
            {
                this.button1.Content = GUILabels.IsSpanish ? GUILabels.spanish["button1b"] : GUILabels.english["button1b"];
                this.statusLabel.Text = GUILabels.IsSpanish ? GUILabels.spanish["statuslabelb"] : GUILabels.english["statuslabelb"];
                this.statusLabel.Foreground = (IBrush)Application.Current.Resources["AppSuccess"];
                this.RunMode(true);
            }
            else if (!this.server.Running)
            {
                this.button1.Content = GUILabels.IsSpanish ? GUILabels.spanish["button1a"] : GUILabels.english["button1a"];
                this.statusLabel.Text = GUILabels.IsSpanish ? GUILabels.spanish["statuslabela"] : GUILabels.english["statuslabela"];
                this.statusLabel.Foreground = (IBrush)Application.Current.Resources["AppTextSecondary"];
                this.RunMode(false);
            }
        }

        private void TextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)sender;

            if (tb.Name == "textBox1")
            {
                Settings.Set("name", this.textBox1.Text);
                this.SetLinkIdent();
            }
            else if (tb.Name == "textBox2")
            {
                ushort u;

                if (ushort.TryParse(this.textBox2.Text, out u))
                    Settings.Set("port", u);
            }
            else if (tb.Name == "textBox3")
                Settings.Set("bot", this.textBox3.Text);
            else if (tb.Name == "textBox4" && textBox4.Text.Length > 0)
                Settings.Set("owner", this.textBox4.Text);
            else if (tb.Name == "textBox5")
            {
                Uri uri;

                if (Uri.TryCreate(this.textBox5.Text, UriKind.Absolute, out uri))
                    Settings.Set("url", this.textBox5.Text, "lse");
                if (String.IsNullOrEmpty(this.textBox5.Text))
                    this.textBox5.Text = "http://198.58.100.116:3000/api/v1";
            }
            else if (tb.Name == "textBox6")
            {
                IPAddress ip;

                if (IPAddress.TryParse(this.textBox6.Text, out ip))
                    Settings.Set("udp_address", ip.GetAddressBytes());
            }
        }

        private void CheckBoxChecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;

            if (cb.Name == "checkBox1")
                Settings.Set("logging", this.checkBox1.IsChecked);
            else if (cb.Name == "checkBox2")
                Settings.Set("can_room_scribble", this.checkBox2.IsChecked);
            else if (cb.Name == "checkBox3")
                Settings.Set("autostart", this.checkBox3.IsChecked);
            else if (cb.Name == "checkBox4")
                Settings.Set("start_min", this.checkBox4.IsChecked);
            else if (cb.Name == "checkBox5")
                Settings.Set("udp", this.checkBox5.IsChecked);
            else if (cb.Name == "checkBox6")
                Settings.Set("voice", this.checkBox6.IsChecked);
            else if (cb.Name == "checkBox7")
                Settings.Set("files", this.checkBox7.IsChecked);
            else if (cb.Name == "checkBox8")
            {
                Settings.Set("captcha", this.checkBox8.IsChecked);
                this.comboBox2.IsEnabled = (bool)this.checkBox8.IsChecked;
            }
            else if (cb.Name == "checkBox9")
                Settings.Set("enabled", this.checkBox9.IsChecked, "web");
            else if (cb.Name == "checkBox10")
            {
                Settings.Set("commands", this.checkBox10.IsChecked);
                this.listView1.IsEnabled = (bool)this.checkBox10.IsChecked;
            }
            else if (cb.Name == "checkBox11")
            {
                Settings.Set("scripting", this.checkBox11.IsChecked);
                this.checkBox12.IsEnabled = (bool)this.checkBox11.IsChecked;
                this.comboBox1.IsEnabled = (bool)this.checkBox11.IsChecked;
            }
            else if (cb.Name == "checkBox12")
                Settings.Set("inroom_scripting", this.checkBox12.IsChecked);
            else if (cb.Name == "checkBox13")
                Settings.Set("auto_ban_clear_enabled", this.checkBox13.IsChecked);
            else if (cb.Name == "checkBox17")
            {
                Settings.Set("age_restrict", this.checkBox17.IsChecked);
                this.numericUpDown2.IsEnabled = (bool)this.checkBox17.IsChecked;
            }
            else if (cb.Name == "checkBox15")
                Settings.Set("reject_male", this.checkBox15.IsChecked);
            else if (cb.Name == "checkBox14")
                Settings.Set("reject_female", this.checkBox14.IsChecked);
            else if (cb.Name == "checkBox16")
                Settings.Set("reject_unknown", this.checkBox16.IsChecked);
            else if (cb.Name == "checkBox18")
                Settings.Set("hide_ips", this.checkBox18.IsChecked);
            else if (cb.Name == "checkBox19")
                Settings.Set("local_host", this.checkBox19.IsChecked);
            else if (cb.Name == "checkBox20")
                Settings.Set("link_reconnect", this.checkBox20.IsChecked);
            else if (cb.Name == "checkBox21")
                Settings.Set("link_admin", this.checkBox21.IsChecked);
            else if (cb.Name == "checkBox22")
                Settings.Set("strict", this.checkBox22.IsChecked);
            else if (cb.Name == "checkBox23")
                Settings.Set("roomsearch", this.checkBox23.IsChecked);
            else if (cb.Name == "checkBox30")
            {
                Settings.Set("script_can_level", this.checkBox30.IsChecked);
                Settings.ScriptCanLevel(Settings.Get<bool>("script_can_level"));
            }
            else if (cb.Name == "checkBox25")
                Settings.Set("fonts_enabled", this.checkBox25.IsChecked);
        }

        private void ScriptLevelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Set("inroom_level", (byte)this.comboBox1.SelectedIndex + 1);
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (SETTING_UP)
                return;
            Settings.Set("auto_ban_clear_interval", this.numericUpDown1.Value);
        }

        private void CaptchaModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Set("captcha_mode", this.comboBox2.SelectedIndex);
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                String path = core.DataPaths.Root;

                if (!System.IO.Directory.Exists(path))
                    System.IO.Directory.CreateDirectory(path);

                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });

                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sb0t-startup.log"),
                        DateTime.Now + " button2 DataPath=" + path + Environment.NewLine);
                }
                catch { }
            }
            catch { }
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                String hashlink = core.Hashlink.EncodeHashlink(new core.Room
                {
                    IP = IPAddress.Loopback,
                    Name = textBox1.Text,
                    Port = ushort.Parse(textBox2.Text)
                });

                if (hashlink != null)
                    Process.Start(new ProcessStartInfo("arlnk://" + hashlink) { UseShellExecute = true });
            }
            catch { }
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                String hashlink = core.Hashlink.EncodeHashlink(new core.Room
                {
                    IP = IPAddress.Loopback,
                    Name = textBox1.Text,
                    Port = ushort.Parse(textBox2.Text)
                });

                if (hashlink != null)
                    Process.Start(new ProcessStartInfo("cb0t://" + hashlink) { UseShellExecute = true });
            }
            catch { }
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            if (SETTING_UP)
                return;
            Settings.Set("age_restrict_value", this.numericUpDown2.Value);
        }

        private void comboBox3_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Set("language", this.ComboBoxLanguageToAresLanguage(this.comboBox3.SelectedIndex));
        }

        private async void button5_Click(object sender, RoutedEventArgs e)
        {
            var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Images") { Patterns = new List<string> { "*.bmp", "*.jpg", "*.png" } }
                }
            });

            if (files.Count > 0)
            {
                try
                {
                    string file = files[0].TryGetLocalPath();
                    byte[] data = File.ReadAllBytes(file);
                    byte[] resized = Services.ImageUtils.ResizeToJpeg(data, 90, 90);
                    this.image1.Source = ToBitmap(resized);

                    String path = DataPaths.Avatars;

                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    File.WriteAllBytes(path + "\\server", resized);
                    Avatars.UpdateServerAvatar(resized);
                }
                catch { }
            }
        }

        private async void button6_Click(object sender, RoutedEventArgs e)
        {
            var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Images") { Patterns = new List<string> { "*.bmp", "*.jpg", "*.png" } }
                }
            });

            if (files.Count > 0)
            {
                try
                {
                    string file = files[0].TryGetLocalPath();
                    byte[] data = File.ReadAllBytes(file);
                    byte[] resized = Services.ImageUtils.ResizeToJpeg(data, 90, 90);
                    this.image2.Source = ToBitmap(resized);

                    String path = DataPaths.Avatars;

                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    File.WriteAllBytes(path + "\\default", resized);
                    Avatars.UpdateDefaultAvatar(resized);
                }
                catch { }
            }
        }

        private static Avalonia.Media.Imaging.Bitmap ToBitmap(byte[] data)
        {
            using (var ms = new MemoryStream(data))
                return new Avalonia.Media.Imaging.Bitmap(ms);
        }

        private void ExtensionGUISelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Extension embedded GUI is not available yet.
        }

        private void UninstallExtension(object sender, RoutedEventArgs e)
        {
            object obj = this.listBox2.SelectedItem;

            if (obj != null && obj is StackPanel sp && sp.Tag != null)
                this.UnloadExtension(sp.Tag.ToString());
        }

        private void RefreshExtensions(object sender, RoutedEventArgs e)
        {
            this.listBox1.Items.Clear();

            String path = DataPaths.Extensions;

            if (Directory.Exists(path))
            {
                DirectoryInfo root = new DirectoryInfo(path);

                foreach (DirectoryInfo folder in root.GetDirectories())
                    foreach (FileInfo file in folder.GetFiles())
                        if (file.Name == "extension.dll")
                        {
                            this.listBox1.Items.Add(folder.Name);
                            break;
                        }
            }
        }

        private void PluginInstallDoubleClicked(object sender, TappedEventArgs e)
        {
            if (this.listBox1.SelectedItem != null)
                this.LoadExtension(this.listBox1.SelectedItem.ToString());
        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            String text = this.textBox8.Text;
            this.textBox8.Text = String.Empty;

            try
            {
                if (text.StartsWith("sblnk://"))
                {
                    text = text.Substring(8);
                    byte[] buffer = Convert.FromBase64String(text);
                    Array.Reverse(buffer);
                    byte len = buffer[0];
                    String name = System.Text.Encoding.UTF8.GetString(buffer, 1, len);
                    Guid guid = new Guid(buffer.Skip(1 + len).ToArray());
                    this.AddLink(name, guid);
                }
                else throw new Exception();
            }
            catch
            {
                ShowMessage(GUILabels.IsSpanish ? GUILabels.spanish["mboxc"] : GUILabels.english["mboxc"]);
            }
        }

        private void comboBox4_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Set("link_mode", this.comboBox4.SelectedIndex);
        }

        private void RemoveTrustedLeaf(object sender, RoutedEventArgs e)
        {
            int i = this.listBox3.SelectedIndex;

            if (i > -1)
            {
                core.LinkHub.TrustedLeafItem item = (core.LinkHub.TrustedLeafItem)this.listBox3.Items[i];
                core.LinkHub.TrustedLeavesManager.RemoveItem(item);
                this.listBox3.Items.RemoveAt(i);
            }
        }

        private void SpanishClicked(object sender, TappedEventArgs e)
        {
            GUILabels.SetSpanish(this);
        }

        private void EnglishClicked(object sender, TappedEventArgs e)
        {
            GUILabels.SetEnglish(this);
        }

        private void buttonAddProxy_Click(object sender, RoutedEventArgs e)
        {
            string ip = this.textBoxProxyIP.Text.Trim();
            if (String.IsNullOrEmpty(ip))
                return;
            System.Net.IPAddress parsed;
            if (!System.Net.IPAddress.TryParse(ip, out parsed))
            {
                ShowMessage("Invalid IP address.");
                return;
            }
            core.TrustedProxyManager.AddTrustedProxy(parsed.ToString());
            this.textBoxProxyIP.Text = String.Empty;
            this.RefreshTrustedProxies();
        }

        private void RemoveTrustedProxy_Click(object sender, RoutedEventArgs e)
        {
            if (this.listBoxTrustedProxies.SelectedItem == null)
                return;
            string ip = this.listBoxTrustedProxies.SelectedItem.ToString();
            core.TrustedProxyManager.RemoveTrustedProxy(ip);
            this.RefreshTrustedProxies();
        }

        private void RefreshTrustedProxies()
        {
            this.listBoxTrustedProxies.Items.Clear();
            foreach (string ip in core.TrustedProxyManager.GetTrustedProxies())
                this.listBoxTrustedProxies.Items.Add(ip);
        }

        private void CmdFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = this.cmdFilter.Text;
            if (String.IsNullOrEmpty(filter))
                this.listView1.ItemsSource = _commands;
            else
                this.listView1.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<CommandItem>(
                    _commands.Where(x =>
                        x.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Level.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void LevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SETTING_UP || sender == null)
                return;

            ComboBox combo = sender as ComboBox;
            if (combo == null || combo.DataContext == null)
                return;

            CommandItem item = combo.DataContext as CommandItem;
            if (item == null)
                return;

            CommandManager.UpdateCommand(item.Name, item.Level);
        }

        private void btnTheme_Click(object sender, RoutedEventArgs e)
        {
            string current = App.CurrentTheme;
            string next = current == "dark" ? "light" : "dark";
            App.ApplyTheme(next);
            core.Settings.Set("theme", next);
        }

        private async void ShowMessage(string text)
        {
            // Native Avalonia 11 dialog (replaces ThingLing.Controls.MessageBox,
            // which is built against Avalonia 0.10 and fails to load).
            try
            {
                await MsgBox.ShowAsync(this, text, "sb0t");
            }
            catch
            {
                // Never crash the GUI if the dialog cannot be shown.
            }
        }
    }
}
