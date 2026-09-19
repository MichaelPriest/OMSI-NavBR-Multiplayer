using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NavBR.Client.Multiplayer;

internal sealed class RoleplayKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly LowLevelKeyboardProc _callback;
    private readonly IntPtr _hook;

    public Func<int, bool, bool>? HandleKey { get; set; }

    public RoleplayKeyboardHook()
    {
        _callback = HookCallback;
        _hook = SetWindowsHookEx(
            WhKeyboardLl,
            _callback,
            GetModuleHandle(null),
            0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Unable to install roleplay keyboard hook.");
        }
    }

    public static bool IsKeyDown(int virtualKey)
    {
        try
        {
            return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsOmsiForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(foreground, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(unchecked((int)processId));
            return string.Equals(
                process.ProcessName,
                "Omsi",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var isDown = message is WmKeyDown or WmSysKeyDown;
            var isUp = message is WmKeyUp or WmSysKeyUp;
            if (isDown || isUp)
            {
                var virtualKey = Marshal.ReadInt32(lParam);
                try
                {
                    if (HandleKey?.Invoke(virtualKey, isDown) == true)
                    {
                        return new IntPtr(1);
                    }
                }
                catch
                {
                    // Input filtering must never destabilize the desktop client.
                }
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
