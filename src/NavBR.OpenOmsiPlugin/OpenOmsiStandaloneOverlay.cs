using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NavBR.OpenOmsiPlugin;

/// <summary>
/// Update-safe NavBR control surface. It lives entirely inside the plugin DLL and never
/// patches/replaces openomsi.exe. F10 toggles a native Win32 overlay anchored to the
/// openOMSI window; clicks call the plugin's HUD state directly.
/// </summary>
internal static class OpenOmsiStandaloneOverlay
{
    private const int Width = 390;
    private const int Header = 74;
    private const int RowHeight = 38;
    private const int Footer = 58;
    private const int RowCount = 10;
    private const int Height = Header + RowHeight * RowCount + Footer;

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x00000002;

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_HOTKEY = 0x0312;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;
    private const int HOTKEY_ID = 0x4E4252; // NBR
    private const uint VK_F10 = 0x79;

    private static readonly string[] Rows =
    [
        "Minimapa / GPS",
        "Mapa completo",
        "Zoom automatico",
        "Seguir veiculo",
        "Horarios",
        "TeleMatrix",
        "Trafego IA",
        "Jogadores",
        "Congestionamento",
        "Setas / rota 3D"
    ];

    private static readonly bool[] Enabled = new bool[RowCount];
    private static readonly WndProcDelegate WndProcRoot = WndProc;

    private static Thread? _thread;
    private static IntPtr _window;
    private static volatile bool _running;
    private static volatile bool _visible = true;

    internal static void Start()
    {
        if (!OperatingSystem.IsWindows() || _running)
        {
            return;
        }

        _running = true;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "NavBR openOMSI overlay"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    internal static void Stop()
    {
        _running = false;
        var hwnd = _window;
        if (hwnd != IntPtr.Zero)
        {
            PostMessage(hwnd, WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static void Run()
    {
        try
        {
            var instance = GetModuleHandle(null);
            var cls = "NavBR.OpenOMSI.Overlay." + Environment.ProcessId;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                hInstance = instance,
                lpszClassName = cls,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcRoot),
                hCursor = LoadCursor(IntPtr.Zero, (IntPtr)32512)
            };

            if (RegisterClassEx(ref wc) == 0)
            {
                return;
            }

            var owner = FindOpenOmsiWindow();
            var rect = GetAnchorRect(owner);
            _window = CreateWindowEx(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED,
                cls,
                "NavBR",
                WS_POPUP | WS_VISIBLE,
                rect.X,
                rect.Y,
                Width,
                Height,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);

            if (_window == IntPtr.Zero)
            {
                return;
            }

            SetLayeredWindowAttributes(_window, 0, 242, LWA_ALPHA);
            RegisterHotKey(_window, HOTKEY_ID, 0, VK_F10);
            ShowWindow(_window, SW_SHOWNOACTIVATE);
            InvalidateRect(_window, IntPtr.Zero, true);

            var lastAnchor = owner;
            var lastTick = Environment.TickCount64;

            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);

                if (Environment.TickCount64 - lastTick > 500)
                {
                    lastTick = Environment.TickCount64;
                    if (lastAnchor == IntPtr.Zero || !IsWindow(lastAnchor))
                    {
                        lastAnchor = FindOpenOmsiWindow();
                    }
                    PositionNearOpenOmsi(lastAnchor);
                }
            }
        }
        catch
        {
            // The plugin must never bring the simulator down because the optional overlay failed.
        }
        finally
        {
            if (_window != IntPtr.Zero)
            {
                UnregisterHotKey(_window, HOTKEY_ID);
                DestroyWindow(_window);
                _window = IntPtr.Zero;
            }
            _running = false;
        }
    }

    private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_HOTKEY when wParam.ToInt32() == HOTKEY_ID:
                _visible = !_visible;
                ShowWindow(hwnd, _visible ? SW_SHOWNOACTIVATE : SW_HIDE);
                if (_visible)
                {
                    PositionNearOpenOmsi(FindOpenOmsiWindow());
                    InvalidateRect(hwnd, IntPtr.Zero, true);
                }
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                HandleClick(hwnd, LowWord(lParam), HighWord(lParam));
                return IntPtr.Zero;

            case WM_PAINT:
                Paint(hwnd);
                return IntPtr.Zero;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static void HandleClick(IntPtr hwnd, int x, int y)
    {
        if (y >= Header && y < Header + RowHeight * RowCount)
        {
            var bit = (y - Header) / RowHeight;
            if (bit is >= 0 and < RowCount)
            {
                Enabled[bit] = !Enabled[bit];
                var mask = 1u << bit;
                PluginExports.ApplyHostHudFlags(mask, Enabled[bit] ? mask : 0);
                InvalidateRect(hwnd, IntPtr.Zero, false);
            }
            return;
        }

        var footerY = Header + RowHeight * RowCount;
        if (y >= footerY)
        {
            var third = Width / 3;
            var preset = Math.Clamp(x / third, 0, 2);
            ApplyPreset(preset);
            InvalidateRect(hwnd, IntPtr.Zero, false);
        }
    }

