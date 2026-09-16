using System.Reflection;
using System.Windows;
using NavBR.Client.Maps;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

internal static class OmsiCompatibilityManifestFactory
{
    public static OmsiCompatibilityManifest Create(
        VehicleTelemetry? telemetry,
        OmsiMapInfo? activeMap,
        string? omsiVersion = null)
    {
        return CreateCore(
            telemetry,
            telemetry?.MapName ?? activeMap?.FolderName,
            telemetry?.MapCompatibilityId ?? activeMap?.CompatibilityId,
            omsiVersion);
    }

    public static OmsiCompatibilityManifest Create(
        string? mapName,
        string? mapCompatibilityId,
        string? omsiVersion = null)
    {
        return CreateCore(
            telemetry: null,
            mapName,
            mapCompatibilityId,
            omsiVersion);
    }

    private static OmsiCompatibilityManifest CreateCore(
        VehicleTelemetry? telemetry,
        string? mapName,
        string? mapCompatibilityId,
        string? omsiVersion)
    {
        var connection = Application.Current is App app
            ? app.PluginBridge.GetConnectionInfo()
            : null;

        var pluginCapabilities = connection?.LastCapabilities?.Capabilities ?? Array.Empty<string>();
        var capabilities = new HashSet<string>(pluginCapabilities, StringComparer.OrdinalIgnoreCase)
        {
            PluginBridgeProtocol.CapabilityAdvancedTelemetry,
            PluginBridgeProtocol.CapabilityGhostReplay,
            PluginBridgeProtocol.CapabilityTimetableState
        };

        return new OmsiCompatibilityManifest(
            OmsiVersion: Normalize(omsiVersion),
            NavBRVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            MapName: Normalize(mapName),
            MapCompatibilityId: Normalize(mapCompatibilityId),
            VehiclePath: Normalize(telemetry?.VehiclePath),
            VehicleCompatibilityId: Normalize(telemetry?.VehicleCompatibilityId),
            HofName: Normalize(telemetry?.HofName),
            HofCompatibilityId: Normalize(telemetry?.HofCompatibilityId),
            PluginProtocolVersion: PluginBridgeProtocol.Version,
            PluginDeployment: connection?.IsConnected == true ? "NATIVE-AOT-X86" : null,
            Capabilities: capabilities.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
