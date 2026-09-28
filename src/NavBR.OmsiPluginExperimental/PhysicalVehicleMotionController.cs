using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Smooths NavBR-owned remote buses between multiplayer telemetry frames.
/// All OMSI writes remain on the OMSI callback thread.
/// </summary>
internal static class PhysicalVehicleMotionController
{
    private const double MinimumInterpolationMs = 70d;
    private const double MaximumInterpolationMs = 320d;
    private const double DefaultInterpolationMs = 120d;
    private const double InterpolationPeriodScale = 1.05d;
    private const double TeleportDistanceMeters = 30d;
    private const long TeleportGapMs = 1_500;
    private const long StaleTargetAfterMs = 5_000;
    private const long ReadbackIntervalMs = 1_000;
    private const double ReadbackToleranceMeters = 3d;
    private const int ExternalControlSuccessBit = 1 << 0;
    private const int ExternalControlWasCalculatedResetBit = 1 << 1;
    private const int ExternalControlPreCalcRequestedBit = 1 << 2;
    private const int ExternalControlLoadedTileResetBit = 1 << 3;
    private const int ExternalControlPhysicsBodyReenabledBit = 1 << 4;
    private const int ExternalControlPhysicsSyncUnavailableBit = 1 << 5;
    private const int ExternalControlConflictMask =
        ExternalControlWasCalculatedResetBit |
        ExternalControlPreCalcRequestedBit |
        ExternalControlLoadedTileResetBit |
        ExternalControlPhysicsBodyReenabledBit |
        ExternalControlPhysicsSyncUnavailableBit;
    private const long ExternalControlConflictLogIntervalMs = 2_000;
    private const long PathBindingComparisonLogIntervalMs = 10_000;
    private const long PhysicsBodyComparisonLogIntervalMs = 10_000;
    private const long MaximumSettledPoseReassertIntervalMs = 250;
    private const double FramePoseDriftToleranceMeters = 0.05d;

    private static readonly Dictionary<string, MotionState> States =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> RemovalScratch = new();
    private static long _lastTickMs;
    private static long _lastPathBindingComparisonLogTickMs;
    private static long _lastExternalControlTickMs;

    public static int ActiveCount => States.Count;

    public static bool TryInitialize(
        PhysicalVehicleInstance instance,
        PluginBridgeMessage command,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (!TryReadSnapshot(command, out var snapshot))
        {
            errorCode = "invalid-pose";
            errorMessage = "Physical vehicle updates require finite local position and quaternion values.";
            return false;
        }

        if (!TryApplyVisualState(instance, command))
        {
            errorCode = "visual-state-write-failed";
            errorMessage = "OMSI rejected the guarded vehicle visual-state write.";
            return false;
        }

        if (!TryApplyTransform(instance, snapshot, writeTileIndex: true))
        {
            errorCode = "transform-write-failed";
            errorMessage =
                $"OMSI rejected the guarded vehicle transform write at native stage {OmsiNativeInterop.GetLastVehicleTransformFailureStage()}.";
            return false;
        }

        if (!TryConfirmTransform(
                instance,
                snapshot,
                validateTileIndex: true,
                out errorCode,
                out errorMessage))
        {
            return false;
        }

        var now = Environment.TickCount64;
        States[instance.InstanceId] = new MotionState
        {
            Current = snapshot,
            Start = snapshot,
            Target = snapshot,
            StartTickMs = now,
            LastTargetTickMs = now,
            DurationMs = DefaultInterpolationMs,
            LastSourceTimestampMs = command.TimestampUnixMilliseconds,
            LastReadbackTickMs = now
        };
        return true;
    }

