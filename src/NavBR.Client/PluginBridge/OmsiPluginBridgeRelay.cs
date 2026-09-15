using System.Windows;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client.PluginBridge;

public static class OmsiPluginBridgeRelay
{
    public static async Task ForwardRemoteTelemetryAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default)
    {
        if (Application.Current is not App app || !app.PluginBridge.IsConnected)
        {
            return;
        }

        var telemetry = frame.Telemetry;
        var message = new PluginBridgeMessage(
            PluginBridgeProtocol.RemoteVehicleState,
            PluginBridgeProtocol.Version,
            PlayerId: frame.Player.PlayerId,
            DisplayName: frame.Player.DisplayName,
            MapName: telemetry.MapName ?? frame.Player.MapName,
            MapCompatibilityId: frame.Player.MapCompatibilityId ?? telemetry.MapCompatibilityId,
            TimestampUnixMilliseconds: telemetry.Timestamp.ToUnixTimeMilliseconds(),
            X: telemetry.X,
            Y: telemetry.Y,
            Z: telemetry.Z,
            GridX: telemetry.GridX,
            GridY: telemetry.GridY,
            TileX: telemetry.TileX,
            TileY: telemetry.TileY,
            HeadingDegrees: telemetry.HeadingDegrees,
            SpeedKph: telemetry.SpeedKph);

        try
        {
            await app.PluginBridge.SendRemoteVehicleStateAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // O bridge é experimental e opcional. Falhas locais nunca devem
            // afetar a sessão multiplayer, o HUD ou a conexão SignalR.
        }
    }
}