    private static void ApplyPreset(int preset)
    {
        Array.Fill(Enabled, false);
        switch (preset)
        {
            case 0: // GPS
                foreach (var bit in new[] { 0, 2, 3, 9 }) Enabled[bit] = true;
                break;
            case 1: // Operacao
                foreach (var bit in new[] { 0, 2, 3, 4, 5, 6, 8, 9 }) Enabled[bit] = true;
                break;
            default:
                Array.Fill(Enabled, true);
                break;
        }

        uint values = 0;
        for (var i = 0; i < Enabled.Length; i++)
        {
            if (Enabled[i]) values |= 1u << i;
        }
        PluginExports.ApplyHostHudFlags((1u << RowCount) - 1u, values);
    }

    private static void Paint(IntPtr hwnd)
    {
        var hdc = BeginPaint(hwnd, out var ps);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var bg = CreateSolidBrush(Rgb(18, 21, 27));
            var rowBg = CreateSolidBrush(Rgb(31, 36, 45));
            var on = CreateSolidBrush(Rgb(45, 145, 235));
            var off = CreateSolidBrush(Rgb(72, 78, 88));
            var accent = CreateSolidBrush(Rgb(35, 74, 112));
            var white = Rgb(245, 248, 252);
            var muted = Rgb(170, 185, 205);

            var clientRect = Rect(0, 0, Width, Height);
            FillRect(hdc, ref clientRect, bg);
            SetBkMode(hdc, 1);
            SetTextColor(hdc, white);
            TextOut(hdc, 16, 14, "NavBR", 5);
            SetTextColor(hdc, muted);
            var subtitle = "Plugin update-safe · F10 · openomsi.exe oficial";
            TextOut(hdc, 16, 42, subtitle, subtitle.Length);

            for (var i = 0; i < RowCount; i++)
            {
                var y = Header + i * RowHeight;
                var rr = Rect(8, y + 1, Width - 8, y + RowHeight - 1);
                FillRect(hdc, ref rr, rowBg);
                SetTextColor(hdc, white);
                TextOut(hdc, 18, y + 11, Rows[i], Rows[i].Length);

                var sw = Rect(Width - 62, y + 9, Width - 20, y + 29);
                FillRect(hdc, ref sw, Enabled[i] ? on : off);
            }

            var footerY = Header + RowHeight * RowCount;
            var names = new[] { "GPS", "Operacao", "Tudo" };
            for (var i = 0; i < names.Length; i++)
            {
                var x0 = i * (Width / 3) + 6;
                var x1 = (i + 1) * (Width / 3) - 6;
                var r = Rect(x0, footerY + 12, x1, Height - 10);
                FillRect(hdc, ref r, accent);
                SetTextColor(hdc, white);
                TextOut(hdc, x0 + 18, footerY + 24, names[i], names[i].Length);
            }

            DeleteObject(bg);
            DeleteObject(rowBg);
            DeleteObject(on);
            DeleteObject(off);
            DeleteObject(accent);
        }
        finally
        {
            EndPaint(hwnd, ref ps);
        }
    }

    private static void PositionNearOpenOmsi(IntPtr owner)
    {
        if (_window == IntPtr.Zero || owner == IntPtr.Zero || !_visible)
        {
            return;
        }

        if (!GetWindowRect(owner, out var r))
        {
            return;
        }

        var x = Math.Max(r.Left + 12, r.Right - Width - 24);
        var y = r.Top + 48;
        SetWindowPos(_window, new IntPtr(-1), x, y, Width, Height, 0x0010);
    }

    private static (int X, int Y) GetAnchorRect(IntPtr owner)
    {
        if (owner != IntPtr.Zero && GetWindowRect(owner, out var r))
        {
            return (Math.Max(r.Left + 12, r.Right - Width - 24), r.Top + 48);
        }
        return (100, 100);
    }

    private static IntPtr FindOpenOmsiWindow()
    {
        var pid = (uint)Environment.ProcessId;
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid == pid && IsWindowVisible(hwnd))
            {
                var length = GetWindowTextLength(hwnd);
                if (length > 0)
                {
                    found = hwnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static RECT Rect(int left, int top, int right, int bottom) =>
        new() { Left = left, Top = top, Right = right, Bottom = bottom };

    private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
    private static int LowWord(IntPtr value) => unchecked((short)((long)value & 0xffff));
    private static int HighWord(IntPtr value) => unchecked((short)(((long)value >> 16) & 0xffff));

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumWindowsDelegate(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int cmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsDelegate callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint color);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TextOut(IntPtr hdc, int x, int y, string text, int length);
}
