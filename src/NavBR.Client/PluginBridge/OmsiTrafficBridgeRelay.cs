using System.Windows;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client.PluginBridge;

internal static class OmsiTrafficBridgeRelay
{
    public static Task ForwardTrafficSnapshotAsync(
        TrafficSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var message = new PluginBridgeMessage(
            PluginBridgeProtocol.TrafficSnapshotState,
            PluginBridgeProtocol.Version,
            MapName: snapshot.MapName,
            MapCompatibilityId: snapshot.MapCompatibilityId,
            TimestampUnixMilliseconds: snapshot.TimestampUtc.ToUnixTimeMilliseconds(),
            AuthorityPlayerId: snapshot.AuthorityPlayerId,
            Sequence: snapshot.Sequence,
            TrafficVehicles: snapshot.Vehicles.Take(48).ToArray());

        return SendBestEffortAsync(message, cancellationToken);
    }

    public static Task ClearTrafficAsync(CancellationToken cancellationToken = default) =>
        SendBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.ClearTrafficVehicles,
                PluginBridgeProtocol.Version),
            cancellationToken);

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
            // Traffic sync is experimental. Bridge failures must never break
            // the SignalR room or ordinary 2D multiplayer markers.
        }
    }
}
