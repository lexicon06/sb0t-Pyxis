using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace gui
{
    /// <summary>
    /// Minimal native message dialog for Avalonia 11.
    /// Replaces ThingLing.Controls.MessageBox, which is compiled against
    /// Avalonia 0.10 and throws with Avalonia 11 (Avalonia.Visuals not found).
    /// </summary>
    public static class MsgBox
    {
        public static async System.Threading.Tasks.Task ShowAsync(Window owner, string text, string title = "sb0t")
        {
            var win = new Window
            {
                Title = title,
                Width = 420,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            var border = new Border
            {
                Padding = new Avalonia.Thickness(20)
            };

            var panel = new DockPanel();

            var message = new TextBlock
            {
                Text = text,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 0, 16)
            };
            DockPanel.SetDock(message, Dock.Top);
            panel.Children.Add(message);

            var ok = new Button
            {
                Content = "OK",
                MinWidth = 80,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            ok.Click += (s, e) => win.Close();
            DockPanel.SetDock(ok, Dock.Bottom);
            panel.Children.Add(ok);

            border.Child = panel;
            win.Content = border;

            try
            {
                // Apply the app theme resources to the dialog.
                win.Resources.MergedDictionaries.Add(App.Current.Resources);
            }
            catch { }

            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();
        }
    }
}