    public static bool TrySetTarget(
        PhysicalVehicleInstance instance,
        PluginBridgeMessage command,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (!TryReadSnapshot(command, out var target))
        {
            errorCode = "invalid-pose";
            errorMessage = "Physical vehicle updates require finite local position and quaternion values.";
            return false;
        }

        if (!States.TryGetValue(instance.InstanceId, out var state))
        {
            return TryInitialize(instance, command, out errorCode, out errorMessage);
        }

        if (!string.IsNullOrWhiteSpace(state.FaultCode))
        {
            errorCode = state.FaultCode;
            errorMessage = state.FaultMessage ??
                "OMSI did not confirm the previous physical vehicle motion write.";
            States.Remove(instance.InstanceId);
            return false;
        }

        var sourceTimestamp = command.TimestampUnixMilliseconds;
        if (sourceTimestamp is long sourceMs &&
            state.LastSourceTimestampMs is long previousSourceMs &&
            sourceMs <= previousSourceMs)
        {
            // Reconnects/network jitter can surface an older telemetry sample.
            // Never rewind a physical bus to an older state.
            return true;
        }

        if (!TryApplyVisualState(instance, command))
        {
            errorCode = "visual-state-write-failed";
            errorMessage = "OMSI rejected the guarded vehicle visual-state write.";
            return false;
        }

        if (target.MapTileIndex is null &&
            state.Target.MapTileIndex is int previousTileIndex)
        {
            target = target with { MapTileIndex = previousTileIndex };
        }

        var tileChanged =
            target.MapTileIndex is int targetTileIndex &&
            state.Target.MapTileIndex != targetTileIndex;

        var now = Environment.TickCount64;
        var gapMs = Math.Max(0L, now - state.LastTargetTickMs);
        var sourceGapMs =
            sourceTimestamp is long sourceValue &&
            state.LastSourceTimestampMs is long previousSourceValue &&
            sourceValue > previousSourceValue
                ? sourceValue - previousSourceValue
                : 0L;
        var cadenceMs = sourceGapMs is >= 20 and <= 1_000
            ? sourceGapMs
            : gapMs;
        var distance = Distance(state.Current, target);

        if (tileChanged ||
            gapMs >= TeleportGapMs ||
            distance >= TeleportDistanceMeters)
        {
            // Tile transitions and map teleports/repositions should snap.
            // Interpolating Position across two Kachel coordinate frames would
            // drag the bus through an invalid local reference.
            if (!TryApplyTransform(
                    instance,
                    target,
                    writeTileIndex: tileChanged))
            {
                errorCode = "transform-write-failed";
                errorMessage =
                    $"OMSI rejected the guarded vehicle transform write at native stage {OmsiNativeInterop.GetLastVehicleTransformFailureStage()}.";
                return false;
            }

            if (!TryConfirmTransform(
                    instance,
                    target,
                    validateTileIndex: tileChanged,
                    out errorCode,
                    out errorMessage))
            {
                return false;
            }

            state.Current = target;
            state.Start = target;
            state.Target = target;
            state.StartTickMs = now;
            state.LastTargetTickMs = now;
            state.DurationMs = DefaultInterpolationMs;
            state.LastSourceTimestampMs = sourceTimestamp;
            state.LastReadbackTickMs = now;
            return true;
        }

        state.Start = state.Current;
        state.Target = target;
        state.StartTickMs = now;
        state.DurationMs = cadenceMs <= 0
            ? DefaultInterpolationMs
            : Math.Clamp(
                cadenceMs * InterpolationPeriodScale,
                MinimumInterpolationMs,
                MaximumInterpolationMs);
        state.LastTargetTickMs = now;
        state.LastSourceTimestampMs = sourceTimestamp;
        return true;
    }

    /// <summary>
    /// Reasserts NavBR ownership once per OMSI callback cycle, independently
    /// from interpolation cadence. This must run on OMSI's callback thread.
    /// </summary>
    public static void MaintainExternalControl()
    {
        var now = Environment.TickCount64;
        var activeCount = States.Count;
        if (activeCount == 0)
        {
            _lastExternalControlTickMs = now;
            return;
        }

        // System variable 0 can be requested more often than the visible frame
        // cadence on some OMSI setups. Bound ownership maintenance so native
        // ODE/transform work cannot monopolize OMSI's callback thread.
        var minimumMaintainIntervalMs = activeCount switch
        {
            >= 9 => 33L,
            >= 5 => 24L,
            _ => 16L
        };
        if (_lastExternalControlTickMs > 0 &&
            now - _lastExternalControlTickMs < minimumMaintainIntervalMs)
        {
            return;
        }

        _lastExternalControlTickMs = now;
        RemovalScratch.Clear();
        foreach (var pair in States)
        {
            var instanceId = pair.Key;
            var state = pair.Value;
            if (!PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance) ||
                OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) != 1)
            {
                RemovalScratch.Add(instanceId);
                continue;
            }

