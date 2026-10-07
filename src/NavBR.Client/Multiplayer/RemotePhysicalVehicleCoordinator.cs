using System.Collections.Concurrent;
using System.Windows;
using NavBR.Client.Diagnostics;
using NavBR.Client.Maps;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

internal sealed record RemotePhysicalVehicleStatus(
    string State,
    string? ErrorCode,
    string? ErrorMessage,
    int? PartCount,
    int? ExpectedPartCount,
    DateTimeOffset UpdatedAtUtc);

internal sealed class RemotePhysicalVehicleCoordinator
{
    private const int MaxPhysicalRemotePlayers = 12;
    private const double PhysicalSpawnRadiusMeters = 500d;
    private const double PhysicalDespawnRadiusMeters = 700d;
    private const double CapacityReplacementMarginMeters = 75d;
    private static readonly TimeSpan CapacityEvictionCooldown =
        TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TileUnavailableRetryDelay =
        TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SpawnFailureRetryDelay =
        TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LocalTelemetryFreshness =
        TimeSpan.FromSeconds(3);
    private static readonly TimeSpan VehicleIdentityChangeDebounce =
        TimeSpan.FromSeconds(1.5);
    private const int VehicleIdentityChangeMinObservations = 4;

    private readonly ConcurrentDictionary<string, byte> _spawned = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _spawnInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _spawnPending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _playerGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _spawnLifecycleGate = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _lastFailureByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RemotePhysicalVehicleStatus> _statusByPlayer =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _spawnedCompatibilityByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _resolvedVehiclePathByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _consecutiveUpdateFailuresByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _distanceByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _capacityEvictionsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _capacitySuppressedUntilByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _spawnRetryAfterByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPhysicalUpdateAtByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, VehicleIdentityCandidate> _vehicleIdentityCandidateByPlayer =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly OmsiVehicleAssetResolver _vehicleAssetResolver;
    private readonly OmsiPhysicalRoadAnchorResolver _physicalRoadAnchorResolver;
    private readonly string _coordinatorId = Guid.NewGuid().ToString("N")[..8];
    private OmsiCompatibilityManifest? _localManifest;
    private VehicleTelemetry? _localTelemetry;
    private readonly object _spawnMaterializationSync = new();
    private string? _materializingPlayerId;
    private string _lastPublishedPhysicalSetSignature = string.Empty;

    public event Action<IReadOnlyList<string>>? PhysicalVehicleSetChanged;

    public RemotePhysicalVehicleCoordinator(
        Func<string?>? omsiInstallDirectorySource = null)
    {
        var installDirectorySource =
            omsiInstallDirectorySource ?? (() => null);
        _vehicleAssetResolver = new OmsiVehicleAssetResolver(
            installDirectorySource);
        _physicalRoadAnchorResolver =
            new OmsiPhysicalRoadAnchorResolver(
                installDirectorySource);
        NavBRAppLog.Info(
            "physical-vehicle-coordinator-created",
            $"coordinator={_coordinatorId}");
    }

