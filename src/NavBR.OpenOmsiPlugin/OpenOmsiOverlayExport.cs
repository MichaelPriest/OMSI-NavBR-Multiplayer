using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal static class OpenOmsiOverlayExport
{
    private const int MaximumPayloadBytes = 262_144;
    private static byte[] _latest = [];

    internal static void Publish(OpenOmsiOverlayFrameState frame)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                frame,
                OpenOmsiPluginJsonContext.Default.OpenOmsiOverlayFrameState);
            var bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > MaximumPayloadBytes)
            {
                return;
            }

            Volatile.Write(ref _latest, bytes);
        }
        catch
        {
        }
    }

    internal static int RequiredBytes =>
        Volatile.Read(ref _latest).Length + 1;

    internal static int CopyLatest(Span<byte> destination)
    {
        var payload = Volatile.Read(ref _latest);
        var required = payload.Length + 1;
        if (destination.Length < required)
        {
            return required;
        }

        payload.CopyTo(destination);
        destination[payload.Length] = 0;
        return payload.Length;
    }

    internal static void Reset() =>
        Volatile.Write(ref _latest, []);

    [UnmanagedCallersOnly(
        CallConvs = [typeof(CallConvStdcall)],
        EntryPoint = "OpenOmsiGetOverlayFrame")]
    public static unsafe int GetOverlayFrame(
        byte* buffer,
        int capacity)
    {
        try
        {
            var payload = Volatile.Read(ref _latest);
            var required = payload.Length + 1;

            // Probe mode: the host asks how much room is needed.
            if (buffer is null || capacity <= 0)
            {
                return required;
            }

            if (capacity < required)
            {
                return required;
            }

            var destination = new Span<byte>(buffer, capacity);
            payload.CopyTo(destination);
            destination[payload.Length] = 0;
            return payload.Length;
        }
        catch
        {
            return 0;
        }
    }
}
