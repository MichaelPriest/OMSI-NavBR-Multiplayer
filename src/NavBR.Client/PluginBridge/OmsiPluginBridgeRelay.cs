using System.Windows;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client.PluginBridge;

public static class OmsiPluginBridgeRelay
{
    public static Task ForwardRemoteTelemetryAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default)
    {
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

        return SendBestEffortAsync(message, cancellationToken);
    }

    public static Task RemoveRemotePlayerAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Task.CompletedTask;
        }

        var message = new PluginBridgeMessage(
            PluginBridgeProtocol.RemoteVehicleRemoved,
            PluginBridgeProtocol.Version,
            PlayerId: playerId);

        return SendBestEffortAsync(message, cancellationToken);
    }

    public static Task ClearRemotePlayersAsync(CancellationToken cancellationToken = default)
    {
        var message = new PluginBridgeMessage(
            PluginBridgeProtocol.ClearRemoteVehicles,
            PluginBridgeProtocol.Version);

        return SendBestEffortAsync(message, cancellationToken);
    }

    private static async Task SendBestEffortAsync(
        PluginBridgeMessage message,
        CancellationToken cancellationToken)
    {
        if (Application.Current is not App app || !app.PluginBridge.IsConnected)
        {
            return;
        }

        try
        {
            await app.PluginBridge.SendMessageAsync(message, cancellationToken);
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