    public bool IsPhysicalMultiplayerAvailable
    {
        get
        {
            if (Application.Current is not App app ||
                !app.PluginBridge.IsConnected)
            {
                return false;
            }

            return app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityVehicleSpawn) &&
                   app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityVehicleTransform) &&
                   app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25);
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

        var state = !IsPhysicalMultiplayerAvailable
            ? "plugin-unavailable"
            : "waiting-telemetry";
        return new RemotePhysicalVehicleStatus(
            state,
            ErrorCode: null,
            ErrorMessage: null,
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _spawnInFlight.TryRemove(playerId, out _);
            ReleaseSpawnMaterializationSlot(playerId);
        }
        catch (Exception ex)
        {
            _spawnInFlight.TryRemove(playerId, out _);
            ReleaseSpawnMaterializationSlot(playerId);
            _spawnRetryAfterByPlayer[playerId] =
                DateTimeOffset.UtcNow + SpawnFailureRetryDelay;
            SetStatus(
                playerId,
                "coordinator-error",
                ex.GetType().Name,
                ex.Message);
            NavBRAppLog.Error(
                $"physical-vehicle-apply-failed coordinator={_coordinatorId} player={playerId}",
                ex);
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
            var (pluginErrorCode, pluginErrorMessage) =
                DescribePluginAvailabilityFailure();
            SetStatus(
                playerId,
                "plugin-unavailable",
                pluginErrorCode,
                pluginErrorMessage);
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
            SetStatus(
                playerId,
                "identity-missing",
                "vehicle-compatibility-id-missing",
                string.IsNullOrWhiteSpace(remoteManifest.VehiclePath)
                    ? "Remote telemetry has neither a usable vehicle path nor the required SHA-256 vehicle compatibility id."
                    : $"Remote telemetry reported '{remoteManifest.VehiclePath}', but no SHA-256 vehicle compatibility id was available. The sender could not fingerprint the active OMSI vehicle definition.");
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
            var localStateDetail = localManifest is null
                ? "Local compatibility manifest is unavailable."
                : localTelemetry is null
                    ? "Local OMSI telemetry has not been received yet."
                    : !localTelemetry.IsInGame
                        ? "Local OMSI telemetry says the player is not in game."
                        : $"Local OMSI telemetry is stale by {(DateTimeOffset.UtcNow - localTelemetry.Timestamp).TotalSeconds:F1}s (limit {LocalTelemetryFreshness.TotalSeconds:F0}s).";
            SetStatus(
                playerId,
                "local-state-unavailable",
                "local-telemetry-unavailable",
                localStateDetail);
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

        var remoteIsOpenOmsi =
            IsOpenOmsiSource(remoteManifest);
        if (remoteIsOpenOmsi &&
            !HasCoherentPhysicalPose(frame.Telemetry) &&
            _physicalRoadAnchorResolver
                .TryResolveOpenOmsiWorldAnchor(
                    frame.Telemetry,
                    out var openOmsiWorldAnchor))
        {
            frame =
                ApplyOpenOmsiWorldAnchor(
                    frame,
                    openOmsiWorldAnchor);
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
        if (_spawned.ContainsKey(frame.Player.PlayerId))
        {
            if (!_spawnedCompatibilityByPlayer.TryGetValue(
                    frame.Player.PlayerId,
                    out var spawnedCompatibilityId) ||
                string.IsNullOrWhiteSpace(spawnedCompatibilityId))
            {
                // Recover ownership metadata rather than immediately tearing
                // down a bus that OMSI has already materialized. This can
                // happen around reconnect/status publication boundaries.
                _spawnedCompatibilityByPlayer[frame.Player.PlayerId] =
                    remoteVehicleCompatibilityId;
                _vehicleIdentityCandidateByPlayer.TryRemove(playerId, out _);
                NavBRAppLog.Info(
                    "physical-vehicle-identity-recovered",
                    $"player={playerId} compatibility={DescribeCompatibilityId(remoteVehicleCompatibilityId)}");
            }
            else if (!string.Equals(
                         spawnedCompatibilityId,
                         remoteVehicleCompatibilityId,
                         StringComparison.OrdinalIgnoreCase))
            {
                if (!IsVehicleIdentityChangeStable(
                        playerId,
                        spawnedCompatibilityId,
                        remoteVehicleCompatibilityId,
                        remoteManifest.VehiclePath))
                {
                    return;
                }

                // Only replace a live OMSI vehicle after the new identity has
                // remained stable across multiple frames. A single stale or
                // alternating manifest/telemetry frame must never cause a
                // despawn/spawn loop.
                SetStatus(
                    playerId,
                    "switching-vehicle",
                    "vehicle-identity-confirmed",
                    $"Vehicle identity stabilized: {DescribeCompatibilityId(spawnedCompatibilityId)} -> {DescribeCompatibilityId(remoteVehicleCompatibilityId)}.");
                await DespawnOwnedAsync(playerId, cancellationToken);
                if (_spawned.ContainsKey(frame.Player.PlayerId))
                {
                    // Despawn failed and restored the old ownership state. Never
                    // reuse that instance as if it represented the new vehicle.
                    return;
                }
            }
            else
            {
                _vehicleIdentityCandidateByPlayer.TryRemove(playerId, out _);
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

        var hasCoherentPhysicalPose =
            HasCoherentPhysicalPose(
                frame.Telemetry);

        if (!hasCoherentPhysicalPose &&
            TryRecoverExplicitPhysicalPose(
                frame.Telemetry,
                out var recoveredTelemetry))
        {
            frame = frame with
            {
                Telemetry = recoveredTelemetry
            };
            hasCoherentPhysicalPose = true;
        }

        if (!hasCoherentPhysicalPose)
        {
            // OMSI can switch RoadVehicle.Kachel in the middle of one
            // read-only telemetry poll while the simulation is running. The
            // provider withholds LocalX/Y/Z + quaternion for that transient
            // frame rather than pairing coordinates from different Kacheln.
            // For openOMSI senders, a world pose is converted above using the
            // receiver's real global.cfg TileSize and loaded map tile catalog.
            SetStatus(
                playerId,
                remoteIsOpenOmsi
                    ? "waiting-openomsi-world-pose"
                    : "waiting-coherent-pose",
                remoteIsOpenOmsi
                    ? "openomsi-world-pose-unresolved"
                    : "physical-pose-unstable",
                remoteIsOpenOmsi
                    ? "The openOMSI world pose could not be mapped to a real local OMSI Kachel. Verify map fingerprint, tile coverage and world coordinates."
                    : "Holding the last physical pose while OMSI completes a Kachel transition.");
            return;
        }

        var stableLocalX = frame.Telemetry.LocalX!.Value;
        var stableLocalZ = frame.Telemetry.LocalZ!.Value;

        var hasExplicitPhysicalGrid =
            frame.Telemetry.PhysicalGridX is int &&
            frame.Telemetry.PhysicalGridY is int;

        // Backward compatibility for an older server/shared payload that may
        // strip PhysicalGridX/Y: only trust legacy GridX/Y when the transmitted
        // TileX/TileY still prove they were derived from the same RoadVehicle
        // local pose. Navigation GridX/Y + unrelated TileX/TileY must never be
        // paired with LocalX/LocalZ for physical multiplayer.
        var hasCoherentLegacyPhysicalGrid =
            frame.Telemetry.PhysicalGridX is null &&
            frame.Telemetry.PhysicalGridY is null &&
            frame.Telemetry.GridX is int &&
            frame.Telemetry.GridY is int &&
            frame.Telemetry.TileX is double legacyTileX &&
            double.IsFinite(legacyTileX) &&
            frame.Telemetry.TileY is double legacyTileY &&
            double.IsFinite(legacyTileY) &&
            Math.Abs(legacyTileX - stableLocalX) <= 0.05d &&
            Math.Abs(legacyTileY - stableLocalZ) <= 0.05d;

        var hasStablePhysicalGrid =
            hasExplicitPhysicalGrid ||
            hasCoherentLegacyPhysicalGrid;
        var hasSimulatorLocalTile =
            playerId.StartsWith("sim-", StringComparison.OrdinalIgnoreCase) &&
            frame.Telemetry.MapTileIndex is int simulatorTileIndex &&
            simulatorTileIndex >= 0;

        if (!_spawned.ContainsKey(playerId) &&
            !hasStablePhysicalGrid &&
            !hasSimulatorLocalTile)
        {
            SetStatus(
                playerId,
                "tile-unavailable",
                "remote-grid-missing",
                "Remote telemetry did not include a Kachel-coherent OMSI GridX/GridY identity. PhysicalGridX/Y is preferred; legacy GridX/GridY is accepted only when TileX/TileY matches the same LocalX/LocalZ pose.");
            return;
        }

        if (!_spawned.ContainsKey(playerId))
        {
            if (_spawnRetryAfterByPlayer.TryGetValue(
                    playerId,
                    out var retryAfter))
            {
                if (retryAfter > DateTimeOffset.UtcNow)
                {
                    SetStatus(playerId, "spawn-retrying");
                    return;
                }

                _spawnRetryAfterByPlayer.TryRemove(playerId, out _);
            }

            SetStatus(playerId, "resolving-asset");
            var resolvedVehiclePath = await _vehicleAssetResolver.ResolveAsync(
                remoteManifest.VehiclePath,
                remoteVehicleCompatibilityId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(resolvedVehiclePath))
            {
                SetStatus(
                    playerId,
                    "asset-unresolved",
                    "vehicle-asset-unresolved",
                    $"No local Vehicles\\*.bus/ovh matched {DescribeCompatibilityId(remoteVehicleCompatibilityId)}. Reported sender path: {remoteManifest.VehiclePath ?? "-"}. The receiver must have the same vehicle definition content.");
                ReportFailureOnce(
                    playerId,
                    "asset",
                    $"physical-vehicle-asset-unresolved compatibility={DescribeCompatibilityId(remoteVehicleCompatibilityId)} reported-path={remoteManifest.VehiclePath ?? "-"}");
                return;
            }

            var consistInfo = await _vehicleAssetResolver.InspectConsistAsync(
                resolvedVehiclePath,
                cancellationToken);
            if (consistInfo is { ExpectedPartCount: > 1, IsComplete: false })
            {
                SetStatus(
                    playerId,
                    "consist-incomplete",
                    "multi-vehicle-consist-incomplete",
                    expectedPartCount: consistInfo.ExpectedPartCount);
                ReportFailureOnce(
                    playerId,
                    "consist",
                    $"multi-vehicle-consist-incomplete expected-parts={consistInfo.ExpectedPartCount}");
                return;
            }

            // openOMSI carries up to three rear sections in addition to the
            // main bus. Match that bounded shape for the first native OMSI
            // consist pass; larger trains remain fail-closed until their
            // ownership/lifecycle has been validated separately.
            if (consistInfo is { ExpectedPartCount: > 4 })
            {
                SetStatus(
                    playerId,
                    "consist-unsupported",
                    "multi-vehicle-consist-too-large",
                    expectedPartCount: consistInfo.ExpectedPartCount);
                ReportFailureOnce(
                    playerId,
                    "consist",
                    $"multi-vehicle-consist-too-large expected-parts={consistInfo.ExpectedPartCount}");
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

            var targetPhysicalFrame = BuildPhysicalFrame(
                frame,
                remoteManifest,
                resolvedVehiclePath);
            var spawnFrame = targetPhysicalFrame;
            var isSimulatorPlayer =
                playerId.StartsWith(
                    "sim-",
                    StringComparison.OrdinalIgnoreCase);
            var hasRoadTarget = false;
            if (isSimulatorPlayer &&
                _physicalRoadAnchorResolver.TryResolveRoadAnchor(
                    targetPhysicalFrame.Telemetry,
                    out var roadAnchor))
            {
                targetPhysicalFrame = ApplyPhysicalRoadAnchor(
                    targetPhysicalFrame,
                    roadAnchor,
                    alignHeadingToRoad: false);
                spawnFrame = targetPhysicalFrame;
                hasRoadTarget = true;
                NavBRAppLog.Info(
                    "physical-road-target",
                    $"player={playerId} source={roadAnchor.Source} anchor={roadAnchor.Name ?? "-"} grid={roadAnchor.GridX},{roadAnchor.GridY} local=({roadAnchor.LocalX:F2},{roadAnchor.LocalY:F2},{roadAnchor.LocalZ:F2}) distance={roadAnchor.DistanceMeters:F2}m");
            }

            if (isSimulatorPlayer && !hasRoadTarget)
            {
                SetStatus(
                    playerId,
                    "waiting-road-anchor",
                    "road-anchor-unavailable",
                    "The simulator convoy is waiting for a nearby real spline behind the host.");
                return;
            }

            // Match openOMSI's lifecycle model: once the desktop has
            // admitted the remote (compatibility, distance, local asset and
            // coherent physical pose), the continuous state stream owns native
            // creation and updates. Do not synchronously wait for MakeVehicle
            // over the pipe; OMSI's callback-thread lifecycle retries
            // materialization safely in the background of subsequent frames.
            _spawnedCompatibilityByPlayer[playerId] =
                remoteVehicleCompatibilityId;
            _resolvedVehiclePathByPlayer[playerId] =
                resolvedVehiclePath;
            _spawnPending[playerId] = 0;
            _spawned[playerId] = 0;

            SetStatus(
                playerId,
                "materializing",
                "state-stream-admitted",
                $"Physical state stream admitted. grid={spawnFrame.Telemetry.GridX?.ToString() ?? "-"},{spawnFrame.Telemetry.GridY?.ToString() ?? "-"} local=({spawnFrame.Telemetry.LocalX?.ToString("F2") ?? "-"},{spawnFrame.Telemetry.LocalY?.ToString("F2") ?? "-"},{spawnFrame.Telemetry.LocalZ?.ToString("F2") ?? "-"}) asset={resolvedVehiclePath}.",
                expectedPartCount: consistInfo?.ExpectedPartCount);

            NavBRAppLog.Info(
                "physical-state-stream-admitted",
                $"coordinator={_coordinatorId} player={playerId} " +
                $"grid={spawnFrame.Telemetry.GridX?.ToString() ?? "-"},{spawnFrame.Telemetry.GridY?.ToString() ?? "-"} " +
                $"local=({spawnFrame.Telemetry.LocalX?.ToString("F2") ?? "-"},{spawnFrame.Telemetry.LocalY?.ToString("F2") ?? "-"},{spawnFrame.Telemetry.LocalZ?.ToString("F2") ?? "-"}) " +
                $"asset={resolvedVehiclePath} compatibility={DescribeCompatibilityId(remoteVehicleCompatibilityId)}");

            await OmsiPluginBridgeRelay.ForwardAdmittedRemotePhysicalStateAsync(
                spawnFrame,
                cancellationToken);

            _spawnPending.TryRemove(playerId, out _);
            _spawnRetryAfterByPlayer.TryRemove(playerId, out _);
            _consecutiveUpdateFailuresByPlayer.TryRemove(playerId, out _);
            _lastFailureByPlayer.TryRemove(playerId, out _);
            _lastPhysicalUpdateAtByPlayer[playerId] = DateTimeOffset.UtcNow;

            PublishPhysicalVehicleSetIfChanged();
            RemoteDiagnosticsService.Record(
                "physical-vehicle",
                "info",
                "state-stream-admitted");
            return;
        }

        if (!_resolvedVehiclePathByPlayer.TryGetValue(
                frame.Player.PlayerId,
                out var localVehiclePath))
        {
            // Ownership can survive a reconnect/status transition while the
            // local path cache is rebuilt. Recover by the authoritative hash
            // instead of immediately destroying the OMSI instance.
            SetStatus(playerId, "recovering-path");
            localVehiclePath = await _vehicleAssetResolver.ResolveAsync(
                remoteManifest.VehiclePath,
                remoteVehicleCompatibilityId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(localVehiclePath))
            {
                _spawnRetryAfterByPlayer[playerId] =
                    DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(750);
                SetStatus(
                    playerId,
                    "path-recovery-pending",
                    "physical-vehicle-path-state-missing",
                    "The remote bus is owned but its local vehicle path is being recovered from the content fingerprint.");
                return;
            }

            _resolvedVehiclePathByPlayer[playerId] = localVehiclePath;
            _spawnedCompatibilityByPlayer[playerId] =
                remoteVehicleCompatibilityId;
            NavBRAppLog.Info(
                $"physical-vehicle-path-recovered player={playerId} path={localVehiclePath} compatibility={DescribeCompatibilityId(remoteVehicleCompatibilityId)}");
        }

        var updateInterval = ResolvePhysicalUpdateInterval(frame.Telemetry);
        if (_lastPhysicalUpdateAtByPlayer.TryGetValue(
                playerId,
                out var lastPhysicalUpdateAt) &&
            DateTimeOffset.UtcNow - lastPhysicalUpdateAt < updateInterval)
        {
            SetStatus(playerId, "active");
            return;
        }

        var physicalFrame = BuildPhysicalFrame(
            frame,
            remoteManifest,
            localVehiclePath);
        if (playerId.StartsWith(
                "sim-",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!_physicalRoadAnchorResolver.TryResolveRoadAnchor(
                    physicalFrame.Telemetry,
                    out var updateRoadAnchor))
            {
                SetStatus(
                    playerId,
                    "waiting-road-anchor",
                    "road-anchor-unavailable",
                    "Holding the last physical pose until a nearby spline is resolved.");
                return;
            }

            physicalFrame = ApplyPhysicalRoadAnchor(
                physicalFrame,
                updateRoadAnchor,
                alignHeadingToRoad: false);
        }

        // Keep feeding the locally resolved, Kachel-coherent state. The
        // plugin-side lifecycle owns spawn/materialization/retry and applies the
        // newest target on OMSI's callback thread, mirroring openOMSI's
        // continuous remote-state model rather than request/response updates.
        await OmsiPluginBridgeRelay.ForwardAdmittedRemotePhysicalStateAsync(
            physicalFrame,
            cancellationToken);

        _lastPhysicalUpdateAtByPlayer[playerId] = DateTimeOffset.UtcNow;
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
        var hadActiveOwnership = _spawned.TryRemove(playerId, out _);
        var hadPendingOwnership = _spawnPending.TryRemove(playerId, out _);
        if (!hadActiveOwnership && !hadPendingOwnership)
        {
            _spawnedCompatibilityByPlayer.TryRemove(playerId, out _);
            _resolvedVehiclePathByPlayer.TryRemove(playerId, out _);
            ReleaseSpawnMaterializationSlot(playerId);
            return;
        }

        _consecutiveUpdateFailuresByPlayer.TryRemove(playerId, out _);
        _lastPhysicalUpdateAtByPlayer.TryRemove(playerId, out _);
        _vehicleIdentityCandidateByPlayer.TryRemove(playerId, out _);
        _spawnedCompatibilityByPlayer.TryRemove(
            playerId,
            out var previousCompatibilityId);
        _resolvedVehiclePathByPlayer.TryRemove(
            playerId,
            out var previousVehiclePath);

        var result = await OmsiPluginBridgeRelay.DespawnRemoteVehicleAsync(
            playerId,
            cancellationToken);
        if (result?.Success != true)
        {
            // A missing Plugin Bridge reply is not proof that the OMSI-owned
            // vehicle disappeared. Preserve NavBR ownership metadata so a
            // later frame/cleanup can retry instead of orphaning the bus.
            if (hadActiveOwnership)
            {
                _spawned.TryAdd(playerId, 0);
            }
            if (hadPendingOwnership)
            {
                _spawnPending.TryAdd(playerId, 0);
            }
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

        ReleaseSpawnMaterializationSlot(playerId);
        _lastFailureByPlayer.TryRemove(playerId, out _);
        PublishPhysicalVehicleSetIfChanged();
        RemoteDiagnosticsService.Record(
            "physical-vehicle",
            "info",
            "despawn-success");
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var players = _spawned.Keys
            .Concat(_spawnPending.Keys)
            .Concat(_playerGates.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var playerId in players)
        {
            await DespawnAsync(playerId, cancellationToken);
        }

        _spawnInFlight.Clear();
        _spawnPending.Clear();
        lock (_spawnMaterializationSync)
        {
            _materializingPlayerId = null;
        }
        _spawnedCompatibilityByPlayer.Clear();
        _vehicleIdentityCandidateByPlayer.Clear();
        _resolvedVehiclePathByPlayer.Clear();
        _consecutiveUpdateFailuresByPlayer.Clear();
        _distanceByPlayer.Clear();
        _capacityEvictionsInFlight.Clear();
        _capacitySuppressedUntilByPlayer.Clear();
        _spawnRetryAfterByPlayer.Clear();
        _lastPhysicalUpdateAtByPlayer.Clear();
        _statusByPlayer.Clear();
        PublishPhysicalVehicleSetIfChanged(force: true);
    }

    private bool IsVehicleIdentityChangeStable(
        string playerId,
        string currentCompatibilityId,
        string nextCompatibilityId,
        string? nextVehiclePath)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = _vehicleIdentityCandidateByPlayer.AddOrUpdate(
            playerId,
            _ => new VehicleIdentityCandidate(
                nextCompatibilityId,
                nextVehiclePath,
                now,
                1),
            (_, previous) =>
                string.Equals(
                    previous.CompatibilityId,
                    nextCompatibilityId,
                    StringComparison.OrdinalIgnoreCase)
                    ? previous with
                    {
                        VehiclePath = nextVehiclePath ?? previous.VehiclePath,
                        Observations = Math.Min(
                            previous.Observations + 1,
                            VehicleIdentityChangeMinObservations + 8)
                    }
                    : new VehicleIdentityCandidate(
                        nextCompatibilityId,
                        nextVehiclePath,
                        now,
                        1));

        var age = now - candidate.FirstSeenUtc;
        if (candidate.Observations < VehicleIdentityChangeMinObservations ||
            age < VehicleIdentityChangeDebounce)
        {
            SetStatus(
                playerId,
                "vehicle-change-pending",
                "vehicle-identity-unstable",
                $"Ignoring transient vehicle identity change {DescribeCompatibilityId(currentCompatibilityId)} -> {DescribeCompatibilityId(nextCompatibilityId)}; observations={candidate.Observations}, age={age.TotalMilliseconds:F0}ms.");
            return false;
        }

        _vehicleIdentityCandidateByPlayer.TryRemove(playerId, out _);
        NavBRAppLog.Info(
            "physical-vehicle-identity-switch-confirmed",
            $"player={playerId} previous={DescribeCompatibilityId(currentCompatibilityId)} next={DescribeCompatibilityId(nextCompatibilityId)} path={nextVehiclePath ?? "-"} observations={candidate.Observations} ageMs={age.TotalMilliseconds:F0}");
        return true;
    }

    private static string DescribeCompatibilityId(string? compatibilityId)
    {
        var value = compatibilityId?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Length <= 24
            ? value
            : value[..24] + "…";
    }

    private bool TryAcquireSpawnMaterializationSlot(string playerId)
    {
        lock (_spawnMaterializationSync)
        {
            if (string.IsNullOrWhiteSpace(_materializingPlayerId))
            {
                _materializingPlayerId = playerId;
                return true;
            }

            return string.Equals(
                _materializingPlayerId,
                playerId,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ReleaseSpawnMaterializationSlot(string playerId)
    {
        lock (_spawnMaterializationSync)
        {
            if (string.Equals(
                    _materializingPlayerId,
                    playerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _materializingPlayerId = null;
            }
        }
    }

    private void PublishPhysicalVehicleSetIfChanged(bool force = false)
    {
        var playerIds = _spawned.Keys
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var signature = string.Join("\n", playerIds);

        if (!force &&
            string.Equals(
                signature,
                _lastPublishedPhysicalSetSignature,
                StringComparison.Ordinal))
        {
            return;
        }

        _lastPublishedPhysicalSetSignature = signature;
        PhysicalVehicleSetChanged?.Invoke(playerIds);
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

    private static TimeSpan ResolvePhysicalUpdateInterval(
        VehicleTelemetry telemetry)
    {
        // Keep physical update cadence tied to whether the remote vehicle is
        // actually moving, not to camera distance. A bus at the edge of the
        // physical spawn radius still needs enough sender samples to cross
        // Kacheln and articulate smoothly. This mirrors openOMSI's 20 Hz
        // active / 5 Hz idle transport behavior while the existing network and
        // plugin pressure governors remain free to back off upstream.
        var active =
            Math.Abs(telemetry.SpeedKph) > 0.35d ||
            HasMeaningfulVelocity(telemetry);

        return TimeSpan.FromMilliseconds(active ? 50d : 200d);
    }

    private static bool HasMeaningfulVelocity(VehicleTelemetry telemetry)
    {
        if (telemetry.VelocityX is not double vx ||
            telemetry.VelocityY is not double vy ||
            telemetry.VelocityZ is not double vz ||
            !double.IsFinite(vx) ||
            !double.IsFinite(vy) ||
            !double.IsFinite(vz))
        {
            return false;
        }

        return vx * vx + vy * vy + vz * vz > 0.01d;
    }

    private static bool IsTileAvailabilityError(string? errorCode) =>
        !string.IsNullOrWhiteSpace(errorCode) &&
        (string.Equals(
             errorCode,
             "tile-unavailable",
             StringComparison.Ordinal) ||
         string.Equals(
             errorCode,
             "tile-grid-unavailable",
             StringComparison.Ordinal) ||
         string.Equals(
             errorCode,
             "tile-grid-missing",
             StringComparison.Ordinal));

    private static bool IsFatalUpdateFailure(string? errorCode) =>
        string.Equals(errorCode, "vehicle-not-owned", StringComparison.Ordinal) ||
        string.Equals(errorCode, "vehicle-pointer-stale", StringComparison.Ordinal) ||
        string.Equals(errorCode, "invalid-pose", StringComparison.Ordinal) ||
        string.Equals(errorCode, "invalid-instance-id", StringComparison.Ordinal) ||
        string.Equals(errorCode, "backend-unavailable", StringComparison.Ordinal) ||
        string.Equals(errorCode, "writes-disabled", StringComparison.Ordinal) ||
        string.Equals(errorCode, "motion-transform-write-failed", StringComparison.Ordinal);

    private static bool IsRecoverableMotionReadbackFailure(string? errorCode) =>
        PluginBridgeProtocol.IsRecoverablePhysicalMotionError(errorCode);

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
            : IsTileAvailabilityError(errorCode)
                ? "tile-unavailable"
                : $"{operation}-failed";
        SetStatus(
            playerId,
            state,
            errorCode,
            result?.ErrorMessage,
            result?.RemoteVehicleCount,
            expectedPartCount);
        ReportFailureOnce(
            playerId,
            operation,
            $"{operation}-failed error={errorCode} parts={result?.RemoteVehicleCount?.ToString() ?? "n/a"} expected-parts={expectedPartCount?.ToString() ?? "n/a"} detail={detail}");
    }

    private static (string ErrorCode, string ErrorMessage)
        DescribePluginAvailabilityFailure()
    {
        if (Application.Current is not App app)
        {
            return (
                "plugin-app-context-unavailable",
                "The desktop OMSI plugin bridge is not available in the current application context.");
        }

        var info = app.PluginBridge.GetConnectionInfo();
        if (!info.IsConnected)
        {
            return (
                "plugin-disconnected",
                "The OMSI x86 plugin is not connected. If NavBR was updated while OMSI was open, close OMSI, restart NavBR so the plugin DLL can be replaced, then start OMSI again.");
        }

        var missing = new List<string>(3);
        if (!app.PluginBridge.SupportsCapability(
                PluginBridgeProtocol.CapabilityVehicleSpawn))
        {
            missing.Add(PluginBridgeProtocol.CapabilityVehicleSpawn);
        }

        if (!app.PluginBridge.SupportsCapability(
                PluginBridgeProtocol.CapabilityVehicleTransform))
        {
            missing.Add(PluginBridgeProtocol.CapabilityVehicleTransform);
        }

        if (!app.PluginBridge.SupportsCapability(
                PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25))
        {
            missing.Add(PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25);
        }

        if (missing.Count > 0)
        {
            return (
                "plugin-capability-missing",
                $"Connected OMSI plugin is missing required capability/capabilities: {string.Join(", ", missing)}. This build requires the state-interop-25 physical multiplayer path. Close OMSI completely, restart/install NavBR so the plugin DLL can be replaced, then start OMSI again.");
        }

        return (
            "plugin-unavailable",
            "The OMSI plugin bridge is connected but physical multiplayer is not currently available.");
    }

    private void SetStatus(
        string playerId,
        string state,
        string? errorCode = null,
        string? errorMessage = null,
        int? partCount = null,
        int? expectedPartCount = null)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var normalizedErrorCode =
            string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        var normalizedErrorMessage =
            string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        var normalizedPartCount = partCount is > 0 ? partCount : null;
        var normalizedExpectedPartCount =
            expectedPartCount is > 0 ? expectedPartCount : null;

        var next = new RemotePhysicalVehicleStatus(
            state,
            normalizedErrorCode,
            normalizedErrorMessage,
            normalizedPartCount,
            normalizedExpectedPartCount,
            DateTimeOffset.UtcNow);

        var changed =
            !_statusByPlayer.TryGetValue(playerId, out var previous) ||
            !string.Equals(previous.State, next.State, StringComparison.Ordinal) ||
            !string.Equals(previous.ErrorCode, next.ErrorCode, StringComparison.Ordinal) ||
            !string.Equals(previous.ErrorMessage, next.ErrorMessage, StringComparison.Ordinal) ||
            previous.PartCount != next.PartCount ||
            previous.ExpectedPartCount != next.ExpectedPartCount;

        _statusByPlayer[playerId] = next;
        if (changed)
        {
            NavBRAppLog.Info(
                $"physical-vehicle coordinator={_coordinatorId} player={playerId} state={next.State} " +
                $"error={next.ErrorCode ?? "-"} parts={next.PartCount?.ToString() ?? "-"} " +
                $"expected-parts={next.ExpectedPartCount?.ToString() ?? "-"} " +
                $"detail={next.ErrorMessage ?? "-"}");
        }
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
        string resolvedVehiclePath)
    {
        var hasPhysicalGrid =
            frame.Telemetry.PhysicalGridX is int &&
            frame.Telemetry.PhysicalGridY is int;
        var physicalGridX = hasPhysicalGrid
            ? frame.Telemetry.PhysicalGridX
            : frame.Telemetry.GridX;
        var physicalGridY = hasPhysicalGrid
            ? frame.Telemetry.PhysicalGridY
            : frame.Telemetry.GridY;

        return frame with
        {
            Telemetry = frame.Telemetry with
            {
                // Prefer the RoadVehicle.Kachel-coherent physical grid. The
                // public Render deployment can temporarily run an older
                // NavBR.Shared that strips PhysicalGridX/Y; in that case keep
                // backward compatibility by forwarding legacy GridX/Y. The
                // plugin still resolves that pair to a loaded local Kachel
                // before any write, so an invalid legacy grid fails closed.
                GridX = physicalGridX,
                GridY = physicalGridY,
                MapTileIndex =
                    frame.Player.PlayerId.StartsWith("sim-", StringComparison.OrdinalIgnoreCase)
                        ? frame.Telemetry.MapTileIndex
                        : null,
                VehiclePath = resolvedVehiclePath,
                VehicleCompatibilityId = remoteManifest.VehicleCompatibilityId,
                HofName = remoteManifest.HofName,
                HofCompatibilityId = remoteManifest.HofCompatibilityId
            }
        };
    }

    private static bool IsOpenOmsiSource(
        OmsiCompatibilityManifest manifest) =>
        string.Equals(
            manifest.PluginDeployment,
            "OPENOMSI-X64",
            StringComparison.OrdinalIgnoreCase) ||
        manifest.Capabilities?.Contains(
            PluginBridgeProtocol
                .CapabilityOpenOmsiStandardPlugin,
            StringComparer.OrdinalIgnoreCase) == true;

    private static bool HasCoherentPhysicalPose(
        VehicleTelemetry telemetry) =>
        telemetry.LocalX is double localX &&
        double.IsFinite(localX) &&
        telemetry.LocalY is double localY &&
        double.IsFinite(localY) &&
        telemetry.LocalZ is double localZ &&
        double.IsFinite(localZ) &&
        telemetry.RotationX is double rotationX &&
        double.IsFinite(rotationX) &&
        telemetry.RotationY is double rotationY &&
        double.IsFinite(rotationY) &&
        telemetry.RotationZ is double rotationZ &&
        double.IsFinite(rotationZ) &&
        telemetry.RotationW is double rotationW &&
        double.IsFinite(rotationW);

    private static bool TryRecoverExplicitPhysicalPose(
        VehicleTelemetry telemetry,
        out VehicleTelemetry recovered)
    {
        recovered = telemetry;

        // PhysicalGridX/Y is only published by the OMSI reader after it has
        // verified RoadVehicle.Kachel and Position against the same tile
        // pointer. Therefore this is safe to use as a recovery source when
        // nullable local/quaternion fields were lost in transport.
        if (telemetry.PhysicalGridX is not int physicalGridX ||
            telemetry.PhysicalGridY is not int physicalGridY ||
            telemetry.TileX is not double tileX ||
            !double.IsFinite(tileX) ||
            telemetry.TileY is not double tileY ||
            !double.IsFinite(tileY) ||
            Math.Abs(tileX) > 1_200d ||
            Math.Abs(tileY) > 1_200d)
        {
            return false;
        }

        // PublishTelemetryAsync can supplement PhysicalGridX/Y from the
        // in-process plugin when the external reader catches OMSI exactly
        // between two Kacheln. In that case TileX/TileY may still come from
        // NavigationVehicle. They are safe recovery coordinates only when the
        // navigation grid is complete and agrees with the physical grid (or
        // when no navigation grid was supplied at all).
        if (telemetry.GridX.HasValue != telemetry.GridY.HasValue)
        {
            return false;
        }

        if (telemetry.GridX is int navigationGridX &&
            telemetry.GridY is int navigationGridY &&
            (navigationGridX != physicalGridX ||
             navigationGridY != physicalGridY))
        {
            return false;
        }

        var localX =
            telemetry.LocalX is double existingLocalX &&
            double.IsFinite(existingLocalX)
                ? existingLocalX
                : tileX;
        var localZ =
            telemetry.LocalZ is double existingLocalZ &&
            double.IsFinite(existingLocalZ)
                ? existingLocalZ
                : tileY;
        var localY =
            telemetry.LocalY is double existingLocalY &&
            double.IsFinite(existingLocalY)
                ? existingLocalY
                : double.IsFinite(telemetry.Y)
                    ? telemetry.Y
                    : double.NaN;

        if (!double.IsFinite(localY))
        {
            return false;
        }

        double rotationX;
        double rotationY;
        double rotationZ;
        double rotationW;

        var existingRotationX =
            telemetry.RotationX ?? double.NaN;
        var existingRotationY =
            telemetry.RotationY ?? double.NaN;
        var existingRotationZ =
            telemetry.RotationZ ?? double.NaN;
        var existingRotationW =
            telemetry.RotationW ?? double.NaN;
        var hasQuaternion =
            double.IsFinite(existingRotationX) &&
            double.IsFinite(existingRotationY) &&
            double.IsFinite(existingRotationZ) &&
            double.IsFinite(existingRotationW);

        if (hasQuaternion)
        {
            rotationX = existingRotationX;
            rotationY = existingRotationY;
            rotationZ = existingRotationZ;
            rotationW = existingRotationW;
        }
        else
        {
            if (!double.IsFinite(telemetry.HeadingDegrees))
            {
                return false;
            }

            var headingRadians =
                telemetry.HeadingDegrees *
                (Math.PI / 180d);
            var halfHeading = headingRadians * 0.5d;

            // OMSI/D3D uses Y-up; a heading is a pure yaw around Y.
            rotationX = 0d;
            rotationY = Math.Sin(halfHeading);
            rotationZ = 0d;
            rotationW = Math.Cos(halfHeading);
        }

        recovered = telemetry with
        {
            LocalX = localX,
            LocalY = localY,
            LocalZ = localZ,
            RotationX = rotationX,
            RotationY = rotationY,
            RotationZ = rotationZ,
            RotationW = rotationW
        };

        return HasCoherentPhysicalPose(recovered);
    }

    private PlayerTelemetryFrame ApplyOpenOmsiWorldAnchor(
        PlayerTelemetryFrame frame,
        OmsiPhysicalRoadAnchor anchor)
    {
        var telemetry = frame.Telemetry;
        VehicleSectionPose[]? rearSections = null;

        if (telemetry.RearSections is { Length: > 0 } incomingRear)
        {
            var converted = new List<VehicleSectionPose>(
                Math.Min(
                    incomingRear.Length,
                    OpenOmsiLanProtocol.MaxRearSections));

            foreach (var section in incomingRear.Take(
                         OpenOmsiLanProtocol.MaxRearSections))
            {
                // Incoming openOMSI rear poses are staged in world metres:
                // LocalX=world X, LocalZ=world ground Y, LocalY=world vertical Z.
                var heading = QuaternionHeadingDegrees(
                    section.RotationX,
                    section.RotationY,
                    section.RotationZ,
                    section.RotationW);
                var sectionTelemetry = telemetry with
                {
                    X = section.LocalX,
                    Y = section.LocalZ,
                    Z = section.LocalY,
                    HeadingDegrees = heading,
                    RearSections = null
                };

                if (!_physicalRoadAnchorResolver.TryResolveOpenOmsiWorldAnchor(
                        sectionTelemetry,
                        out var sectionAnchor))
                {
                    continue;
                }

                converted.Add(
                    new VehicleSectionPose(
                        sectionAnchor.LocalX,
                        sectionAnchor.LocalY,
                        sectionAnchor.LocalZ,
                        sectionAnchor.RotationX,
                        sectionAnchor.RotationY,
                        sectionAnchor.RotationZ,
                        sectionAnchor.RotationW,
                        sectionAnchor.GridX,
                        sectionAnchor.GridY,
                        MapTileIndex: null));
            }

            if (converted.Count > 0)
            {
                rearSections = converted.ToArray();
            }
        }

        return frame with
        {
            Telemetry = telemetry with
            {
                // Convert openOMSI's Z-up world convention into OMSI/D3D's
                // Y-up convention for every downstream OMSI 2 calculation.
                X = telemetry.X,
                Y = telemetry.Z,
                Z = telemetry.Y,
                GridX = anchor.GridX,
                GridY = anchor.GridY,
                PhysicalGridX = anchor.GridX,
                PhysicalGridY = anchor.GridY,
                TileX = anchor.LocalX,
                TileY = anchor.LocalZ,
                LocalX = anchor.LocalX,
                LocalY = anchor.LocalY,
                LocalZ = anchor.LocalZ,
                MapTileIndex = null,
                HeadingDegrees =
                    anchor.HeadingDegrees,
                RotationX =
                    anchor.RotationX,
                RotationY =
                    anchor.RotationY,
                RotationZ =
                    anchor.RotationZ,
                RotationW =
                    anchor.RotationW,
                VelocityX =
                    telemetry.VelocityX,
                VelocityY =
                    telemetry.VelocityZ,
                VelocityZ =
                    telemetry.VelocityY,
                RearSections = rearSections
            }
        };
    }

    private static double QuaternionHeadingDegrees(
        double x,
        double y,
        double z,
        double w)
    {
        var lengthSquared = x * x + y * y + z * z + w * w;
        if (!double.IsFinite(lengthSquared) || lengthSquared < 0.00000001d)
        {
            return 0d;
        }

        var inverse = 1d / Math.Sqrt(lengthSquared);
        x *= inverse;
        y *= inverse;
        z *= inverse;
        w *= inverse;

        var sinYaw = 2d * (w * y + x * z);
        var cosYaw = 1d - 2d * (y * y + z * z);
        var degrees = Math.Atan2(sinYaw, cosYaw) * (180d / Math.PI);
        return (degrees + 360d) % 360d;
    }

    private static PlayerTelemetryFrame ApplyPhysicalRoadAnchor(
        PlayerTelemetryFrame frame,
        OmsiPhysicalRoadAnchor anchor,
        bool alignHeadingToRoad)
    {
        var telemetry = frame.Telemetry;
        var sourceGridX =
            telemetry.PhysicalGridX ?? telemetry.GridX;
        var sourceGridY =
            telemetry.PhysicalGridY ?? telemetry.GridY;
        var keepMapTileIndex =
            sourceGridX == anchor.GridX &&
            sourceGridY == anchor.GridY;

        return frame with
        {
            Telemetry = telemetry with
            {
                GridX = anchor.GridX,
                GridY = anchor.GridY,
                PhysicalGridX = anchor.GridX,
                PhysicalGridY = anchor.GridY,
                TileX = anchor.LocalX,
                TileY = anchor.LocalZ,
                LocalX = anchor.LocalX,
                LocalY = anchor.LocalY,
                LocalZ = anchor.LocalZ,
                MapTileIndex =
                    keepMapTileIndex
                        ? telemetry.MapTileIndex
                        : null,
                HeadingDegrees =
                    alignHeadingToRoad
                        ? anchor.HeadingDegrees
                        : telemetry.HeadingDegrees,
                RotationX =
                    alignHeadingToRoad
                        ? anchor.RotationX
                        : telemetry.RotationX,
                RotationY =
                    alignHeadingToRoad
                        ? anchor.RotationY
                        : telemetry.RotationY,
                RotationZ =
                    alignHeadingToRoad
                        ? anchor.RotationZ
                        : telemetry.RotationZ,
                RotationW =
                    alignHeadingToRoad
                        ? anchor.RotationW
                        : telemetry.RotationW
            }
        };
    }

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

    private sealed record VehicleIdentityCandidate(
        string CompatibilityId,
        string? VehiclePath,
        DateTimeOffset FirstSeenUtc,
        int Observations);

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
