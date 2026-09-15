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

        // Vehicle identity is refreshed on every telemetry frame. Presence is
        // created when the player joins and can therefore predate the moment in
        // which OMSI has a fully resolved .bus/.ovh definition.
        var remoteManifest = BuildLiveRemoteManifest(frame);
        if (string.IsNullOrWhiteSpace(remoteManifest.VehiclePath) ||
            string.IsNullOrWhiteSpace(remoteManifest.VehicleCompatibilityId))
        {
            await DespawnAsync(frame.Player.PlayerId, cancellationToken);
            return;
        }

        // Physical rendering only requires the same map/protocol. Players do
        // not need to be driving the same bus: the receiver creates the remote
        // player's actual vehicle asset from VehiclePath and validates its
        // fingerprint separately in the OMSI plugin backend.
        var report = OmsiCompatibilityEvaluator.Compare(
            _localManifest,
            remoteManifest,
            requireVehicleForPhysicalMultiplayer: false);
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

    private static OmsiCompatibilityManifest BuildLiveRemoteManifest(PlayerTelemetryFrame frame)
    {
        var telemetry = frame.Telemetry;
        var reported = frame.Player.Compatibility;

        if (reported is not null)
        {
            return reported with
            {
                MapName = telemetry.MapName ?? reported.MapName,
                MapCompatibilityId = telemetry.MapCompatibilityId ?? reported.MapCompatibilityId,
                VehiclePath = telemetry.VehiclePath ?? reported.VehiclePath,
                VehicleCompatibilityId = telemetry.VehicleCompatibilityId ?? reported.VehicleCompatibilityId,
                HofName = telemetry.HofName ?? reported.HofName,
                HofCompatibilityId = telemetry.HofCompatibilityId ?? reported.HofCompatibilityId
            };
        }

        return new OmsiCompatibilityManifest(
            OmsiVersion: null,
            NavBRVersion: null,
            MapName: telemetry.MapName ?? frame.Player.MapName,
            MapCompatibilityId: telemetry.MapCompatibilityId ?? frame.Player.MapCompatibilityId,
            VehiclePath: telemetry.VehiclePath,
            VehicleCompatibilityId: telemetry.VehicleCompatibilityId,
            HofName: telemetry.HofName,
            HofCompatibilityId: telemetry.HofCompatibilityId,
            PluginProtocolVersion: PluginBridgeProtocol.Version,
            PluginDeployment: null,
            Capabilities: Array.Empty<string>());
    }
}
