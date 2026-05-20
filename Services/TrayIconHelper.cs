using System;
using System.Runtime.InteropServices;

namespace TAY.Services
{
    public static class TrayIconHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;

        private const int WM_USER = 0x0400;
        public const int WM_TRAYICON = WM_USER + 120;

        // Tray message parameters
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONUP = 0x0205;

        private const int IDI_SHIELD = 32518; // Default Windows Shield Icon
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        private const int NIIF_INFO = 0x00000001;

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RETURNCMD = 0x00000100;
        private const uint TPM_LEFTALIGN = 0x00000000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIconW(int dwMessage, [In] ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImageW(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc callback, IntPtr idSubclass, IntPtr refData);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc callback, IntPtr idSubclass);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr idSubclass, IntPtr refData);

        private static NOTIFYICONDATA _nid;
        private static bool _active = false;
        private static IntPtr _hWnd = IntPtr.Zero;
        
        private static SubclassProc? _newWndProc;
        
        private static Action? _onLeftClick;
        private static Action? _onDoubleClick;
        private static Action? _onExit;

        public static void Initialize(IntPtr hWnd, string tooltip, Action onLeftClick, Action onDoubleClick, Action onExit)
        {
            if (_active) return;

            _onLeftClick = onLeftClick;
            _onDoubleClick = onDoubleClick;
            _onExit = onExit;

            // Load custom TAY icon from Assets folder if it exists
            string iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "tay.ico");
            IntPtr hIcon = IntPtr.Zero;

            if (System.IO.File.Exists(iconPath))
            {
                try
                {
                    hIcon = LoadImageW(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                }
                catch { }
            }

            // Fallback to default Windows shield icon if tay.ico isn't resolved
            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIconW(IntPtr.Zero, (IntPtr)IDI_SHIELD);
            }

            _nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = hIcon,
                szTip = tooltip
            };

            // Add the icon to system tray
            Shell_NotifyIconW(NIM_ADD, ref _nid);
            _active = true;

            _hWnd = hWnd;

            // Subclass the Window using comctl32 to capture WM_TRAYICON message
            _newWndProc = new SubclassProc(SubclassWndProc);
            SetWindowSubclass(hWnd, _newWndProc, (IntPtr)101, IntPtr.Zero);
        }

        private static IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr idSubclass, IntPtr refData)
        {
            if (msg == WM_TRAYICON)
            {
                int eventId = lParam.ToInt32();
                if (eventId == WM_LBUTTONUP)
                {
                    _onLeftClick?.Invoke();
                }
                else if (eventId == WM_LBUTTONDBLCLK)
                {
                    _onDoubleClick?.Invoke();
                }
                else if (eventId == WM_RBUTTONUP)
                {
                    ShowPopupMenu(hWnd);
                }
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private static void ShowPopupMenu(IntPtr hWnd)
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            AppendMenuW(hMenu, MF_STRING, (IntPtr)1001, "Open Dashboard");
            AppendMenuW(hMenu, MF_STRING, (IntPtr)1002, "Toggle Mini Status");
            AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero, "");
            AppendMenuW(hMenu, MF_STRING, (IntPtr)1003, "Exit");

            GetCursorPos(out POINT pt);

            // Make sure the menu dismisses when clicking elsewhere
            SetForegroundWindow(hWnd);

            int selectedId = TrackPopupMenu(
                hMenu,
                TPM_RETURNCMD | TPM_LEFTALIGN,
                pt.X,
                pt.Y,
                0,
                hWnd,
                IntPtr.Zero
            );

            DestroyMenu(hMenu);

            if (selectedId == 1001)
            {
                _onDoubleClick?.Invoke();
            }
            else if (selectedId == 1002)
            {
                _onLeftClick?.Invoke();
            }
            else if (selectedId == 1003)
            {
                _onExit?.Invoke();
            }
        }

        public static void Shutdown()
        {
            if (!_active) return;
            Shell_NotifyIconW(NIM_DELETE, ref _nid);
            if (_hWnd != IntPtr.Zero && _newWndProc != null)
            {
                RemoveWindowSubclass(_hWnd, _newWndProc, (IntPtr)101);
            }
            _active = false;
        }

        public static void ShowBalloon(string title, string message)
        {
            if (!_active) return;

            _nid.uFlags = NIF_INFO;
            _nid.szInfoTitle = title;
            _nid.szInfo = message;
            _nid.dwInfoFlags = NIIF_INFO;
            Shell_NotifyIconW(NIM_MODIFY, ref _nid);
        }
    }
}
