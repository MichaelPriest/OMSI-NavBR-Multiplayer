using System.Collections.Concurrent;
using System.Windows;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client.Multiplayer;

internal sealed class RemotePhysicalVehicleCoordinator
{
    private readonly ConcurrentDictionary<string, byte> _spawned = new(StringComparer.OrdinalIgnoreCase);
    private OmsiCompatibilityManifest? _localManifest;

    public bool IsPhysicalMultiplayerAvailable
    {
        get
        {
            if (Application.Current is not App app || !app.PluginBridge.IsConnected)
            {
                return false;
            }

            return app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityVehicleSpawn) &&
                   app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityVehicleTransform);
        }
    }

    public void SetLocalManifest(OmsiCompatibilityManifest? manifest)
    {
        _localManifest = manifest;
    }

    public async Task ApplyAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default)
    {
        if (!IsPhysicalMultiplayerAvailable ||
            !frame.Telemetry.IsInGame ||
            string.IsNullOrWhiteSpace(frame.Player.PlayerId))
        {
            return;
        }

        var report = OmsiCompatibilityEvaluator.Compare(
            _localManifest,
            frame.Player.Compatibility,
            requireVehicleForPhysicalMultiplayer: true);
        if (!report.IsCompatible)
        {
            await DespawnAsync(frame.Player.PlayerId, cancellationToken);
            return;
        }

        if (_spawned.TryAdd(frame.Player.PlayerId, 0))
        {
            var spawn = await OmsiPluginBridgeRelay.SpawnRemoteVehicleAsync(
                frame,
                cancellationToken);
            if (spawn?.Success != true)
            {
                _spawned.TryRemove(frame.Player.PlayerId, out _);
                return;
            }
        }

        var update = await OmsiPluginBridgeRelay.UpdateRemoteVehicleAsync(
            frame,
            cancellationToken);
        if (update is { Success: false })
        {
            _spawned.TryRemove(frame.Player.PlayerId, out _);
        }
    }

    public async Task DespawnAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        if (!_spawned.TryRemove(playerId, out _))
        {
            return;
        }

        await OmsiPluginBridgeRelay.DespawnRemoteVehicleAsync(
            playerId,
            cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var players = _spawned.Keys.ToArray();
        _spawned.Clear();

        foreach (var playerId in players)
        {
            await OmsiPluginBridgeRelay.DespawnRemoteVehicleAsync(
                playerId,
                cancellationToken);
        }
    }
}
