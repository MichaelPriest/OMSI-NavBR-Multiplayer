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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _playerGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _lastFailureByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _spawnedCompatibilityByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _resolvedVehiclePathByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly OmsiVehicleAssetResolver _vehicleAssetResolver;
    private OmsiCompatibilityManifest? _localManifest;

    public RemotePhysicalVehicleCoordinator(
        Func<string?>? omsiInstallDirectorySource = null)
    {
        _vehicleAssetResolver = new OmsiVehicleAssetResolver(
            omsiInstallDirectorySource ?? (() => null));
    }

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
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var gate = _playerGates.GetOrAdd(
            playerId,
            static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            // Telemetry is high-frequency. Drop overlapping frames instead of
            // building an unbounded per-player queue behind asset resolution.
            return;
        }

        try
        {
            await ApplyCoreAsync(frame, cancellationToken);
        }
        finally
        {
            gate.Release();
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
                await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
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
            await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
            return;
        }

        // A missing local manifest means the client is disconnecting,
        // reconnecting or has not published a valid OMSI state yet. Never let
        // a late remote frame create a physical vehicle in that window.
        var localManifest = _localManifest;
        if (localManifest is null)
        {
            await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
            return;
        }

        // Physical rendering only requires the same map/protocol. Players do
        // not need to be driving the same bus: the desktop client resolves the
        // remote asset by its content fingerprint before the guarded plugin
        // receives the local Vehicles path.
        var report = OmsiCompatibilityEvaluator.Compare(
            localManifest,
            remoteManifest,
            requireVehicleForPhysicalMultiplayer: false);
        if (!report.IsCompatible)
        {
            await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
            return;
        }

        var remoteVehicleCompatibilityId =
            remoteManifest.VehicleCompatibilityId.Trim();
        if (_spawned.ContainsKey(frame.Player.PlayerId) &&
            (!_spawnedCompatibilityByPlayer.TryGetValue(
                 frame.Player.PlayerId,
                 out var spawnedCompatibilityId) ||
             !string.Equals(
                 spawnedCompatibilityId,
                 remoteVehicleCompatibilityId,
                 StringComparison.OrdinalIgnoreCase)))
        {
            // The remote player changed vehicle. Remove the old NavBR-owned
            // instance before creating the newly reported asset.
            await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
            if (_spawned.ContainsKey(frame.Player.PlayerId))
            {
                // Despawn failed and restored the old ownership state. Never
                // reuse that instance as if it represented the new vehicle.
                return;
            }
        }

        if (!_spawned.ContainsKey(frame.Player.PlayerId) &&
            _spawned.Count >= MaxPhysicalRemotePlayers)
        {
            ReportFailureOnce(frame.Player.PlayerId, "limit", "physical-vehicle-limit-reached");
            return;
        }

        if (!_spawned.ContainsKey(frame.Player.PlayerId))
        {
            var resolvedVehiclePath = await _vehicleAssetResolver.ResolveAsync(
                remoteManifest.VehiclePath,
                remoteVehicleCompatibilityId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(resolvedVehiclePath))
            {
                ReportFailureOnce(
                    frame.Player.PlayerId,
                    "asset",
                    "physical-vehicle-asset-unresolved");
                return;
            }

            // Asset resolution can take time on a large Vehicles folder.
            // Re-check the live local session after that await so a disconnect,
            // plugin shutdown or map change cannot race into a late spawn.
            var currentLocalManifest = _localManifest;
            if (!IsPhysicalMultiplayerAvailable ||
                currentLocalManifest is null ||
                !OmsiCompatibilityEvaluator.Compare(
                    currentLocalManifest,
                    remoteManifest,
                    requireVehicleForPhysicalMultiplayer: false).IsCompatible)
            {
                return;
            }

            var spawnFrame = BuildPhysicalFrame(
                frame,
                remoteManifest,
                resolvedVehiclePath);
            if (_spawned.TryAdd(frame.Player.PlayerId, 0))
            {
                var spawn = await OmsiPluginBridgeRelay.SpawnRemoteVehicleAsync(
                    spawnFrame,
                    cancellationToken);
                if (spawn?.Success != true)
                {
                    _spawned.TryRemove(frame.Player.PlayerId, out _);
                    ReportCommandFailureOnce(frame.Player.PlayerId, "spawn", spawn);
                    return;
                }

                _spawnedCompatibilityByPlayer[frame.Player.PlayerId] =
                    remoteVehicleCompatibilityId;
                _resolvedVehiclePathByPlayer[frame.Player.PlayerId] =
                    resolvedVehiclePath;
                _lastFailureByPlayer.TryRemove(frame.Player.PlayerId, out _);
                RemoteDiagnosticsService.Record(
                    "physical-vehicle",
                    "info",
                    "spawn-success");
            }
        }

        if (!_resolvedVehiclePathByPlayer.TryGetValue(
                frame.Player.PlayerId,
                out var localVehiclePath))
        {
            await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
            ReportFailureOnce(
                frame.Player.PlayerId,
                "asset",
                "physical-vehicle-path-state-missing");
            return;
        }

        var physicalFrame = BuildPhysicalFrame(
            frame,
            remoteManifest,
            localVehiclePath);
        var update = await OmsiPluginBridgeRelay.UpdateRemoteVehicleAsync(
            physicalFrame,
            cancellationToken);
        if (update is { Success: false })
        {
            await DespawnOwnedAsync(frame.Player.PlayerId, cancellationToken);
            ReportCommandFailureOnce(frame.Player.PlayerId, "update", update);
            return;
        }

        _lastFailureByPlayer.TryRemove(frame.Player.PlayerId, out _);
    }

    public async Task DespawnAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var gate = _playerGates.GetOrAdd(
            playerId,
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await DespawnOwnedAsync(playerId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task DespawnOwnedAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        if (!_spawned.TryRemove(playerId, out _))
        {
            _spawnedCompatibilityByPlayer.TryRemove(playerId, out _);
            _resolvedVehiclePathByPlayer.TryRemove(playerId, out _);
            return;
        }

        _spawnedCompatibilityByPlayer.TryRemove(
            playerId,
            out var previousCompatibilityId);
        _resolvedVehiclePathByPlayer.TryRemove(
            playerId,
            out var previousVehiclePath);

        var result = await OmsiPluginBridgeRelay.DespawnRemoteVehicleAsync(
            playerId,
            cancellationToken);
        if (result is { Success: false })
        {
            _spawned.TryAdd(playerId, 0);
            if (!string.IsNullOrWhiteSpace(previousCompatibilityId))
            {
                _spawnedCompatibilityByPlayer[playerId] =
                    previousCompatibilityId;
            }
            if (!string.IsNullOrWhiteSpace(previousVehiclePath))
            {
                _resolvedVehiclePathByPlayer[playerId] =
                    previousVehiclePath;
            }

            ReportCommandFailureOnce(playerId, "despawn", result);
            return;
        }

        _lastFailureByPlayer.TryRemove(playerId, out _);
        RemoteDiagnosticsService.Record(
            "physical-vehicle",
            "info",
            "despawn-success");
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var players = _spawned.Keys
            .Concat(_playerGates.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var playerId in players)
        {
            await DespawnAsync(playerId, cancellationToken);
        }

        _spawnedCompatibilityByPlayer.Clear();
        _resolvedVehiclePathByPlayer.Clear();
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

    private static PlayerTelemetryFrame BuildPhysicalFrame(
        PlayerTelemetryFrame frame,
        OmsiCompatibilityManifest remoteManifest,
        string resolvedVehiclePath) =>
        frame with
        {
            Telemetry = frame.Telemetry with
            {
                VehiclePath = resolvedVehiclePath,
                VehicleCompatibilityId = remoteManifest.VehicleCompatibilityId,
                HofName = remoteManifest.HofName,
                HofCompatibilityId = remoteManifest.HofCompatibilityId
            }
        };

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