            if (!PhysicalVehicleBackend.IsSafeOwnedPointer(
                    instance,
                    out var unsafeReason))
            {
                state.FaultCode = "motion-external-control-failed";
                state.FaultMessage =
                    $"NavBR stopped the RoadVehicle external-control keepalive: {unsafeReason}.";
                continue;
            }

            var externalControlResult =
                OmsiNativeInterop.MaintainVehicleExternalControl(
                    instance.VehiclePointer);
            if ((externalControlResult & ExternalControlSuccessBit) == 0)
            {
                state.FaultCode = "motion-external-control-failed";
                state.FaultMessage =
                    "OMSI rejected the NavBR RoadVehicle external-control keepalive.";
                continue;
            }

            var conflictBits =
                externalControlResult & ExternalControlConflictMask;

            var hasObjectPosition =
                OmsiNativeInterop.ReadRoadVehiclePosition(
                    instance.VehiclePointer,
                    out var objectX,
                    out var objectY,
                    out var objectZ) == 1;
            var poseDrifted = false;
            if (hasObjectPosition)
            {
                var dx = objectX - state.Current.X;
                var dy = objectY - state.Current.Y;
                var dz = objectZ - state.Current.Z;
                poseDrifted =
                    dx * dx + dy * dy + dz * dz >
                    FramePoseDriftToleranceMeters *
                    FramePoseDriftToleranceMeters;
            }

            var physicsReenabled =
                (conflictBits & ExternalControlPhysicsBodyReenabledBit) != 0;
            var periodicRefresh =
                now - state.LastFramePoseReassertTickMs >=
                MaximumSettledPoseReassertIntervalMs;

            // Reapply the expensive full transform only when OMSI actually
            // moved the object/body or as a low-rate render-matrix refresh.
            // This keeps the correction from build #1074 without doing several
            // ODE + matrix writes for every callback and every remote bus.
            if ((poseDrifted || physicsReenabled || periodicRefresh) &&
                !TryApplyTransform(
                    instance,
                    state.Current,
                    writeTileIndex: false))
            {
                state.FaultCode = "motion-frame-pose-reassert-failed";
                state.FaultMessage =
                    $"OMSI rejected the bounded NavBR pose reassertion at native stage {OmsiNativeInterop.GetLastVehicleTransformFailureStage()}.";
                continue;
            }

            if (poseDrifted || physicsReenabled || periodicRefresh)
            {
                state.LastFramePoseReassertTickMs = now;
            }

            if (now - state.LastPhysicsBodyLogTickMs >=
                    PhysicsBodyComparisonLogIntervalMs &&
                hasObjectPosition &&
                OmsiNativeInterop.TryReadRoadVehiclePhysicsBodyPosition(
                    instance.VehiclePointer,
                    out var bodyX,
                    out var bodyY,
                    out var bodyZ,
                    out var bodyEnabled))
            {
                state.LastPhysicsBodyLogTickMs = now;
                PluginLogWriter.Enqueue(
                    $"physical-body-compare id={instanceId} pointer=0x{instance.VehiclePointer:X8} " +
                    $"object=({objectX:F2},{objectY:F2},{objectZ:F2}) " +
                    $"body=({bodyX:F2},{bodyY:F2},{bodyZ:F2}) " +
                    $"bodyEnabled={(bodyEnabled ? 1 : 0)} " +
                    $"delta=({bodyX - objectX:F2},{bodyY - objectY:F2},{bodyZ - objectZ:F2})");
            }

