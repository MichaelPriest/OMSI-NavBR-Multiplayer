using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiHudConfiguration(
    bool MiniMapEnabled,
    bool FullMapEnabled,
    bool AutoZoomEnabled,
    bool FollowVehicleEnabled,
    bool TimetableEnabled,
    bool TeleMatrixEnabled,
    bool TrafficEnabled,
    bool MultiplayerEnabled,
    bool CongestionEnabled,
    bool RouteGuidanceEnabled)
{
    public static OpenOmsiHudConfiguration Default { get; } = new(
        MiniMapEnabled: true,
        FullMapEnabled: true,
        AutoZoomEnabled: true,
        FollowVehicleEnabled: true,
        TimetableEnabled: true,
        TeleMatrixEnabled: true,
        TrafficEnabled: true,
        MultiplayerEnabled: true,
        CongestionEnabled: true,
        RouteGuidanceEnabled: true);

    public OpenOmsiHudConfiguration Apply(PluginBridgeMessage message) => this with
    {
        MiniMapEnabled = message.OpenOmsiMiniMapEnabled ?? MiniMapEnabled,
        FullMapEnabled = message.OpenOmsiFullMapEnabled ?? FullMapEnabled,
        AutoZoomEnabled = message.OpenOmsiAutoZoomEnabled ?? AutoZoomEnabled,
        FollowVehicleEnabled = message.OpenOmsiFollowVehicleEnabled ?? FollowVehicleEnabled,
        TimetableEnabled = message.OpenOmsiTimetableHudEnabled ?? TimetableEnabled,
        TeleMatrixEnabled = message.OpenOmsiTeleMatrixEnabled ?? TeleMatrixEnabled,
        TrafficEnabled = message.OpenOmsiTrafficLayerEnabled ?? TrafficEnabled,
        MultiplayerEnabled = message.OpenOmsiMultiplayerLayerEnabled ?? MultiplayerEnabled,
        CongestionEnabled = message.OpenOmsiCongestionLayerEnabled ?? CongestionEnabled,
        RouteGuidanceEnabled = message.OpenOmsiRouteGuidanceEnabled ?? RouteGuidanceEnabled
    };
}

internal static class OpenOmsiHudState
{
    private static readonly object Sync = new();
    private static OpenOmsiHudConfiguration _configuration =
        OpenOmsiHudConfiguration.Default;

    public static OpenOmsiHudConfiguration Current
    {
        get
        {
            lock (Sync)
            {
                return _configuration;
            }
        }
    }

    public static OpenOmsiHudConfiguration Apply(PluginBridgeMessage message)
    {
        lock (Sync)
        {
            _configuration = _configuration.Apply(message);
            return _configuration;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _configuration = OpenOmsiHudConfiguration.Default;
        }
    }
}
