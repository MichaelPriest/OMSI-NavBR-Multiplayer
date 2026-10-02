using NavBR.Client.PluginBridge;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Telemetry;

internal static class OpenOmsiBridgeTelemetryProvider
{
    private static readonly TimeSpan ActiveFreshness =
        TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PausedFreshness =
        TimeSpan.FromMinutes(30);

    public static bool IsOpenOmsiRuntime(
        OmsiPluginBridgeConnectionInfo? connection)
    {
        if (connection?.IsConnected != true)
        {
            return false;
        }

        return HasCapability(
            connection.LastCapabilities,
            PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin) ||
               HasCapability(
                   connection.LastStatus,
                   PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin);
    }

    public static VehicleTelemetry? Read(
        string playerId,
        OmsiPluginBridgeConnectionInfo? connection)
    {
        if (!IsOpenOmsiRuntime(connection) ||
            connection?.LastStatus is not { } status ||
            string.IsNullOrWhiteSpace(status.MapName) ||
            status.X is not double x ||
            status.Y is not double y ||
            status.Z is not double z ||
            status.HeadingDegrees is not double heading ||
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(z) ||
            !double.IsFinite(heading))
        {
            return null;
        }

        var capturedAt = ReadTimestamp(status.TimestampUnixMilliseconds);
        if (capturedAt is null)
        {
            return null;
        }

        var maxAge = status.SimulationPaused == true
            ? PausedFreshness
            : ActiveFreshness;
        var age = DateTimeOffset.UtcNow - capturedAt.Value;
        if (age < TimeSpan.FromSeconds(-5) || age > maxAge)
        {
            return null;
        }

        var speed = status.SpeedKph is double rawSpeed &&
                    double.IsFinite(rawSpeed)
            ? Math.Abs(rawSpeed)
            : 0d;

        var normalizedHeading = heading % 360d;
        if (normalizedHeading < 0d)
        {
            normalizedHeading += 360d;
        }

        return new VehicleTelemetry(
            playerId,
            capturedAt.Value,
            status.MapName.Trim(),
            TrimOrNull(status.VehicleName),
            TrimOrNull(status.Line ?? status.IbisLineCourse),
            TrimOrNull(status.Route ?? status.IbisRouteCode),
            x,
            y,
            z,
            normalizedHeading,
            speed,
            status.IsInGame ?? true,
            NextStopName: TrimOrNull(status.NextStopName),
            DestinationName: TrimOrNull(
                status.DestinationName ?? status.IbisTerminusName),
            DelaySeconds: status.DelaySeconds,
            LocalX: x,
            LocalY: y,
            LocalZ: z,
            StopRequested: status.StopRequested == true,
            SourceTimestampUnixMilliseconds:
                status.TimestampUnixMilliseconds);
    }

    private static bool HasCapability(
        PluginBridgeMessage? message,
        string capability) =>
        message?.Capabilities?.Contains(
            capability,
            StringComparer.OrdinalIgnoreCase) == true;

    private static DateTimeOffset? ReadTimestamp(long? milliseconds)
    {
        if (milliseconds is not long value)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
