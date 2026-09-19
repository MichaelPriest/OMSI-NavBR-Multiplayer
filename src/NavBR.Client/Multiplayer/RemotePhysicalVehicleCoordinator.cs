using System.Collections.Concurrent;
using System.Windows;
using NavBR.Client.Diagnostics;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

internal sealed record RemotePhysicalVehicleStatus(
    string State,
    string? ErrorCode,
    int? PartCount,
    int? ExpectedPartCount,
    DateTimeOffset UpdatedAtUtc);

internal sealed class RemotePhysicalVehicleCoordinator
{
    private const int MaxPhysicalRemotePlayers = 32;
    private const double PhysicalSpawnRadiusMeters = 750d;
    private const double PhysicalDespawnRadiusMeters = 1_000d;
    private const double CapacityReplacementMarginMeters = 75d;
    private static readonly TimeSpan CapacityEvictionCooldown =
        TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LocalTelemetryFreshness =
        TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<string, byte> _spawned = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _playerGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _lastFailureByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RemotePhysicalVehicleStatus> _statusByPlayer =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _spawnedCompatibilityByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _resolvedVehiclePathByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _consecutiveUpdateFailuresByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _distanceByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _capacityEvictionsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _capacitySuppressedUntilByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly OmsiVehicleAssetResolver _vehicleAssetResolver;
    private OmsiCompatibilityManifest? _localManifest;
    private VehicleTelemetry? _localTelemetry;

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

    public void SetLocalTelemetry(VehicleTelemetry? telemetry)
    {
        _localTelemetry = telemetry;
    }

    public bool IsSpawned(string playerId) =>
        !string.IsNullOrWhiteSpace(playerId) && _spawned.ContainsKey(playerId);