            if (conflictBits != 0 &&
                now - state.LastExternalControlConflictLogTickMs >=
                    ExternalControlConflictLogIntervalMs)
            {
                state.LastExternalControlConflictLogTickMs = now;
                PluginLogWriter.Enqueue(
                    $"physical-frame-ownership id={instanceId} pointer=0x{instance.VehiclePointer:X8} " +
                    $"omsiResetWasCalculated={((conflictBits & ExternalControlWasCalculatedResetBit) != 0 ? 1 : 0)} " +
                    $"omsiRequestedPreCalc={((conflictBits & ExternalControlPreCalcRequestedBit) != 0 ? 1 : 0)} " +
                    $"omsiResetLoadedTile={((conflictBits & ExternalControlLoadedTileResetBit) != 0 ? 1 : 0)} " +
                    $"omsiReenabledPhysicsBody={((conflictBits & ExternalControlPhysicsBodyReenabledBit) != 0 ? 1 : 0)} " +
                    $"physicsSyncUnavailable={((conflictBits & ExternalControlPhysicsSyncUnavailableBit) != 0 ? 1 : 0)} " +
                    $"physicsSyncStatus={OmsiNativeInterop.GetLastVehiclePhysicsSyncStatus()}");
            }
        }

        RemoveMarkedStates();
        LogPathBindingComparison(now);
    }

    private static void LogPathBindingComparison(long now)
    {
        if (now - _lastPathBindingComparisonLogTickMs <
            PathBindingComparisonLogIntervalMs)
        {
            return;
        }

        var owned = PhysicalVehicleInstanceRegistry.Snapshot();
        if (owned.Length == 0)
        {
            return;
        }

        var ownedInstance = owned[0];
        if (!OmsiNativeInterop.TryReadRoadVehiclePathDiagnostics(
                ownedInstance.VehiclePointer,
                out var navbrPath))
        {
            return;
        }

        var ownedPointers = new HashSet<int>(
            owned.Select(instance => instance.VehiclePointer));
        var playerPointer = OmsiNativeInterop.GetPlayerVehiclePointer();
        if (!OmsiNativeInterop.TryFindNativeAiRoadVehicle(
                playerPointer,
                ownedPointers,
                out var nativeAiPointer,
                out var nativeAiPath))
        {
            return;
        }

        _lastPathBindingComparisonLogTickMs = now;
        PluginLogWriter.Enqueue(
            $"physical-path-compare navbr=0x{ownedInstance.VehiclePointer:X8} " +
            $"navbrPathFixed={navbrPath.PathFixed} navbrPAI={navbrPath.Pai} " +
            $"navbrCalc={navbrPath.WasCalculated} navbrPreCalc={navbrPath.NeedPreCalc} " +
            $"navbrLoadedTile={navbrPath.OnLoadedKachel} " +
            $"navbrPath={navbrPath.PathKachel}:{navbrPath.PathIndex}:{navbrPath.SubPath} " +
            $"navbrReverse={navbrPath.Reverse} navbrPathPos={navbrPath.PathX:F2},{navbrPath.PathY:F2},{navbrPath.PathZ:F2} " +
            $"navbrPathVel={navbrPath.Velocity:F2} navbrMoving={navbrPath.PaiMovingDistance:F2} " +
            $"navbrTrack={navbrPath.Track}:{navbrPath.TrackEntry} navbrCrossing={navbrPath.OnCrossing} " +
            $"ai=0x{nativeAiPointer:X8} aiPathFixed={nativeAiPath.PathFixed} aiPAI={nativeAiPath.Pai} " +
            $"aiCalc={nativeAiPath.WasCalculated} aiPreCalc={nativeAiPath.NeedPreCalc} " +
            $"aiLoadedTile={nativeAiPath.OnLoadedKachel} " +
            $"aiPath={nativeAiPath.PathKachel}:{nativeAiPath.PathIndex}:{nativeAiPath.SubPath} " +
            $"aiReverse={nativeAiPath.Reverse} aiPathPos={nativeAiPath.PathX:F2},{nativeAiPath.PathY:F2},{nativeAiPath.PathZ:F2} " +
            $"aiPathVel={nativeAiPath.Velocity:F2} aiMoving={nativeAiPath.PaiMovingDistance:F2} " +
            $"aiTrack={nativeAiPath.Track}:{nativeAiPath.TrackEntry} aiCrossing={nativeAiPath.OnCrossing}");
    }

    public static void Tick(long minimumIntervalOverrideMs = 0)
    {
        var now = Environment.TickCount64;
        var activeCount = States.Count;
        if (activeCount == 0)
        {
            _lastTickMs = now;
            return;
        }

        var minimumTickIntervalMs = activeCount switch
        {
            >= 9 => 50L, // 20 Hz when OMSI is already managing many remote buses.
            >= 5 => 33L, // ~30 Hz for medium rooms.
            _ => 20L     // 50 Hz maximum for a few nearby buses.
        };
        if (minimumIntervalOverrideMs > 0)
        {
            minimumTickIntervalMs = Math.Max(
                minimumTickIntervalMs,
                minimumIntervalOverrideMs);
        }
        if (_lastTickMs > 0 && now - _lastTickMs < minimumTickIntervalMs)
        {
            return;
        }

        _lastTickMs = now;
        RemovalScratch.Clear();
        foreach (var pair in States)
        {
            var instanceId = pair.Key;
            var state = pair.Value;

            if (!string.IsNullOrWhiteSpace(state.FaultCode))
            {
                continue;
            }

            if (!PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance) ||
                OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) != 1)
            {
                RemovalScratch.Add(instanceId);
                continue;
            }

            if (now - state.LastTargetTickMs >= StaleTargetAfterMs)
            {
                // If the desktop app, SignalR connection or named pipe dies
                // without a clean despawn, never leave an orphan NavBR bus in
                // OMSI indefinitely.
                if (PhysicalVehicleBackend.IsSafeOwnedPointer(instance, out _) &&
                    OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer) == 1)
                {
                    PhysicalVehicleInstanceRegistry.TryRemove(instanceId, out _);
                    RemovalScratch.Add(instanceId);
                }
                else
                {
                    // Preserve ownership and retry at a bounded cadence instead
                    // of forgetting a still-live OMSI object.
                    state.LastTargetTickMs =
                        now - StaleTargetAfterMs + 1_000;
                }
                continue;
            }

            var duration = Math.Max(1d, state.DurationMs);
            var amount = Math.Clamp((now - state.StartTickMs) / duration, 0d, 1d);
            var settled = IsSettled(state.Current, state.Target);
            var next = settled
                ? state.Target
                : Interpolate(state.Start, state.Target, amount);

            // The previous implementation kept writing the exact same native
            // transform every callback after interpolation had completed.
            // Stationary/settled remote buses now cost no transform write at
            // all until a new network target arrives.
            if (!settled &&
                !TryApplyTransform(instance, next, writeTileIndex: false))
            {
                state.FaultCode = "motion-transform-write-failed";
                state.FaultMessage =
                    "OMSI rejected a smoothed physical vehicle transform write.";
                continue;
            }

            if (now - state.LastReadbackTickMs >= ReadbackIntervalMs)
            {
                if (!TryConfirmTransform(
                        instance,
                        next,
                        validateTileIndex: false,
                        out var readbackErrorCode,
                        out var readbackErrorMessage))
                {
                    state.FaultCode =
                        readbackErrorCode ?? "motion-readback-failed";
                    state.FaultMessage =
                        readbackErrorMessage ??
                        "OMSI did not confirm the smoothed physical vehicle transform.";
                    continue;
                }

                state.LastReadbackTickMs = now;
            }

            state.Current = next;
        }

        RemoveMarkedStates();
    }

    private static void RemoveMarkedStates()
    {
        if (RemovalScratch.Count == 0)
        {
            return;
        }

        foreach (var instanceId in RemovalScratch)
        {
            States.Remove(instanceId);
        }

        RemovalScratch.Clear();
    }

    private static bool IsSettled(
        MotionSnapshot current,
        MotionSnapshot target)
    {
        if (Distance(current, target) > 0.01d ||
            Math.Abs(current.SpeedMps - target.SpeedMps) > 0.01f)
        {
            return false;
        }

        var rotationDot =
            current.RotationX * target.RotationX +
            current.RotationY * target.RotationY +
            current.RotationZ * target.RotationZ +
            current.RotationW * target.RotationW;

        return Math.Abs(rotationDot) >= 0.99999f;
    }

    public static void Remove(string? instanceId)
    {
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            States.Remove(instanceId);
        }
    }

    public static void Clear()
    {
        States.Clear();
        RemovalScratch.Clear();
        _lastTickMs = 0;
        _lastExternalControlTickMs = 0;
    }

    private static bool TryApplyTransform(
        PhysicalVehicleInstance instance,
        MotionSnapshot snapshot,
        bool writeTileIndex) =>
        PhysicalVehicleBackend.IsSafeOwnedPointer(instance, out _) &&
        OmsiNativeInterop.SetVehicleTransform(
            instance.VehiclePointer,
            snapshot.X,
            snapshot.Y,
            snapshot.Z,
            snapshot.RotationX,
            snapshot.RotationY,
            snapshot.RotationZ,
            snapshot.RotationW,
            snapshot.SpeedMps,
            snapshot.MapTileIndex == OmsiNativeInterop.HostPlayerTileSentinel
                ? OmsiNativeInterop.HostPlayerTileSentinel
                : writeTileIndex && snapshot.MapTileIndex is int mapTileIndex
                    ? mapTileIndex
                    : -1) == 1;

    private static bool TryConfirmTransform(
        PhysicalVehicleInstance instance,
        MotionSnapshot snapshot,
        bool validateTileIndex,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;

        if (OmsiNativeInterop.ReadRoadVehiclePosition(
                instance.VehiclePointer,
                out var actualX,
                out var actualY,
                out var actualZ) != 1)
        {
            errorCode = "motion-readback-unavailable";
            errorMessage =
                "OMSI did not expose a readable position after the physical vehicle transform.";
            return false;
        }

        var dx = (double)actualX - snapshot.X;
        var dy = (double)actualY - snapshot.Y;
        var dz = (double)actualZ - snapshot.Z;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (!double.IsFinite(distance) || distance > ReadbackToleranceMeters)
        {
            errorCode = "motion-transform-mismatch";
            errorMessage =
                $"OMSI physical vehicle readback differs from the requested smoothed pose by {distance:F2} m.";
            return false;
        }

        if (validateTileIndex &&
            snapshot.MapTileIndex is int expectedTileIndex &&
            expectedTileIndex >= 0)
        {
            var actualTileIndex =
                OmsiNativeInterop.ReadRoadVehicleTileIndex(
                    instance.VehiclePointer);
            if (actualTileIndex != expectedTileIndex)
            {
                errorCode = "motion-tile-mismatch";
                errorMessage =
                    $"OMSI physical vehicle is on Kachel {actualTileIndex}, expected {expectedTileIndex}.";
                return false;
            }
        }

        return true;
    }

    private static bool TryApplyVisualState(
        PhysicalVehicleInstance instance,
        PluginBridgeMessage command)
    {
        var lightFlags = command.LightFlags ?? 0;
        if (command.BrakePercent is double brakePercent &&
            double.IsFinite(brakePercent) &&
            brakePercent > 1d)
        {
            lightFlags |= (int)VehicleLightFlags.Brake;
        }

        return PhysicalVehicleBackend.IsSafeOwnedPointer(instance, out _) &&
               OmsiNativeInterop.SetVehicleVisualState(
                   instance.VehiclePointer,
                   lightFlags,
                   command.TurnSignal ?? 0) == 1;
    }

    private static bool TryReadSnapshot(
        PluginBridgeMessage command,
        out MotionSnapshot snapshot)
    {
        snapshot = default;
        if (command.LocalX is not double x || !double.IsFinite(x) ||
            command.LocalY is not double y || !double.IsFinite(y) ||
            command.LocalZ is not double z || !double.IsFinite(z) ||
            command.RotationX is not double rotationX || !double.IsFinite(rotationX) ||
            command.RotationY is not double rotationY || !double.IsFinite(rotationY) ||
            command.RotationZ is not double rotationZ || !double.IsFinite(rotationZ) ||
            command.RotationW is not double rotationW || !double.IsFinite(rotationW))
        {
            return false;
        }

        if (Math.Abs(x) > 100000d || Math.Abs(y) > 100000d || Math.Abs(z) > 100000d)
        {
            return false;
        }

        var length = Math.Sqrt(
            rotationX * rotationX +
            rotationY * rotationY +
            rotationZ * rotationZ +
            rotationW * rotationW);
        if (!double.IsFinite(length) || length < 0.0001d)
        {
            return false;
        }

        var inverse = 1d / length;
        var speedMps = command.SpeedKph is double speedKph && double.IsFinite(speedKph)
            ? (float)Math.Clamp(Math.Abs(speedKph) / 3.6d, 0d, 150d)
            : 0f;

        int? mapTileIndex = command.MapTileIndex is int rawTileIndex &&
                            (rawTileIndex == OmsiNativeInterop.HostPlayerTileSentinel ||
                             rawTileIndex is >= 0 and <= 200_000)
            ? rawTileIndex
            : null;

        snapshot = new MotionSnapshot(
            (float)x,
            (float)y,
            (float)z,
            (float)(rotationX * inverse),
            (float)(rotationY * inverse),
            (float)(rotationZ * inverse),
            (float)(rotationW * inverse),
            speedMps,
            mapTileIndex);
        return true;
    }

    private static MotionSnapshot Interpolate(
        MotionSnapshot from,
        MotionSnapshot to,
        double amount)
    {
        var t = (float)Math.Clamp(amount, 0d, 1d);

        var dot =
            from.RotationX * to.RotationX +
            from.RotationY * to.RotationY +
            from.RotationZ * to.RotationZ +
            from.RotationW * to.RotationW;
        var sign = dot < 0f ? -1f : 1f;

        var qx = Lerp(from.RotationX, to.RotationX * sign, t);
        var qy = Lerp(from.RotationY, to.RotationY * sign, t);
        var qz = Lerp(from.RotationZ, to.RotationZ * sign, t);
        var qw = Lerp(from.RotationW, to.RotationW * sign, t);
        var qLength = MathF.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
        if (float.IsFinite(qLength) && qLength > 0.0001f)
        {
            var inverse = 1f / qLength;
            qx *= inverse;
            qy *= inverse;
            qz *= inverse;
            qw *= inverse;
        }
        else
        {
            qx = to.RotationX;
            qy = to.RotationY;
            qz = to.RotationZ;
            qw = to.RotationW;
        }

        return new MotionSnapshot(
            Lerp(from.X, to.X, t),
            Lerp(from.Y, to.Y, t),
            Lerp(from.Z, to.Z, t),
            qx,
            qy,
            qz,
            qw,
            Lerp(from.SpeedMps, to.SpeedMps, t),
            to.MapTileIndex ?? from.MapTileIndex);
    }

    private static float Lerp(float from, float to, float amount) =>
        from + (to - from) * amount;

    private static double Distance(MotionSnapshot left, MotionSnapshot right)
    {
        var dx = (double)right.X - left.X;
        var dy = (double)right.Y - left.Y;
        var dz = (double)right.Z - left.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private sealed class MotionState
    {
        public MotionSnapshot Current { get; set; }
        public MotionSnapshot Start { get; set; }
        public MotionSnapshot Target { get; set; }
        public long StartTickMs { get; set; }
        public long LastTargetTickMs { get; set; }
        public double DurationMs { get; set; }
        public long? LastSourceTimestampMs { get; set; }
        public long LastReadbackTickMs { get; set; }
        public long LastExternalControlConflictLogTickMs { get; set; }
        public long LastPhysicsBodyLogTickMs { get; set; }
        public long LastFramePoseReassertTickMs { get; set; }
        public string? FaultCode { get; set; }
        public string? FaultMessage { get; set; }
    }

    private readonly record struct MotionSnapshot(
        float X,
        float Y,
        float Z,
        float RotationX,
        float RotationY,
        float RotationZ,
        float RotationW,
        float SpeedMps,
        int? MapTileIndex);
}
