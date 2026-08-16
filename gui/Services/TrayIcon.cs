using System;
using System.Runtime.InteropServices;
using System.IO;

namespace gui.Services
{
    /// <summary>
    /// Multi-platform system tray icon.
    /// Windows: uses Shell_NotifyIcon via P/Invoke.
    /// Other platforms: no-op (window minimizes to taskbar normally).
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        // Shell_NotifyIcon structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public IntPtr guidItem;
            public IntPtr hBalloonIcon;
        }

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint WM_USER = 0x0400;
        private const uint WM_LBUTTONUP = 0x0202;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cx, int cy, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x0010;
        private const uint LR_DEFAULTSIZE = 0x0040;

        private IntPtr _hWnd;
        private IntPtr _hIcon;
        private bool _added;
        private uint _id = 1;

        public event Action LeftClicked;

        public TrayIcon(IntPtr windowHandle)
        {
            _hWnd = windowHandle;
        }

        public void Show(string tip)
        {
            if (!IsWindows)
                return;

            LoadIcon();

            var data = CreateData(NIF_ICON | NIF_TIP | NIF_MESSAGE, tip);
            Shell_NotifyIcon(NIM_ADD, ref data);
            _added = true;
        }

        public void UpdateTooltip(string tip)
        {
            if (!IsWindows || !_added)
                return;

            var data = CreateData(NIF_TIP, tip);
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public void Hide()
        {
            if (!IsWindows || !_added)
                return;

            var data = CreateData(0, "");
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }

        public void WndProc(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!IsWindows)
                return;

            if (msg == WM_USER && (uint)lParam == WM_LBUTTONUP)
                LeftClicked?.Invoke();
        }

        private NOTIFYICONDATA CreateData(uint flags, string tip)
        {
            var data = new NOTIFYICONDATA();
            data.cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
            data.hWnd = _hWnd;
            data.uID = _id;
            data.uFlags = flags;
            data.uCallbackMessage = WM_USER;
            data.hIcon = _hIcon;
            data.szTip = tip ?? "";
            return data;
        }

        private void LoadIcon()
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "test2.ico");
            if (File.Exists(iconPath))
                _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        }

        public void Dispose()
        {
            Hide();
        }
    }
}
