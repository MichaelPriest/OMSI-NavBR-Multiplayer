using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NavBR.OpenOmsiPlugin;

/// <summary>
/// Update-safe NavBR in-game surface. Lives entirely inside the plugin DLL and never
/// patches/replaces openomsi.exe. F10 toggles controls plus a live navigation preview.
/// </summary>
internal static class OpenOmsiStandaloneOverlay
{
    private const int Width = 760;
    private const int Height = 520;
    private const int ControlsWidth = 300;
    private const int Header = 64;
    private const int RowHeight = 33;
    private const int Footer = 54;
    private const int RowCount = 10;
    private const int TimerId = 0x4E42;

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x00000002;

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_TIMER = 0x0113;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const uint WM_HOTKEY = 0x0312;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;
    private const int HOTKEY_ID = 0x4E4252;
    private const int HTCAPTION = 2;
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
    private static volatile bool _manualPosition;
    private static IntPtr _openOmsiWindow;

    internal static void Start()
    {
        if (!OperatingSystem.IsWindows() || _running)
        {
            return;
        }

        SyncEnabledFromState();
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

    private static void SyncEnabledFromState()
    {
        var state = OpenOmsiHudState.Current;
        Enabled[0] = state.MiniMapEnabled;
        Enabled[1] = state.FullMapEnabled;
        Enabled[2] = state.AutoZoomEnabled;
        Enabled[3] = state.FollowVehicleEnabled;
        Enabled[4] = state.TimetableEnabled;
        Enabled[5] = state.TeleMatrixEnabled;
        Enabled[6] = state.TrafficEnabled;
        Enabled[7] = state.MultiplayerEnabled;
        Enabled[8] = state.CongestionEnabled;
        Enabled[9] = state.RouteGuidanceEnabled;
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
            _openOmsiWindow = owner;
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
            SetTimer(_window, TimerId, 250, IntPtr.Zero);
            ShowWindow(_window, SW_SHOWNOACTIVATE);
            InvalidateRect(_window, IntPtr.Zero, true);

            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch
        {
            // Optional UI must never bring the simulator down.
        }
        finally
        {
            if (_window != IntPtr.Zero)
            {
                KillTimer(_window, TimerId);
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
                    EnsureOpenOmsiAnchor();
                    if (!_manualPosition)
                    {
                        PositionNearOpenOmsi(_openOmsiWindow);
                    }
                    InvalidateRect(hwnd, IntPtr.Zero, false);
                }
                return IntPtr.Zero;

            case WM_TIMER when wParam.ToInt32() == TimerId:
                if (_visible)
                {
                    EnsureOpenOmsiAnchor();
                    if (!_manualPosition)
                    {
                        PositionNearOpenOmsi(_openOmsiWindow);
                    }
                    InvalidateRect(hwnd, IntPtr.Zero, false);
                }
                return IntPtr.Zero;

            case WM_LBUTTONDOWN:
                if (HighWord(lParam) < Header)
                {
                    _manualPosition = true;
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
                    return IntPtr.Zero;
                }
                break;

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
        if (x < ControlsWidth && y >= Header && y < Header + RowHeight * RowCount)
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
        if (x < ControlsWidth && y >= footerY && y < footerY + Footer)
        {
            var third = ControlsWidth / 3;
            ApplyPreset(Math.Clamp(x / third, 0, 2));
            InvalidateRect(hwnd, IntPtr.Zero, false);
        }
    }

    private static void ApplyPreset(int preset)
    {
        Array.Fill(Enabled, false);
        switch (preset)
        {
            case 0:
                foreach (var bit in new[] { 0, 2, 3, 9 }) Enabled[bit] = true;
                break;
            case 1:
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

        IntPtr bg = IntPtr.Zero;
        IntPtr rowBg = IntPtr.Zero;
        IntPtr on = IntPtr.Zero;
        IntPtr off = IntPtr.Zero;
        IntPtr accent = IntPtr.Zero;
        IntPtr mapBg = IntPtr.Zero;
        IntPtr routePen = IntPtr.Zero;
        IntPtr traveledPen = IntPtr.Zero;
        IntPtr rejoinPen = IntPtr.Zero;

        try
        {
            bg = CreateSolidBrush(Rgb(18, 21, 27));
            rowBg = CreateSolidBrush(Rgb(31, 36, 45));
            on = CreateSolidBrush(Rgb(45, 145, 235));
            off = CreateSolidBrush(Rgb(72, 78, 88));
            accent = CreateSolidBrush(Rgb(35, 74, 112));
            mapBg = CreateSolidBrush(Rgb(11, 14, 19));
            routePen = CreatePen(0, 4, Rgb(48, 160, 255));
            traveledPen = CreatePen(0, 3, Rgb(92, 105, 120));
            rejoinPen = CreatePen(0, 3, Rgb(255, 176, 56));

            var white = Rgb(245, 248, 252);
            var muted = Rgb(170, 185, 205);
            var warning = Rgb(255, 190, 70);

            var clientRect = Rect(0, 0, Width, Height);
            FillRect(hdc, ref clientRect, bg);
            SetBkMode(hdc, 1);

            SetTextColor(hdc, white);
            DrawTextLine(hdc, 14, 12, "NavBR openOMSI");
            SetTextColor(hdc, muted);
            DrawTextLine(hdc, 14, 37, "F10 fecha/abre · plugin-only · EXE oficial");

            DrawControls(hdc, rowBg, on, off, accent, white);

            var divider = Rect(ControlsWidth, 0, ControlsWidth + 2, Height);
            FillRect(hdc, ref divider, accent);

            var frame = OpenOmsiOverlayExport.LatestOverlay2D;
            if (frame is null)
            {
                SetTextColor(hdc, warning);
                DrawTextLine(hdc, ControlsWidth + 24, 28, "Aguardando dados do openOMSI...");
                SetTextColor(hdc, muted);
                DrawTextLine(hdc, ControlsWidth + 24, 56, "Entre no mapa e carregue um veiculo.");
                return;
            }

            DrawLiveHeader(hdc, frame, white, muted, warning);
            DrawMap(hdc, frame, mapBg, routePen, traveledPen, rejoinPen, white, muted);
            DrawLiveFooter(hdc, frame, white, muted, warning);
        }
        finally
        {
            foreach (var obj in new[] { bg, rowBg, on, off, accent, mapBg, routePen, traveledPen, rejoinPen })
            {
                if (obj != IntPtr.Zero) DeleteObject(obj);
            }
            EndPaint(hwnd, ref ps);
        }
    }

    private static void DrawControls(IntPtr hdc, IntPtr rowBg, IntPtr on, IntPtr off, IntPtr accent, uint white)
    {
        for (var i = 0; i < RowCount; i++)
        {
            var y = Header + i * RowHeight;
            var rr = Rect(8, y + 1, ControlsWidth - 8, y + RowHeight - 1);
            FillRect(hdc, ref rr, rowBg);
            SetTextColor(hdc, white);
            DrawTextLine(hdc, 16, y + 9, Rows[i]);

            var sw = Rect(ControlsWidth - 54, y + 7, ControlsWidth - 16, y + 26);
            FillRect(hdc, ref sw, Enabled[i] ? on : off);
        }

        var footerY = Header + RowHeight * RowCount;
        var names = new[] { "GPS", "Operacao", "Tudo" };
        for (var i = 0; i < names.Length; i++)
        {
            var x0 = i * (ControlsWidth / 3) + 5;
            var x1 = (i + 1) * (ControlsWidth / 3) - 5;
            var r = Rect(x0, footerY + 10, x1, footerY + 43);
            FillRect(hdc, ref r, accent);
            SetTextColor(hdc, white);
            DrawTextLine(hdc, x0 + 13, footerY + 19, names[i]);
        }
    }

    private static void DrawLiveHeader(
        IntPtr hdc,
        NavBR.Shared.PluginBridge.OpenOmsiOverlay2DFrameState frame,
        uint white,
        uint muted,
        uint warning)
    {
        var x = ControlsWidth + 18;
        SetTextColor(hdc, frame.OffRoute ? warning : white);
        DrawTextLine(hdc, x, 14, frame.OffRoute ? "FORA DA ROTA" : Safe(frame.PrimaryText, "Navegacao ativa"));

        SetTextColor(hdc, muted);
        var distance = frame.DistanceToManeuverMeters is double d
            ? d < 1000 ? $"{d:0} m" : $"{d / 1000d:0.0} km"
            : "--";
        var remaining = frame.RouteRemainingMeters is double r
            ? r < 1000 ? $"{r:0} m restantes" : $"{r / 1000d:0.0} km restantes"
            : "rota sem distancia";
        DrawTextLine(hdc, x, 39, $"{distance} · {remaining}");
        DrawTextLine(hdc, x, 59, Safe(frame.SecondaryText, "Aguardando proxima parada"));
    }

    private static void DrawMap(
        IntPtr hdc,
        NavBR.Shared.PluginBridge.OpenOmsiOverlay2DFrameState frame,
        IntPtr mapBg,
        IntPtr routePen,
        IntPtr traveledPen,
        IntPtr rejoinPen,
        uint white,
        uint muted)
    {
        const int left = ControlsWidth + 18;
        const int top = 88;
        const int right = Width - 18;
        const int bottom = 386;
        var mapRect = Rect(left, top, right, bottom);
        FillRect(hdc, ref mapRect, mapBg);

        if (frame.CenterX is not double cx || frame.CenterY is not double cy || frame.RadiusMeters <= 0d)
        {
            SetTextColor(hdc, muted);
            DrawTextLine(hdc, left + 20, top + 20, "Posicao ainda indisponivel.");
            return;
        }

        DrawRoute(hdc, frame.TraveledRoute, cx, cy, frame.RadiusMeters, left, top, right, bottom, traveledPen);
        DrawRoute(hdc, frame.ForwardRoute, cx, cy, frame.RadiusMeters, left, top, right, bottom, routePen);
        DrawRoute(hdc, frame.RejoinRoute, cx, cy, frame.RadiusMeters, left, top, right, bottom, rejoinPen);

        foreach (var marker in frame.Markers.Take(120))
        {
            var p = ToScreen(marker.X, marker.Y, cx, cy, frame.RadiusMeters, left, top, right, bottom);
            var radius = string.Equals(marker.Kind, "player", StringComparison.OrdinalIgnoreCase) ? 5 : 3;
            var brush = CreateSolidBrush(
                string.Equals(marker.Kind, "player", StringComparison.OrdinalIgnoreCase)
                    ? Rgb(255, 205, 70)
                    : string.Equals(marker.Kind, "ai", StringComparison.OrdinalIgnoreCase)
                        ? Rgb(90, 220, 140)
                        : Rgb(235, 235, 235));
            var old = SelectObject(hdc, brush);
            Ellipse(hdc, p.X - radius, p.Y - radius, p.X + radius, p.Y + radius);
            SelectObject(hdc, old);
            DeleteObject(brush);
        }

        var centerBrush = CreateSolidBrush(Rgb(255, 255, 255));
        var oldCenter = SelectObject(hdc, centerBrush);
        var center = ToScreen(cx, cy, cx, cy, frame.RadiusMeters, left, top, right, bottom);
        Ellipse(hdc, center.X - 6, center.Y - 6, center.X + 6, center.Y + 6);
        SelectObject(hdc, oldCenter);
        DeleteObject(centerBrush);

        SetTextColor(hdc, white);
        DrawTextLine(hdc, left + 8, top + 8,
            $"GPS · raio {frame.RadiusMeters:0} m · {frame.OrientationMode}");
    }

    private static void DrawRoute(
        IntPtr hdc,
        NavBR.Shared.PluginBridge.OpenOmsiRoutePoint[] points,
        double cx,
        double cy,
        double radius,
        int left,
        int top,
        int right,
        int bottom,
        IntPtr pen)
    {
        if (points.Length < 2)
        {
            return;
        }

        var old = SelectObject(hdc, pen);
        var first = ToScreen(points[0].X, points[0].Y, cx, cy, radius, left, top, right, bottom);
        MoveToEx(hdc, first.X, first.Y, IntPtr.Zero);
        foreach (var point in points.Skip(1))
        {
            var p = ToScreen(point.X, point.Y, cx, cy, radius, left, top, right, bottom);
            LineTo(hdc, p.X, p.Y);
        }
        SelectObject(hdc, old);
    }

    private static POINT ToScreen(
        double x,
        double y,
        double cx,
        double cy,
        double radius,
        int left,
        int top,
        int right,
        int bottom)
    {
        var halfW = (right - left) / 2d;
        var halfH = (bottom - top) / 2d;
        var scale = Math.Min(halfW, halfH) / Math.Max(1d, radius);
        return new POINT
        {
            X = (int)Math.Round(left + halfW + (x - cx) * scale),
            Y = (int)Math.Round(top + halfH - (y - cy) * scale)
        };
    }

    private static void DrawLiveFooter(
        IntPtr hdc,
        NavBR.Shared.PluginBridge.OpenOmsiOverlay2DFrameState frame,
        uint white,
        uint muted,
        uint warning)
    {
        var x = ControlsWidth + 18;
        var y = 399;

        if (frame.TeleMatrixVisible)
        {
            SetTextColor(hdc, white);
            DrawTextLine(hdc, x, y,
                $"Linha {Safe(frame.TeleMatrixLine, "--")}  →  {Safe(frame.TeleMatrixDestination, "--")}");
            SetTextColor(hdc, muted);
            DrawTextLine(hdc, x, y + 23, $"Proxima: {Safe(frame.TeleMatrixNextStop, "--")}");

            var delay = frame.TeleMatrixDelaySeconds;
            SetTextColor(hdc, delay is > 120 or < -120 ? warning : white);
            var delayText = delay is null
                ? "atraso --"
                : delay.Value >= 0
                    ? $"+{delay.Value / 60}:{Math.Abs(delay.Value % 60):00}"
                    : $"-{Math.Abs(delay.Value) / 60}:{Math.Abs(delay.Value % 60):00}";
            DrawTextLine(hdc, x, y + 46, $"{delayText} · {Safe(frame.TeleMatrixPunctualityState, "unknown")}");
        }

        SetTextColor(hdc, muted);
        var layers = $"IA {(frame.TrafficVisible ? "ON" : "OFF")} · Players {(frame.PlayersVisible ? "ON" : "OFF")} · " +
                     $"Autozoom {(frame.AutoZoomEnabled ? "ON" : "OFF")} · Seguir {(frame.FollowVehicleEnabled ? "ON" : "OFF")}";
        DrawTextLine(hdc, x, Height - 27, layers);
    }

    private static string Safe(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static void DrawTextLine(IntPtr hdc, int x, int y, string text)
    {
        if (text.Length > 72)
        {
            text = text[..69] + "...";
        }
        TextOut(hdc, x, y, text, text.Length);
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

        var x = Math.Max(r.Left + 8, r.Right - Width - 18);
        var y = Math.Max(r.Top + 36, r.Top + 8);
        SetWindowPos(_window, new IntPtr(-1), x, y, Width, Height, 0x0010);
    }

    private static (int X, int Y) GetAnchorRect(IntPtr owner)
    {
        if (owner != IntPtr.Zero && GetWindowRect(owner, out var r))
        {
            return (Math.Max(r.Left + 8, r.Right - Width - 18), r.Top + 36);
        }
        return (80, 80);
    }

    private static void EnsureOpenOmsiAnchor()
    {
        if (_openOmsiWindow == IntPtr.Zero ||
            _openOmsiWindow == _window ||
            !IsWindow(_openOmsiWindow))
        {
            _openOmsiWindow = FindOpenOmsiWindow();
        }
    }

    private static IntPtr FindOpenOmsiWindow()
    {
        var pid = (uint)Environment.ProcessId;
        IntPtr found = IntPtr.Zero;
        long largestArea = -1;

        EnumWindows((hwnd, _) =>
        {
            if (hwnd == _window || !IsWindowVisible(hwnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != pid || GetWindowTextLength(hwnd) <= 0)
            {
                return true;
            }

            if (!GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            var width = Math.Max(0, rect.Right - rect.Left);
            var height = Math.Max(0, rect.Bottom - rect.Top);
            var area = (long)width * height;
            if (area > largestArea)
            {
                largestArea = area;
                found = hwnd;
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
    private static extern UIntPtr SetTimer(IntPtr hwnd, int idEvent, uint elapse, IntPtr timerFunc);

    [DllImport("user32.dll")]
    private static extern bool KillTimer(IntPtr hwnd, int idEvent);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

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
    private static extern IntPtr CreatePen(int style, int width, uint color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint color);

    [DllImport("gdi32.dll")]
    private static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr oldPoint);

    [DllImport("gdi32.dll")]
    private static extern bool LineTo(IntPtr hdc, int x, int y);

    [DllImport("gdi32.dll")]
    private static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TextOut(IntPtr hdc, int x, int y, string text, int length);
}
