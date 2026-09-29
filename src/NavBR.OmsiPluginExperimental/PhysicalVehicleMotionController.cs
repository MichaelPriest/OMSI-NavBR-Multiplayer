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
    private const double MinimumBufferedDelayMs = 120d;
    private const double MaximumBufferedDelayMs = 450d;
    private const double BufferedDelaySafetyMs = 20d;
    private const double MaximumExtrapolationMs = 300d;
    private const double ClockOffsetCreep = 0.01d;
    private const int MaximumBufferedSamples = 40;
    private const long SourceClockResetThresholdMs = 30_000;
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
    private const long ConsistPartComparisonLogIntervalMs = 10_000;
    private const long MaximumSettledPoseReassertIntervalMs = 250;
    private const long LightPoseProbeIntervalMs = 75;
    private const long MediumPoseProbeIntervalMs = 100;
    private const long CrowdedPoseProbeIntervalMs = 150;
    private const double FramePoseDriftToleranceMeters = 0.05d;
    private const int LateRenderControlSuccessBit = 1 << 0;
    private const int LateRenderControlCorrectedBit = 1 << 1;
    private const long LateRenderCorrectionLogIntervalMs = 2_000;

    private static readonly Dictionary<string, MotionState> States =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> RemovalScratch = new();
    private static long _lastTickMs;
    private static long _lastPathBindingComparisonLogTickMs;
    private static long _lastExternalControlTickMs;
    private static long _lastLateRenderControlTickMs;

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
            var failureStage =
                OmsiNativeInterop.GetLastVehicleTransformFailureStage();
            errorCode = failureStage == 31
                ? PluginBridgeProtocol.ErrorMotionWorldOriginUnavailable
                : "transform-write-failed";
            errorMessage = failureStage == 31
                ? "OMSI has not materialized a reliable world-space origin on the target Kachel yet."
                : $"OMSI rejected the guarded vehicle transform write at native stage {failureStage}.";
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
        var state = new MotionState
        {
            Current = snapshot,
            Start = snapshot,
            Target = snapshot,
            StartTickMs = now,
            LastTargetTickMs = now,
            DurationMs = DefaultInterpolationMs,
            LastSourceTimestampMs = command.TimestampUnixMilliseconds,
            LastReadbackTickMs = now,
            LastFramePoseProbeTickMs = now,
            LastFramePoseReassertTickMs = now
        };
        if (command.TimestampUnixMilliseconds is long sourceTimestampMs)
        {
            ResetBufferedTimeline(
                state,
                sourceTimestampMs,
                snapshot,
                now);
        }

        States[instance.InstanceId] = state;
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
        var sourceClockReset = false;
        if (sourceTimestamp is long sourceMs &&
            state.LastSourceTimestampMs is long previousSourceMs &&
            sourceMs <= previousSourceMs)
        {
            // Ordinary reordering must never rewind a remote bus. A large
            // backwards jump means the sender restarted or its wall clock was
            // corrected; start a new buffered timeline instead of ignoring that
            // player forever.
            if (previousSourceMs - sourceMs > SourceClockResetThresholdMs)
            {
                sourceClockReset = true;
            }
            else
            {
                return true;
            }
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

        if (target.RearSections is null &&
            state.Target.RearSections is { Length: > 0 } previousRearSections)
        {
            target = target with
            {
                RearSections = previousRearSections
            };
        }

        var now = Environment.TickCount64;

        // Timestamped multiplayer states follow a buffered sender-time
        // timeline. This is deliberately different from repeatedly easing from
        // Current to the newest arrival: arrival-time interpolation turns
        // network jitter into visible bus jitter and can make a moving remote
        // vehicle chase a target it never reaches.
        if (sourceTimestamp is long bufferedSourceMs)
        {
            if (sourceClockReset)
            {
                ResetBufferedTimeline(
                    state,
                    bufferedSourceMs,
                    target,
                    now);
            }
            else
            {
                QueueBufferedSample(
                    state,
                    bufferedSourceMs,
                    target,
                    now);
            }

            state.Target = target;
            state.LastTargetTickMs = now;
            state.LastSourceTimestampMs = sourceTimestamp;
            return true;
        }

        // Compatibility fallback for older bridge clients that do not stamp
        // telemetry. Preserve the existing arrival-time interpolation path.
        state.Samples.Clear();
        state.SourceToLocalOffsetMs = null;

        var tileChanged =
            target.MapTileIndex is int targetTileIndex &&
            state.Target.MapTileIndex != targetTileIndex;

        var gapMs = Math.Max(0L, now - state.LastTargetTickMs);
        var cadenceMs = gapMs;
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
                var failureStage =
                    OmsiNativeInterop.GetLastVehicleTransformFailureStage();
                errorCode = failureStage == 31
                    ? PluginBridgeProtocol.ErrorMotionWorldOriginUnavailable
                    : "transform-write-failed";
                errorMessage = failureStage == 31
                    ? "OMSI has not materialized a reliable world-space origin on the target Kachel yet; keeping the previous physical pose."
                    : $"OMSI rejected the guarded vehicle transform write at native stage {failureStage}.";
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
            state.LastFramePoseProbeTickMs = now;
            state.LastFramePoseReassertTickMs = now;
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
        var currentHostVehiclePointer =
            OmsiNativeInterop.GetPlayerVehiclePointer();
        var poseProbeIntervalMs = activeCount switch
        {
            >= 9 => CrowdedPoseProbeIntervalMs,
            >= 5 => MediumPoseProbeIntervalMs,
            _ => LightPoseProbeIntervalMs
        };
        RemovalScratch.Clear();
        foreach (var pair in States)
        {
            var instanceId = pair.Key;
            var state = pair.Value;
            if (!PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance))
            {
                RemovalScratch.Add(instanceId);
                continue;
            }

            // Keep only immutable provenance and the cached host identity
            // guard in managed code. The native keepalive itself validates live
            // RoadVehicles membership and re-reads PlayerVehicle immediately
            // before touching OMSI memory, so repeating IsRoadVehiclePointer
            // here would add one P/Invoke per bus every 16-33 ms with no extra
            // write safety.
            if (!PhysicalVehicleBackend.HasSafeOwnedPointerIdentity(
                    instance,
                    out var unsafeReason) ||
                (currentHostVehiclePointer != 0 &&
                 instance.VehiclePointer == currentHostVehiclePointer))
            {
                state.FaultCode = "motion-external-control-failed";
                state.FaultMessage =
                    $"NavBR stopped the RoadVehicle external-control keepalive: " +
                    $"{(string.IsNullOrWhiteSpace(unsafeReason) ? "pointer-is-current-host" : unsafeReason)}.";
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

            var posePointers =
                instance.GetPoseOrderedVehiclePointers();
            var activeRearCount = Math.Min(
                state.Current.RearSections?.Length ?? 0,
                Math.Max(0, posePointers.Length - 1));
            var rearControlFailed = false;
            for (var rearIndex = 0;
                 rearIndex < activeRearCount;
                 rearIndex++)
            {
                var rearPointer = posePointers[rearIndex + 1];
                if (!PhysicalVehicleBackend.IsSafeOwnedPointer(
                        instance,
                        rearPointer,
                        out _) ||
                    (OmsiNativeInterop.MaintainVehicleExternalControl(
                         rearPointer) &
                     ExternalControlSuccessBit) == 0)
                {
                    rearControlFailed = true;
                    break;
                }
            }

            if (rearControlFailed)
            {
                state.FaultCode =
                    "motion-rear-external-control-failed";
                state.FaultMessage =
                    "OMSI rejected NavBR external control for an articulated vehicle section.";
                continue;
            }

            var conflictBits =
                externalControlResult & ExternalControlConflictMask;
            var physicsReenabled =
                (conflictBits & ExternalControlPhysicsBodyReenabledBit) != 0;
            var periodicRefresh =
                now - state.LastFramePoseReassertTickMs >=
                MaximumSettledPoseReassertIntervalMs;

            // Keep the ownership keepalive at the frame cadence, but avoid a
            // second native position read for every remote bus on every frame.
            // While a bus is moving, Tick() already writes the network pose.
            // Settled buses are probed often enough to catch OMSI drift well
            // before the bounded 250 ms full-pose refresh. Any ownership
            // conflict or diagnostic sample still forces an immediate probe.
            var settled = IsSettled(state.Current, state.Target);
            var physicsDiagnosticDue =
                now - state.LastPhysicsBodyLogTickMs >=
                PhysicsBodyComparisonLogIntervalMs;
            var poseProbeDue =
                state.LastFramePoseProbeTickMs <= 0 ||
                now - state.LastFramePoseProbeTickMs >= poseProbeIntervalMs;
            var shouldProbeObjectPosition =
                conflictBits != 0 ||
                physicsDiagnosticDue ||
                (settled && poseProbeDue);

            var hasObjectPosition = false;
            var objectX = 0f;
            var objectY = 0f;
            var objectZ = 0f;
            if (shouldProbeObjectPosition)
            {
                state.LastFramePoseProbeTickMs = now;
                hasObjectPosition =
                    OmsiNativeInterop.ReadRoadVehiclePosition(
                        instance.VehiclePointer,
                        out objectX,
                        out objectY,
                        out objectZ) == 1;
            }

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
                var failureStage =
                    OmsiNativeInterop.GetLastVehicleTransformFailureStage();

                // A failed periodic/render reassert is not automatically proof
                // that ownership was lost. MaintainExternalControl succeeded for
                // this exact pointer immediately above, so matrix/ODE fields can
                // still be transiently unavailable while OMSI is materializing
                // or reshuffling render state. Only the native safety stages
                // that prove an invalid/current-player target are latched as
                // fatal; all other failures back off to the normal 250 ms
                // refresh cadence and retry without poisoning the next network
                // target with a permanent FaultCode.
                if (failureStage is 1 or 8)
                {
                    state.FaultCode = "motion-frame-pose-reassert-failed";
                    state.FaultMessage =
                        $"OMSI rejected the bounded NavBR pose reassertion at unsafe native stage {failureStage}.";
                    continue;
                }

                state.LastFramePoseReassertTickMs = now;
                if (now - state.LastFramePoseReassertFailureLogTickMs >=
                        ExternalControlConflictLogIntervalMs)
                {
                    state.LastFramePoseReassertFailureLogTickMs = now;
                    PluginLogWriter.Enqueue(
                        $"physical-frame-reassert-retry id={instanceId} pointer=0x{instance.VehiclePointer:X8} " +
                        $"stage={failureStage} drift={(poseDrifted ? 1 : 0)} " +
                        $"physicsReenabled={(physicsReenabled ? 1 : 0)} periodic={(periodicRefresh ? 1 : 0)}");
                }

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

            LogConsistPartPositions(instance, state, now);

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

    public static void MaintainLateRenderControl()
    {
        var now = Environment.TickCount64;
        var activeCount = States.Count;
        if (activeCount == 0)
        {
            _lastLateRenderControlTickMs = now;
            return;
        }

        // This runs from a later OMSI system-variable callback than the main
        // frame anchor. It performs only a render-matrix consistency probe and
        // writes matrices/visibility when OMSI changed them after NavBR's
        // normal motion callback. No queue, ODE, lifecycle or interpolation
        // work is repeated here.
        var minimumIntervalMs = activeCount switch
        {
            >= 9 => 50L,
            >= 5 => 33L,
            _ => 16L
        };
        if (_lastLateRenderControlTickMs > 0 &&
            now - _lastLateRenderControlTickMs < minimumIntervalMs)
        {
            return;
        }

        _lastLateRenderControlTickMs = now;
        foreach (var pair in States)
        {
            var instanceId = pair.Key;
            var state = pair.Value;
            if (!string.IsNullOrWhiteSpace(state.FaultCode) ||
                !PhysicalVehicleInstanceRegistry.TryGet(
                    instanceId,
                    out var instance) ||
                !PhysicalVehicleBackend.HasSafeOwnedPointerIdentity(
                    instance,
                    out _))
            {
                continue;
            }

            var result =
                OmsiNativeInterop.MaintainVehicleRenderControl(
                    instance.VehiclePointer,
                    state.Current.X,
                    state.Current.Y,
                    state.Current.Z,
                    state.Current.RotationX,
                    state.Current.RotationY,
                    state.Current.RotationZ,
                    state.Current.RotationW);
            if ((result & LateRenderControlSuccessBit) == 0)
            {
                if (now - state.LastLateRenderFailureLogTickMs >=
                        LateRenderCorrectionLogIntervalMs)
                {
                    state.LastLateRenderFailureLogTickMs = now;
                    PluginLogWriter.Enqueue(
                        $"physical-late-render-retry id={instanceId} pointer=0x{instance.VehiclePointer:X8}");
                }
                continue;
            }

            var correctedParts =
                (result & LateRenderControlCorrectedBit) != 0
                    ? 1
                    : 0;
            var rearRenderFailed = false;
            if (state.Current.RearSections is { Length: > 0 } rearSections)
            {
                var posePointers =
                    instance.GetPoseOrderedVehiclePointers();
                var rearCount = Math.Min(
                    rearSections.Length,
                    Math.Max(0, posePointers.Length - 1));
                for (var rearIndex = 0;
                     rearIndex < rearCount;
                     rearIndex++)
                {
                    var pointer = posePointers[rearIndex + 1];
                    var section = rearSections[rearIndex];
                    if (!PhysicalVehicleBackend.IsSafeOwnedPointer(
                            instance,
                            pointer,
                            out _))
                    {
                        rearRenderFailed = true;
                        break;
                    }

                    var rearResult =
                        OmsiNativeInterop.MaintainVehicleRenderControl(
                            pointer,
                            section.X,
                            section.Y,
                            section.Z,
                            section.RotationX,
                            section.RotationY,
                            section.RotationZ,
                            section.RotationW);
                    if ((rearResult & LateRenderControlSuccessBit) == 0)
                    {
                        rearRenderFailed = true;
                        break;
                    }

                    if ((rearResult & LateRenderControlCorrectedBit) != 0)
                    {
                        correctedParts++;
                    }
                }
            }

            if (rearRenderFailed)
            {
                if (now - state.LastLateRenderFailureLogTickMs >=
                        LateRenderCorrectionLogIntervalMs)
                {
                    state.LastLateRenderFailureLogTickMs = now;
                    PluginLogWriter.Enqueue(
                        $"physical-late-render-retry id={instanceId} articulated=1");
                }
                continue;
            }

            if (correctedParts > 0 &&
                now - state.LastLateRenderCorrectionLogTickMs >=
                    LateRenderCorrectionLogIntervalMs)
            {
                state.LastLateRenderCorrectionLogTickMs = now;
                PluginLogWriter.Enqueue(
                    $"physical-late-render-corrected id={instanceId} parts={correctedParts}");
            }
        }
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

            if (!PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance))
            {
                RemovalScratch.Add(instanceId);
                continue;
            }

            // MaintainExternalControl runs before Tick on the OMSI frame
            // callback and validates live RoadVehicles membership. Moving
            // transforms repeat that validation inside the native write, while
            // stale removal below still uses the full managed ownership guard.
            // Avoid another IsRoadVehiclePointer P/Invoke for every bus/tick.

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

            var next = ResolveMotionSnapshot(
                state,
                now,
                out var writeTileIndex);
            var settled = IsSettled(state.Current, next);

            // The previous implementation kept writing the exact same native
            // transform every callback after interpolation had completed.
            // Stationary/settled remote buses now cost no transform write at
            // all until the buffered timeline advances.
            if (!settled &&
                !TryApplyTransform(
                    instance,
                    next,
                    writeTileIndex))
            {
                var failureStage =
                    OmsiNativeInterop.GetLastVehicleTransformFailureStage();

                // During a Kachel transition the target tile can be known
                // before OMSI has materialized its world-space origin. Hold the
                // previous physical pose and retry on the next callback rather
                // than poisoning ownership and forcing a despawn/spawn cycle.
                if (failureStage == 31)
                {
                    continue;
                }

                state.FaultCode = "motion-transform-write-failed";
                state.FaultMessage =
                    $"OMSI rejected a smoothed physical vehicle transform write at native stage {failureStage}.";
                continue;
            }

            if (writeTileIndex ||
                now - state.LastReadbackTickMs >= ReadbackIntervalMs)
            {
                if (!TryConfirmTransform(
                        instance,
                        next,
                        validateTileIndex: writeTileIndex,
                        out var readbackErrorCode,
                        out var readbackErrorMessage))
                {
                    if (PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
                            readbackErrorCode))
                    {
                        // Keep Current on the last confirmed Kachel/pose so the
                        // exact transition is retried instead of being accepted
                        // from an uncertain readback.
                        continue;
                    }

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

    private static void ResetBufferedTimeline(
        MotionState state,
        long sourceTimestampMs,
        MotionSnapshot snapshot,
        long arrivalTickMs)
    {
        state.Samples.Clear();
        state.SourceToLocalOffsetMs = null;
        QueueBufferedSample(
            state,
            sourceTimestampMs,
            snapshot,
            arrivalTickMs);
    }

    private static void QueueBufferedSample(
        MotionState state,
        long sourceTimestampMs,
        MotionSnapshot snapshot,
        long arrivalTickMs)
    {
        var candidateOffsetMs =
            arrivalTickMs - (double)sourceTimestampMs;
        if (state.SourceToLocalOffsetMs is double previousOffsetMs)
        {
            state.SourceToLocalOffsetMs =
                candidateOffsetMs < previousOffsetMs
                    ? candidateOffsetMs
                    : previousOffsetMs +
                      (candidateOffsetMs - previousOffsetMs) *
                      ClockOffsetCreep;
        }
        else
        {
            state.SourceToLocalOffsetMs = candidateOffsetMs;
        }

        state.Samples.Add(
            new MotionSample(
                sourceTimestampMs,
                snapshot));
        while (state.Samples.Count > MaximumBufferedSamples)
        {
            state.Samples.RemoveAt(0);
        }
    }

    private static MotionSnapshot ResolveMotionSnapshot(
        MotionState state,
        long now,
        out bool writeTileIndex)
    {
        MotionSnapshot next;
        if (state.SourceToLocalOffsetMs is double offsetMs &&
            state.Samples.Count > 0)
        {
            var renderSourceMs =
                now -
                offsetMs -
                ResolveBufferedDelayMs(state);
            next = SampleBufferedTimeline(
                state.Samples,
                renderSourceMs);
        }
        else
        {
            var duration = Math.Max(1d, state.DurationMs);
            var amount =
                Math.Clamp(
                    (now - state.StartTickMs) / duration,
                    0d,
                    1d);
            next = Interpolate(
                state.Start,
                state.Target,
                amount);
        }

        writeTileIndex =
            next.MapTileIndex is int nextTileIndex &&
            state.Current.MapTileIndex != nextTileIndex;
        return next;
    }

    private static double ResolveBufferedDelayMs(
        MotionState state)
    {
        if (state.Samples.Count < 2)
        {
            return DefaultInterpolationMs;
        }

        var maximumRecentGapMs = 0d;
        var firstIndex =
            Math.Max(1, state.Samples.Count - 4);
        for (var i = firstIndex;
             i < state.Samples.Count;
             i++)
        {
            var gapMs =
                state.Samples[i].SourceTimestampMs -
                state.Samples[i - 1].SourceTimestampMs;
            if (gapMs is > 0 and <= 1_000)
            {
                maximumRecentGapMs =
                    Math.Max(
                        maximumRecentGapMs,
                        gapMs);
            }
        }

        if (maximumRecentGapMs <= 0d)
        {
            return DefaultInterpolationMs;
        }

        return Math.Clamp(
            maximumRecentGapMs * 2d +
            BufferedDelaySafetyMs,
            MinimumBufferedDelayMs,
            MaximumBufferedDelayMs);
    }

    private static MotionSnapshot SampleBufferedTimeline(
        List<MotionSample> samples,
        double renderSourceMs)
    {
        var first = samples[0];
        if (renderSourceMs <= first.SourceTimestampMs)
        {
            return first.Snapshot;
        }

        var leftIndex = samples.Count - 1;
        for (var i = samples.Count - 1;
             i >= 0;
             i--)
        {
            if (samples[i].SourceTimestampMs <=
                renderSourceMs)
            {
                leftIndex = i;
                break;
            }
        }

        if (leftIndex + 1 < samples.Count)
        {
            var left = samples[leftIndex];
            var right = samples[leftIndex + 1];
            if (IsBufferedDiscontinuity(left, right))
            {
                // Local OMSI coordinates belong to a Kachel. Never blend two
                // Kachel frames or a deliberate teleport. Hold the old pose
                // until the sender-time cursor reaches the new sample, then
                // switch tile and pose together.
                return left.Snapshot;
            }

            var spanMs =
                Math.Max(
                    1d,
                    right.SourceTimestampMs -
                    left.SourceTimestampMs);
            var amount =
                Math.Clamp(
                    (renderSourceMs -
                     left.SourceTimestampMs) /
                    spanMs,
                    0d,
                    1d);
            return Interpolate(
                left.Snapshot,
                right.Snapshot,
                amount);
        }

        var last = samples[^1];
        var aheadMs =
            Math.Clamp(
                renderSourceMs -
                last.SourceTimestampMs,
                0d,
                MaximumExtrapolationMs);
        return Extrapolate(
            last.Snapshot,
            aheadMs);
    }

    private static bool IsBufferedDiscontinuity(
        MotionSample left,
        MotionSample right)
    {
        if (right.SourceTimestampMs -
                left.SourceTimestampMs >=
            TeleportGapMs)
        {
            return true;
        }

        if (left.Snapshot.MapTileIndex is int leftTile &&
            right.Snapshot.MapTileIndex is int rightTile &&
            leftTile != rightTile)
        {
            return true;
        }

        return Distance(
            left.Snapshot,
            right.Snapshot) >=
            TeleportDistanceMeters;
    }

    private static MotionSnapshot Extrapolate(
        MotionSnapshot source,
        double aheadMs)
    {
        if (!source.HasVelocity ||
            aheadMs <= 0d)
        {
            return source;
        }

        var seconds =
            (float)(aheadMs / 1_000d);
        var x =
            source.X +
            source.VelocityX * seconds;
        var y =
            source.Y +
            source.VelocityY * seconds;
        var z =
            source.Z +
            source.VelocityZ * seconds;
        if (!float.IsFinite(x) ||
            !float.IsFinite(y) ||
            !float.IsFinite(z))
        {
            return source;
        }

        SectionSnapshot[]? rearSections = source.RearSections;
        if (rearSections is { Length: > 0 } &&
            source.MapTileIndex is int frontTileIndex)
        {
            var deltaX = source.VelocityX * seconds;
            var deltaY = source.VelocityY * seconds;
            var deltaZ = source.VelocityZ * seconds;
            rearSections = rearSections
                .Select(section =>
                    section.MapTileIndex == frontTileIndex
                        ? section with
                        {
                            X = section.X + deltaX,
                            Y = section.Y + deltaY,
                            Z = section.Z + deltaZ
                        }
                        : section)
                .ToArray();
        }

        return source with
        {
            X = x,
            Y = y,
            Z = z,
            RearSections = rearSections
        };
    }

    private static bool IsSettled(
        MotionSnapshot current,
        MotionSnapshot target)
    {
        var dx = (double)target.X - current.X;
        var dy = (double)target.Y - current.Y;
        var dz = (double)target.Z - current.Z;
        if ((target.MapTileIndex is int targetTileIndex &&
             current.MapTileIndex != targetTileIndex) ||
            dx * dx + dy * dy + dz * dz > 0.0001d ||
            Math.Abs(current.SpeedMps - target.SpeedMps) > 0.01f)
        {
            return false;
        }

        var rotationDot =
            current.RotationX * target.RotationX +
            current.RotationY * target.RotationY +
            current.RotationZ * target.RotationZ +
            current.RotationW * target.RotationW;

        return Math.Abs(rotationDot) >= 0.99999f &&
               AreRearSectionsSettled(
                   current.RearSections,
                   target.RearSections);
    }

    private static bool AreRearSectionsSettled(
        SectionSnapshot[]? current,
        SectionSnapshot[]? target)
    {
        if (current is null || target is null)
        {
            return current is null && target is null;
        }

        if (current.Length != target.Length)
        {
            return false;
        }

        for (var index = 0; index < current.Length; index++)
        {
            var left = current[index];
            var right = target[index];
            var dx = (double)right.X - left.X;
            var dy = (double)right.Y - left.Y;
            var dz = (double)right.Z - left.Z;
            var rotationDot =
                left.RotationX * right.RotationX +
                left.RotationY * right.RotationY +
                left.RotationZ * right.RotationZ +
                left.RotationW * right.RotationW;
            if (left.MapTileIndex != right.MapTileIndex ||
                dx * dx + dy * dy + dz * dz > 0.0001d ||
                Math.Abs(rotationDot) < 0.99999f)
            {
                return false;
            }
        }

        return true;
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
        _lastLateRenderControlTickMs = 0;
    }

    private static bool TryApplyTransform(
        PhysicalVehicleInstance instance,
        MotionSnapshot snapshot,
        bool writeTileIndex)
    {
        // Static provenance is checked here; native writes perform the live
        // RoadVehicles membership and current PlayerVehicle guards at the
        // actual memory-write boundary.
        if (!PhysicalVehicleBackend.HasSafeOwnedPointerIdentity(
                instance,
                out _))
        {
            return false;
        }

        if (OmsiNativeInterop.SetVehicleTransform(
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
                    : writeTileIndex &&
                      snapshot.MapTileIndex is int mapTileIndex
                        ? mapTileIndex
                        : -1) != 1)
        {
            return false;
        }

        if (!TryApplyRearSectionTransforms(
                instance,
                snapshot))
        {
            return false;
        }

        if (!snapshot.HasVelocity &&
            !snapshot.HasAccelerationLocal)
        {
            return true;
        }

        // SetVehicleTransform already wrote a safe fallback. Exact motion
        // vectors are an optional fidelity upgrade; a guarded rejection here
        // must never turn a valid pose write into a fatal despawn/respan loop.
        // The native export independently protects PlayerVehicle and stale
        // RoadVehicle pointers, so ignoring a rejected optional overwrite is
        // safe and preserves the fallback state.
        _ = OmsiNativeInterop.SetVehicleNetworkMotion(
            instance.VehiclePointer,
            snapshot.HasVelocity ? 1 : 0,
            snapshot.VelocityX,
            snapshot.VelocityY,
            snapshot.VelocityZ,
            snapshot.HasAccelerationLocal ? 1 : 0,
            snapshot.AccelerationLocalX,
            snapshot.AccelerationLocalY,
            snapshot.AccelerationLocalZ);
        return true;
    }

    private static bool TryApplyRearSectionTransforms(
        PhysicalVehicleInstance instance,
        MotionSnapshot snapshot)
    {
        if (snapshot.RearSections is not { Length: > 0 } rearSections)
        {
            return true;
        }

        var posePointers =
            instance.GetPoseOrderedVehiclePointers();
        var availableRearCount =
            Math.Max(0, posePointers.Length - 1);
        var count =
            Math.Min(rearSections.Length, availableRearCount);
        for (var index = 0; index < count; index++)
        {
            var pointer = posePointers[index + 1];
            var section = rearSections[index];
            if (!PhysicalVehicleBackend.IsSafeOwnedPointer(
                    instance,
                    pointer,
                    out _) ||
                OmsiNativeInterop.SetVehicleTransform(
                    pointer,
                    section.X,
                    section.Y,
                    section.Z,
                    section.RotationX,
                    section.RotationY,
                    section.RotationZ,
                    section.RotationW,
                    snapshot.SpeedMps,
                    section.MapTileIndex) != 1)
            {
                return false;
            }
        }

        return true;
    }

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
            errorCode = PluginBridgeProtocol.ErrorMotionReadbackUnavailable;
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
            errorCode = PluginBridgeProtocol.ErrorMotionTransformMismatch;
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
                errorCode = PluginBridgeProtocol.ErrorMotionTileMismatch;
                errorMessage =
                    $"OMSI physical vehicle is on Kachel {actualTileIndex}, expected {expectedTileIndex}.";
                return false;
            }
        }

        if (snapshot.RearSections is { Length: > 0 } rearSections)
        {
            var posePointers =
                instance.GetPoseOrderedVehiclePointers();
            var count = Math.Min(
                rearSections.Length,
                Math.Max(0, posePointers.Length - 1));
            for (var index = 0; index < count; index++)
            {
                var pointer = posePointers[index + 1];
                var section = rearSections[index];
                if (OmsiNativeInterop.ReadRoadVehiclePosition(
                        pointer,
                        out var rearX,
                        out var rearY,
                        out var rearZ) != 1)
                {
                    errorCode =
                        PluginBridgeProtocol.ErrorMotionReadbackUnavailable;
                    errorMessage =
                        $"OMSI did not expose articulated section {index + 1} after the network transform.";
                    return false;
                }

                var rearDx = (double)rearX - section.X;
                var rearDy = (double)rearY - section.Y;
                var rearDz = (double)rearZ - section.Z;
                var rearDistance = Math.Sqrt(
                    rearDx * rearDx +
                    rearDy * rearDy +
                    rearDz * rearDz);
                var rearTile =
                    OmsiNativeInterop.ReadRoadVehicleTileIndex(pointer);
                if (!double.IsFinite(rearDistance) ||
                    rearDistance > ReadbackToleranceMeters)
                {
                    errorCode =
                        PluginBridgeProtocol.ErrorMotionTransformMismatch;
                    errorMessage =
                        $"OMSI articulated section {index + 1} readback differs by {rearDistance:F2} m.";
                    return false;
                }

                if (rearTile != section.MapTileIndex)
                {
                    errorCode =
                        PluginBridgeProtocol.ErrorMotionTileMismatch;
                    errorMessage =
                        $"OMSI articulated section {index + 1} is on Kachel {rearTile}, expected {section.MapTileIndex}.";
                    return false;
                }
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

        // Make the whole OMSI consist look like one remote vehicle. MakeVehicle
        // can return a main RoadVehicle plus one or more trailer/articulated
        // RoadVehicles; applying lamps only to the primary left the rear
        // sections visually disconnected from the player state. Every write
        // still passes the exact owned-pointer/current-player guard.
        foreach (var vehiclePointer in instance.GetOwnedVehiclePointers())
        {
            if (!PhysicalVehicleBackend.IsSafeOwnedPointer(
                    instance,
                    vehiclePointer,
                    out _) ||
                OmsiNativeInterop.SetVehicleVisualState(
                    vehiclePointer,
                    lightFlags,
                    command.TurnSignal ?? 0) != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static void LogConsistPartPositions(
        PhysicalVehicleInstance instance,
        MotionState state,
        long now)
    {
        var pointers = instance.GetOwnedVehiclePointers();
        if (pointers.Length <= 1 ||
            now - state.LastConsistPartsLogTickMs <
                ConsistPartComparisonLogIntervalMs)
        {
            return;
        }

        state.LastConsistPartsLogTickMs = now;
        var parts = new List<string>(pointers.Length);
        foreach (var vehiclePointer in pointers)
        {
            if (!PhysicalVehicleBackend.IsSafeOwnedPointer(
                    instance,
                    vehiclePointer,
                    out var unsafeReason))
            {
                parts.Add(
                    $"0x{vehiclePointer:X8}:unsafe={unsafeReason}");
                continue;
            }

            if (OmsiNativeInterop.ReadRoadVehiclePosition(
                    vehiclePointer,
                    out var x,
                    out var y,
                    out var z) != 1)
            {
                parts.Add(
                    $"0x{vehiclePointer:X8}:pose=unavailable");
                continue;
            }

            var tileIndex =
                OmsiNativeInterop.ReadRoadVehicleTileIndex(vehiclePointer);
            parts.Add(
                $"0x{vehiclePointer:X8}:kachel={tileIndex},local=({x:F2},{y:F2},{z:F2})");
        }

        PluginLogWriter.Enqueue(
            $"physical-consist-parts id={instance.InstanceId} primary=0x{instance.VehiclePointer:X8} " +
            $"partCount={pointers.Length} parts=[{string.Join(" | ", parts)}]");
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

        var hasVelocity = TryReadBoundedVector(
            command.VelocityX,
            command.VelocityY,
            command.VelocityZ,
            maximumMagnitude: 150f,
            out var velocityX,
            out var velocityY,
            out var velocityZ);
        var hasAccelerationLocal = TryReadBoundedVector(
            command.AccelerationLocalX,
            command.AccelerationLocalY,
            command.AccelerationLocalZ,
            maximumMagnitude: 100f,
            out var accelerationLocalX,
            out var accelerationLocalY,
            out var accelerationLocalZ);
        if (!TryReadRearSectionSnapshots(
                command.RearSections,
                out var rearSections))
        {
            return false;
        }

        snapshot = new MotionSnapshot(
            (float)x,
            (float)y,
            (float)z,
            (float)(rotationX * inverse),
            (float)(rotationY * inverse),
            (float)(rotationZ * inverse),
            (float)(rotationW * inverse),
            speedMps,
            mapTileIndex,
            hasVelocity,
            velocityX,
            velocityY,
            velocityZ,
            hasAccelerationLocal,
            accelerationLocalX,
            accelerationLocalY,
            accelerationLocalZ,
            rearSections);
        return true;
    }

    private static bool TryReadRearSectionSnapshots(
        VehicleSectionPose[]? sections,
        out SectionSnapshot[]? snapshots)
    {
        snapshots = null;
        if (sections is null)
        {
            return true;
        }

        if (sections.Length > 3)
        {
            return false;
        }

        snapshots = new SectionSnapshot[sections.Length];
        for (var index = 0; index < sections.Length; index++)
        {
            var section = sections[index];
            if (!double.IsFinite(section.LocalX) ||
                !double.IsFinite(section.LocalY) ||
                !double.IsFinite(section.LocalZ) ||
                !double.IsFinite(section.RotationX) ||
                !double.IsFinite(section.RotationY) ||
                !double.IsFinite(section.RotationZ) ||
                !double.IsFinite(section.RotationW) ||
                Math.Abs(section.LocalX) > 100_000d ||
                Math.Abs(section.LocalY) > 100_000d ||
                Math.Abs(section.LocalZ) > 100_000d ||
                section.MapTileIndex is not int mapTileIndex ||
                mapTileIndex is < 0 or > 200_000)
            {
                return false;
            }

            var length = Math.Sqrt(
                section.RotationX * section.RotationX +
                section.RotationY * section.RotationY +
                section.RotationZ * section.RotationZ +
                section.RotationW * section.RotationW);
            if (!double.IsFinite(length) ||
                length < 0.0001d)
            {
                return false;
            }

            var inverse = 1d / length;
            snapshots[index] = new SectionSnapshot(
                (float)section.LocalX,
                (float)section.LocalY,
                (float)section.LocalZ,
                (float)(section.RotationX * inverse),
                (float)(section.RotationY * inverse),
                (float)(section.RotationZ * inverse),
                (float)(section.RotationW * inverse),
                mapTileIndex);
        }

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

        var interpolateVelocity =
            from.HasVelocity &&
            to.HasVelocity;
        var interpolateAcceleration =
            from.HasAccelerationLocal &&
            to.HasAccelerationLocal;

        return new MotionSnapshot(
            Lerp(from.X, to.X, t),
            Lerp(from.Y, to.Y, t),
            Lerp(from.Z, to.Z, t),
            qx,
            qy,
            qz,
            qw,
            Lerp(from.SpeedMps, to.SpeedMps, t),
            to.MapTileIndex ?? from.MapTileIndex,
            to.HasVelocity,
            interpolateVelocity
                ? Lerp(from.VelocityX, to.VelocityX, t)
                : to.VelocityX,
            interpolateVelocity
                ? Lerp(from.VelocityY, to.VelocityY, t)
                : to.VelocityY,
            interpolateVelocity
                ? Lerp(from.VelocityZ, to.VelocityZ, t)
                : to.VelocityZ,
            to.HasAccelerationLocal,
            interpolateAcceleration
                ? Lerp(
                    from.AccelerationLocalX,
                    to.AccelerationLocalX,
                    t)
                : to.AccelerationLocalX,
            interpolateAcceleration
                ? Lerp(
                    from.AccelerationLocalY,
                    to.AccelerationLocalY,
                    t)
                : to.AccelerationLocalY,
            interpolateAcceleration
                ? Lerp(
                    from.AccelerationLocalZ,
                    to.AccelerationLocalZ,
                    t)
                : to.AccelerationLocalZ,
            InterpolateRearSections(
                from.RearSections,
                to.RearSections,
                t));
    }

    private static SectionSnapshot[]? InterpolateRearSections(
        SectionSnapshot[]? from,
        SectionSnapshot[]? to,
        float amount)
    {
        if (to is null)
        {
            return from;
        }

        if (from is null ||
            from.Length != to.Length)
        {
            return amount >= 1f ? to : from;
        }

        if (to.Length == 0)
        {
            return to;
        }

        var result = new SectionSnapshot[to.Length];
        for (var index = 0; index < to.Length; index++)
        {
            var left = from[index];
            var right = to[index];
            if (left.MapTileIndex != right.MapTileIndex)
            {
                result[index] =
                    amount >= 1f ? right : left;
                continue;
            }

            var dot =
                left.RotationX * right.RotationX +
                left.RotationY * right.RotationY +
                left.RotationZ * right.RotationZ +
                left.RotationW * right.RotationW;
            var sign = dot < 0f ? -1f : 1f;
            var qx = Lerp(
                left.RotationX,
                right.RotationX * sign,
                amount);
            var qy = Lerp(
                left.RotationY,
                right.RotationY * sign,
                amount);
            var qz = Lerp(
                left.RotationZ,
                right.RotationZ * sign,
                amount);
            var qw = Lerp(
                left.RotationW,
                right.RotationW * sign,
                amount);
            var qLength =
                MathF.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (float.IsFinite(qLength) &&
                qLength > 0.0001f)
            {
                var inverse = 1f / qLength;
                qx *= inverse;
                qy *= inverse;
                qz *= inverse;
                qw *= inverse;
            }
            else
            {
                qx = right.RotationX;
                qy = right.RotationY;
                qz = right.RotationZ;
                qw = right.RotationW;
            }

            result[index] = new SectionSnapshot(
                Lerp(left.X, right.X, amount),
                Lerp(left.Y, right.Y, amount),
                Lerp(left.Z, right.Z, amount),
                qx,
                qy,
                qz,
                qw,
                right.MapTileIndex);
        }

        return result;
    }

    private static bool TryReadBoundedVector(
        double? x,
        double? y,
        double? z,
        float maximumMagnitude,
        out float valueX,
        out float valueY,
        out float valueZ)
    {
        valueX = 0f;
        valueY = 0f;
        valueZ = 0f;
        if (x is not double sourceX ||
            y is not double sourceY ||
            z is not double sourceZ ||
            !double.IsFinite(sourceX) ||
            !double.IsFinite(sourceY) ||
            !double.IsFinite(sourceZ))
        {
            return false;
        }

        var magnitudeSquared =
            sourceX * sourceX +
            sourceY * sourceY +
            sourceZ * sourceZ;
        var maximumSquared =
            (double)maximumMagnitude * maximumMagnitude;
        if (!double.IsFinite(magnitudeSquared) ||
            magnitudeSquared > maximumSquared)
        {
            return false;
        }

        valueX = (float)sourceX;
        valueY = (float)sourceY;
        valueZ = (float)sourceZ;
        return true;
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
        public long LastFramePoseProbeTickMs { get; set; }
        public long LastExternalControlConflictLogTickMs { get; set; }
        public long LastPhysicsBodyLogTickMs { get; set; }
        public long LastConsistPartsLogTickMs { get; set; }
        public long LastFramePoseReassertTickMs { get; set; }
        public long LastFramePoseReassertFailureLogTickMs { get; set; }
        public long LastLateRenderCorrectionLogTickMs { get; set; }
        public long LastLateRenderFailureLogTickMs { get; set; }
        public List<MotionSample> Samples { get; } = new();
        public double? SourceToLocalOffsetMs { get; set; }
        public string? FaultCode { get; set; }
        public string? FaultMessage { get; set; }
    }

    private readonly record struct MotionSample(
        long SourceTimestampMs,
        MotionSnapshot Snapshot);

    private readonly record struct MotionSnapshot(
        float X,
        float Y,
        float Z,
        float RotationX,
        float RotationY,
        float RotationZ,
        float RotationW,
        float SpeedMps,
        int? MapTileIndex,
        bool HasVelocity,
        float VelocityX,
        float VelocityY,
        float VelocityZ,
        bool HasAccelerationLocal,
        float AccelerationLocalX,
        float AccelerationLocalY,
        float AccelerationLocalZ,
        SectionSnapshot[]? RearSections);

    private readonly record struct SectionSnapshot(
        float X,
        float Y,
        float Z,
        float RotationX,
        float RotationY,
        float RotationZ,
        float RotationW,
        int MapTileIndex);
}