    public RemotePhysicalVehicleStatus GetStatus(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId) &&
            _statusByPlayer.TryGetValue(playerId, out var status))
        {
            return status;
        }

        var state = !ExperimentalFeatureFlags.PhysicalVehiclesEnabled
            ? "disabled"
            : !IsPhysicalMultiplayerAvailable
                ? "plugin-unavailable"
                : "waiting-telemetry";
        return new RemotePhysicalVehicleStatus(
            state,
            ErrorCode: null,
            PartCount: null,
            ExpectedPartCount: null,
            DateTimeOffset.UtcNow);
    }

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
        var playerId = frame.Player.PlayerId;
        if (!ExperimentalFeatureFlags.PhysicalVehiclesEnabled)
        {
            SetStatus(playerId, "disabled");
            if (_spawned.ContainsKey(playerId))
            {
                await DespawnOwnedAsync(playerId, cancellationToken);
            }
            return;
        }

        if (!frame.Telemetry.IsInGame)
        {
            SetStatus(playerId, "remote-not-in-game");
            if (_spawned.ContainsKey(playerId))
            {
                await DespawnOwnedAsync(playerId, cancellationToken);
            }
            return;
        }

        if (!IsPhysicalMultiplayerAvailable)
        {
            SetStatus(playerId, "plugin-unavailable");
            if (_spawned.ContainsKey(playerId))
            {
                await DespawnOwnedAsync(playerId, cancellationToken);
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
            SetStatus(playerId, "identity-missing");
            await DespawnOwnedAsync(playerId, cancellationToken);
            return;
        }

        // A missing local manifest means the client is disconnecting,
        // reconnecting or has not published a valid OMSI state yet. Never let
        // a late remote frame create a physical vehicle in that window.
        var localManifest = _localManifest;
        var localTelemetry = _localTelemetry;
        if (localManifest is null ||
            localTelemetry is null ||
            !localTelemetry.IsInGame ||
            DateTimeOffset.UtcNow - localTelemetry.Timestamp > LocalTelemetryFreshness)
        {
            SetStatus(playerId, "local-state-unavailable");
            await DespawnOwnedAsync(playerId, cancellationToken);
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
            SetStatus(
                playerId,
                "incompatible",
                report.Issues.FirstOrDefault()?.Code);
            await DespawnOwnedAsync(playerId, cancellationToken);
            return;
        }

        double? currentDistanceMeters = null;
        if (TryGetLocalDistanceMeters(frame.Telemetry, out var distanceMeters))
        {
            currentDistanceMeters = distanceMeters;
            _distanceByPlayer[playerId] = distanceMeters;

            var distanceLimit = _spawned.ContainsKey(playerId)
                ? PhysicalDespawnRadiusMeters
                : PhysicalSpawnRadiusMeters;
            if (distanceMeters > distanceLimit)
            {
                SetStatus(playerId, "out-of-range");
                if (_spawned.ContainsKey(playerId))
                {
                    await DespawnOwnedAsync(playerId, cancellationToken);
                    SetStatus(playerId, "out-of-range");
                }
                return;
            }
        }

        if (!_spawned.ContainsKey(playerId) &&
            _capacitySuppressedUntilByPlayer.TryGetValue(
                playerId,
                out var suppressedUntil))
        {
            if (suppressedUntil > DateTimeOffset.UtcNow)
            {
                SetStatus(playerId, "capacity-evicted");
                return;
            }

            _capacitySuppressedUntilByPlayer.TryRemove(playerId, out _);
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
            SetStatus(playerId, "switching-vehicle");
            await DespawnOwnedAsync(playerId, cancellationToken);
            if (_spawned.ContainsKey(frame.Player.PlayerId))
            {
                // Despawn failed and restored the old ownership state. Never
                // reuse that instance as if it represented the new vehicle.
                return;
            }
        }

        if (!_spawned.ContainsKey(playerId) &&
            _spawned.Count >= MaxPhysicalRemotePlayers)
        {
            if (currentDistanceMeters is double candidateDistance &&
                TryScheduleFartherVehicleEviction(
                    playerId,
                    candidateDistance))
            {
                SetStatus(playerId, "waiting-nearer-slot");
                return;
            }

            SetStatus(playerId, "limit-reached");
            ReportFailureOnce(playerId, "limit", "physical-vehicle-limit-reached");
            return;
        }

        if (!_spawned.ContainsKey(playerId))
        {
            SetStatus(playerId, "resolving-asset");
            var resolvedVehiclePath = await _vehicleAssetResolver.ResolveAsync(
                remoteManifest.VehiclePath,
                remoteVehicleCompatibilityId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(resolvedVehiclePath))
            {
                SetStatus(playerId, "asset-unresolved");
                ReportFailureOnce(
                    playerId,
                    "asset",
                    "physical-vehicle-asset-unresolved");
                return;
            }

            var consistInfo = await _vehicleAssetResolver.InspectConsistAsync(
                resolvedVehiclePath,
                cancellationToken);
            if (consistInfo is { ExpectedPartCount: > 1 })
            {
                SetStatus(
                    playerId,
                    "consist-unsupported",
                    "multi-vehicle-consist-declared",
                    expectedPartCount: consistInfo.ExpectedPartCount);
                ReportFailureOnce(
                    playerId,
                    "consist",
                    $"multi-vehicle-consist-declared expected-parts={consistInfo.ExpectedPartCount} complete={consistInfo.IsComplete}");
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
                SetStatus(playerId, "session-changed");
                return;
            }

            var spawnFrame = BuildPhysicalFrame(
                frame,
                remoteManifest,
                resolvedVehiclePath);
            if (_spawned.TryAdd(playerId, 0))
            {
                SetStatus(playerId, "spawning");
                var spawn = await OmsiPluginBridgeRelay.SpawnRemoteVehicleAsync(
                    spawnFrame,
                    cancellationToken);
                if (spawn?.Success != true)
                {
                    _spawned.TryRemove(playerId, out _);
                    ReportCommandFailureOnce(
                        playerId,
                        "spawn",
                        spawn,
                        consistInfo?.ExpectedPartCount);
                    return;
                }

                _spawnedCompatibilityByPlayer[frame.Player.PlayerId] =
                    remoteVehicleCompatibilityId;
                _resolvedVehiclePathByPlayer[frame.Player.PlayerId] =
                    resolvedVehiclePath;
                _consecutiveUpdateFailuresByPlayer.TryRemove(playerId, out _);
                _lastFailureByPlayer.TryRemove(playerId, out _);
                SetStatus(playerId, "active");
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
            await DespawnOwnedAsync(playerId, cancellationToken);
            SetStatus(playerId, "path-state-missing");
            ReportFailureOnce(
                playerId,
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
        if (update?.Success != true)
        {
            var errorCode = update?.ErrorCode ?? "no-result";
            var failureCount = _consecutiveUpdateFailuresByPlayer.AddOrUpdate(
                playerId,
                1,
                static (_, previous) => Math.Min(previous + 1, 10));

            if (IsFatalUpdateFailure(errorCode) || failureCount >= 3)
            {
                await DespawnOwnedAsync(playerId, cancellationToken);
                ReportCommandFailureOnce(playerId, "update", update);
                return;
            }

            SetStatus(
                playerId,
                "update-retrying",
                errorCode);
            return;
        }

        _consecutiveUpdateFailuresByPlayer.TryRemove(playerId, out _);
        _lastFailureByPlayer.TryRemove(playerId, out _);
        SetStatus(playerId, "active");
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

        _consecutiveUpdateFailuresByPlayer.TryRemove(playerId, out _);
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
        _consecutiveUpdateFailuresByPlayer.Clear();
        _distanceByPlayer.Clear();
        _capacityEvictionsInFlight.Clear();
        _capacitySuppressedUntilByPlayer.Clear();
        _statusByPlayer.Clear();
    }

    private bool TryScheduleFartherVehicleEviction(
        string candidatePlayerId,
        double candidateDistanceMeters)
    {
        // Only one capacity replacement may run at a time. Otherwise several
        // simultaneous near players could evict several far buses before the
        // first newly freed slot is consumed.
        if (!_capacityEvictionsInFlight.IsEmpty)
        {
            return true;
        }

        var farthest = _spawned.Keys
            .Where(id =>
                !string.Equals(
                    id,
                    candidatePlayerId,
                    StringComparison.OrdinalIgnoreCase) &&
                _distanceByPlayer.ContainsKey(id))
            .Select(id => new
            {
                PlayerId = id,
                Distance = _distanceByPlayer.TryGetValue(id, out var value)
                    ? value
                    : double.NaN
            })
            .Where(item => double.IsFinite(item.Distance))
            .OrderByDescending(item => item.Distance)
            .FirstOrDefault();

        if (farthest is null ||
            farthest.Distance - candidateDistanceMeters <
                CapacityReplacementMarginMeters ||
            !_capacityEvictionsInFlight.TryAdd(farthest.PlayerId, 0))
        {
            return false;
        }

        _ = EvictForCloserVehicleAsync(farthest.PlayerId);
        return true;
    }

    private async Task EvictForCloserVehicleAsync(string playerId)
    {
        try
        {
            await DespawnAsync(playerId);
            if (!IsSpawned(playerId))
            {
                _capacitySuppressedUntilByPlayer[playerId] =
                    DateTimeOffset.UtcNow + CapacityEvictionCooldown;
                SetStatus(playerId, "capacity-evicted");
                RemoteDiagnosticsService.Record(
                    "physical-vehicle",
                    "info",
                    "capacity-evicted-for-nearer-player");
            }
        }
        catch (Exception ex)
        {
            RemoteDiagnosticsService.Record(
                "physical-vehicle",
                "error",
                $"capacity-eviction-failed type={ex.GetType().Name}");
        }
        finally
        {
            _capacityEvictionsInFlight.TryRemove(playerId, out _);
        }
    }

    private static bool IsFatalUpdateFailure(string? errorCode) =>
        string.Equals(errorCode, "vehicle-not-owned", StringComparison.Ordinal) ||
        string.Equals(errorCode, "vehicle-pointer-stale", StringComparison.Ordinal) ||
        string.Equals(errorCode, "invalid-pose", StringComparison.Ordinal) ||
        string.Equals(errorCode, "invalid-instance-id", StringComparison.Ordinal) ||
        string.Equals(errorCode, "backend-unavailable", StringComparison.Ordinal) ||
        string.Equals(errorCode, "writes-disabled", StringComparison.Ordinal);

    private void ReportCommandFailureOnce(
        string playerId,
        string operation,
        PluginBridgeMessage? result,
        int? expectedPartCount = null)
    {
        var errorCode = result?.ErrorCode ?? "no-result";
        var detail = result?.ErrorMessage ?? string.Empty;
        var state = string.Equals(
                errorCode,
                "multi-vehicle-consist-unsupported",
                StringComparison.Ordinal)
            ? "consist-unsupported"
            : $"{operation}-failed";
        SetStatus(
            playerId,
            state,
            errorCode,
            result?.RemoteVehicleCount,
            expectedPartCount);
        ReportFailureOnce(
            playerId,
            operation,
            $"{operation}-failed error={errorCode} parts={result?.RemoteVehicleCount?.ToString() ?? "n/a"} expected-parts={expectedPartCount?.ToString() ?? "n/a"} detail={detail}");
    }

    private void SetStatus(
        string playerId,
        string state,
        string? errorCode = null,
        int? partCount = null,
        int? expectedPartCount = null)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        _statusByPlayer[playerId] = new RemotePhysicalVehicleStatus(
            state,
            string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim(),
            partCount is > 0 ? partCount : null,
            expectedPartCount is > 0 ? expectedPartCount : null,
            DateTimeOffset.UtcNow);
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

    private bool TryGetLocalDistanceMeters(
        VehicleTelemetry remoteTelemetry,
        out double distanceMeters)
    {
        distanceMeters = 0d;
        var local = _localTelemetry;
        if (local is null ||
            !local.IsInGame ||
            !remoteTelemetry.IsInGame ||
            !double.IsFinite(local.X) ||
            !double.IsFinite(local.Y) ||
            !double.IsFinite(local.Z) ||
            !double.IsFinite(remoteTelemetry.X) ||
            !double.IsFinite(remoteTelemetry.Y) ||
            !double.IsFinite(remoteTelemetry.Z))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(local.MapCompatibilityId) &&
            !string.IsNullOrWhiteSpace(remoteTelemetry.MapCompatibilityId) &&
            !string.Equals(
                local.MapCompatibilityId,
                remoteTelemetry.MapCompatibilityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dx = remoteTelemetry.X - local.X;
        var dy = remoteTelemetry.Y - local.Y;
        var dz = remoteTelemetry.Z - local.Z;
        distanceMeters = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return double.IsFinite(distanceMeters);
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
