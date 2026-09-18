using System.Collections.Concurrent;
using System.Windows;
using NavBR.Client.Diagnostics;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client.Multiplayer;

internal sealed class RemotePhysicalVehicleCoordinator
{
    private const int MaxPhysicalRemotePlayers = 32;

    private readonly ConcurrentDictionary<string, byte> _spawned = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _lastFailureByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private OmsiCompatibilityManifest? _localManifest;

    public bool IsPhysicalMultiplayerAvailable
    {
        get
        {
            if (!ExperimentalFeatureFlags.PhysicalVehiclesEnabled ||
                Application.Current is not App app ||
                !app.PluginBridge.IsConnected)
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

    public bool IsSpawned(string playerId) =>
        !string.IsNullOrWhiteSpace(playerId) && _spawned.ContainsKey(playerId);

    public async Task ApplyAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default)
    {
        var playerId = frame.Player.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId) ||
            !_inFlight.TryAdd(playerId, 0))
        {
            return;
        }

        try
        {
            await ApplyCoreAsync(frame, cancellationToken);
        }
        finally
        {
            _inFlight.TryRemove(playerId, out _);
        }
    }

    private async Task ApplyCoreAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken)
    {
        if (!IsPhysicalMultiplayerAvailable ||
            !frame.Telemetry.IsInGame ||
            string.IsNullOrWhiteSpace(frame.Player.PlayerId))
        {
            if (_spawned.ContainsKey(frame.Player.PlayerId))
            {
                await DespawnAsync(frame.Player.PlayerId, cancellationToken);
            }
            return;
        }

        // Vehicle identity is refreshed on every telemetry frame. Presence is
        // created when the player joins and can therefore predate the moment in
        // which OMSI has a fully resolved .bus/.ovh definition.
        var remoteManifest = BuildLiveRemoteManifest(frame);
        // The content fingerprint is authoritative for physical spawning.
        // VehiclePath is only a location hint and can differ between installs.
        if (string.IsNullOrWhiteSpace(remoteManifest.VehicleCompatibilityId))
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

        var physicalFrame = frame with
        {
            Telemetry = frame.Telemetry with
            {
                VehiclePath = remoteManifest.VehiclePath,
                VehicleCompatibilityId = remoteManifest.VehicleCompatibilityId,
                HofName = remoteManifest.HofName,
                HofCompatibilityId = remoteManifest.HofCompatibilityId
            }
        };

        if (!_spawned.ContainsKey(frame.Player.PlayerId) &&
            _spawned.Count >= MaxPhysicalRemotePlayers)
        {
            ReportFailureOnce(frame.Player.PlayerId, "limit", "physical-vehicle-limit-reached");
            return;
        }

        if (_spawned.TryAdd(frame.Player.PlayerId, 0))
        {
            var spawn = await OmsiPluginBridgeRelay.SpawnRemoteVehicleAsync(
                physicalFrame,
                cancellationToken);
            if (spawn?.Success != true)
            {
                _spawned.TryRemove(frame.Player.PlayerId, out _);
                ReportCommandFailureOnce(frame.Player.PlayerId, "spawn", spawn);
                return;
            }

            _lastFailureByPlayer.TryRemove(frame.Player.PlayerId, out _);
            RemoteDiagnosticsService.Record("physical-vehicle", "info", "spawn-success");
        }

        var update = await OmsiPluginBridgeRelay.UpdateRemoteVehicleAsync(
            physicalFrame,
            cancellationToken);
        if (update is { Success: false })
        {
            _spawned.TryRemove(frame.Player.PlayerId, out _);
            ReportCommandFailureOnce(frame.Player.PlayerId, "update", update);
            return;
        }

        _lastFailureByPlayer.TryRemove(frame.Player.PlayerId, out _);
    }

    public async Task DespawnAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        if (!_spawned.TryRemove(playerId, out _))
        {
            return;
        }

        var result = await OmsiPluginBridgeRelay.DespawnRemoteVehicleAsync(
            playerId,
            cancellationToken);
        if (result is { Success: false })
        {
            ReportCommandFailureOnce(playerId, "despawn", result);
            return;
        }

        _lastFailureByPlayer.TryRemove(playerId, out _);
        RemoteDiagnosticsService.Record("physical-vehicle", "info", "despawn-success");
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var players = _spawned.Keys.ToArray();
        _spawned.Clear();

        foreach (var playerId in players)
        {
            var result = await OmsiPluginBridgeRelay.DespawnRemoteVehicleAsync(
                playerId,
                cancellationToken);
            if (result is { Success: false })
            {
                ReportCommandFailureOnce(playerId, "despawn", result);
            }
            else
            {
                _lastFailureByPlayer.TryRemove(playerId, out _);
            }
        }
    }

    private void ReportCommandFailureOnce(
        string playerId,
        string operation,
        PluginBridgeMessage? result)
    {
        var errorCode = result?.ErrorCode ?? "no-result";
        var detail = result?.ErrorMessage ?? string.Empty;
        ReportFailureOnce(
            playerId,
            operation,
            $"{operation}-failed error={errorCode} detail={detail}");
    }

    private void ReportFailureOnce(string playerId, string key, string message)
    {
        var fingerprint = $"{key}:{message}";
        if (_lastFailureByPlayer.TryGetValue(playerId, out var previous) &&
            string.Equals(previous, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _lastFailureByPlayer[playerId] = fingerprint;
        RemoteDiagnosticsService.Record("physical-vehicle", "error", message);
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
