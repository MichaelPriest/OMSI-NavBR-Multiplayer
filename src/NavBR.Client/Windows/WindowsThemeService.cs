using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NavBR.Client.Windows;

internal static class WindowsThemeService
{
    // Dark caption opt-in. Attribute 20 is current; 19 supports older Windows 10 builds.
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    // Windows 11 caption customization. Unsupported builds simply ignore these calls.
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    // COLORREF is 0x00BBGGRR.
    private const uint NavCaptionColor = 0x001F1107; // #07111F
    private const uint NavCaptionTextColor = 0x00FFF7EE; // #EEF7FF
    private const uint NavBorderColor = 0x005F4124; // #24415F

    public static void ApplyDarkTitleBar(Window window)
    {
        if (!OperatingSystem.IsWindows() || window.WindowStyle == WindowStyle.None)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        var result = DwmSetWindowAttribute(
            handle,
            DwmwaUseImmersiveDarkMode,
            ref enabled,
            Marshal.SizeOf<int>());

        if (result != 0)
        {
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaUseImmersiveDarkModeLegacy,
                ref enabled,
                Marshal.SizeOf<int>());
        }

        // Some Windows 11 configurations keep the native caption white even after the
        // immersive-dark flag. Explicit colors make NavBR independent from that system quirk.
        var caption = NavCaptionColor;
        var text = NavCaptionTextColor;
        var border = NavBorderColor;
        _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, Marshal.SizeOf<uint>());
        _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, Marshal.SizeOf<uint>());
        _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, Marshal.SizeOf<uint>());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref uint attributeValue,
        int attributeSize);
}
