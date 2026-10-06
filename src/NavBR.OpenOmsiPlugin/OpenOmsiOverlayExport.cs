using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal static class OpenOmsiOverlayExport
{
    private const int MaximumPayloadBytes = 262_144;
    private static byte[] _latestV1 = [];
    private static byte[] _latestV2 = [];

    internal static void Publish(
        OpenOmsiOverlayFrameState frame,
        OpenOmsiOverlay2DFrameState overlay2D,
        OpenOmsiWorldGuidanceFrameState world)
    {
        try
        {
            PublishJson(
                frame,
                OpenOmsiPluginJsonContext.Default.OpenOmsiOverlayFrameState,
                ref _latestV1);

            var envelope = new OpenOmsiOverlayExportEnvelopeV2(
                Version: 2,
                TimestampUnixMilliseconds: frame.TimestampUnixMilliseconds,
                Overlay2D: overlay2D,
                WorldGuidance: world);
            PublishJson(
                envelope,
                OpenOmsiPluginJsonContext.Default.OpenOmsiOverlayExportEnvelopeV2,
                ref _latestV2);
        }
        catch
        {
        }
    }

    private static void PublishJson<T>(
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        ref byte[] target)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length <= MaximumPayloadBytes)
        {
            Volatile.Write(ref target, bytes);
        }
    }

    internal static int RequiredBytes =>
        Volatile.Read(ref _latestV1).Length + 1;

    internal static int RequiredBytesV2 =>
        Volatile.Read(ref _latestV2).Length + 1;

    internal static int PayloadBytesV2 =>
        Volatile.Read(ref _latestV2).Length;

    internal static int MaximumPayloadBytesForSmoke =>
        MaximumPayloadBytes;

    internal static int CopyLatest(Span<byte> destination) =>
        Copy(Volatile.Read(ref _latestV1), destination);

    internal static int CopyLatestV2(Span<byte> destination) =>
        Copy(Volatile.Read(ref _latestV2), destination);

    private static int Copy(byte[] payload, Span<byte> destination)
    {
        var required = payload.Length + 1;
        if (destination.Length < required)
        {
            return required;
        }

        payload.CopyTo(destination);
        destination[payload.Length] = 0;
        return payload.Length;
    }

    internal static void Reset()
    {
        Volatile.Write(ref _latestV1, []);
        Volatile.Write(ref _latestV2, []);
    }

    [UnmanagedCallersOnly(
        CallConvs = [typeof(CallConvStdcall)],
        EntryPoint = "OpenOmsiGetOverlayFrame")]
    public static unsafe int GetOverlayFrame(
        byte* buffer,
        int capacity)
    {
        try
        {
            var payload = Volatile.Read(ref _latestV1);
            var required = payload.Length + 1;

            // Probe mode: the host asks how much room is needed.
            if (buffer == null || capacity <= 0)
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

    [UnmanagedCallersOnly(
        CallConvs = [typeof(CallConvStdcall)],
        EntryPoint = "OpenOmsiGetOverlayFrameV2")]
    public static unsafe int GetOverlayFrameV2(
        byte* buffer,
        int capacity)
    {
        try
        {
            var payload = Volatile.Read(ref _latestV2);
            var required = payload.Length + 1;
            if (buffer == null || capacity <= 0)
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
