using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Owns the long-lived physical-bus lifecycle on OMSI's callback thread.
/// Desktop commands express intent; this supervisor keeps the exact vehicle
/// alive, retries transient spawn/materialisation failures, and recovers a
/// stale pointer without requiring the desktop to replay native operations.
/// </summary>
internal static class PhysicalVehicleLifecycleSupervisor
{
    private const long StaleIntentAfterMs = 15_000;
    private const long MaterializationRetryMs = 250;
    private const long TransientRetryMs = 750;
    private const long SlowRetryMs = 2_000;
    private const int MaxEntries = 32;
    private const long TargetRateLogIntervalMs = 5_000;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, LifecycleEntry> Entries =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PendingRemovals =
        new(StringComparer.OrdinalIgnoreCase);
    // Tick runs only on OMSI's callback thread. Reuse this bounded buffer
    // instead of allocating Entries.Values.ToArray() on every work slice.
    private static readonly List<LifecycleTickEntry> TickScratch =
        new(MaxEntries);

    private static int _resetRequested;
    private static int _randomBusControlProbeAttempted;
    private static long _internalCommandSequence;
    private static readonly Dictionary<string, string> LastVarProbeKey =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> LastVarPinKey =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> LastVisualPinKey =
        new(StringComparer.OrdinalIgnoreCase);

    public static int DesiredCount
    {
        get
        {
            lock (Sync)
            {
                return Entries.Count;
            }
        }
    }

    public static string Summary
    {
        get
        {
            lock (Sync)
            {
                var active = 0;
                var pending = 0;
                foreach (var entry in Entries.Values)
                {
                    if (string.Equals(entry.State, "active", StringComparison.Ordinal))
                    {
                        active++;
                    }
                    else
                    {
                        pending++;
                    }
                }

                return $"desired:{Entries.Count},active:{active},pending:{pending}";
            }
        }
    }

    public static void ObserveRemoteState(
        PluginBridgeMessage remoteState,
        PluginBridgeMessage? localState)
    {
        if (!ExperimentalVehicleCommandProcessor.ExperimentalWritesEnabled ||
            !IsCompatibleRemoteState(localState, remoteState) ||
            string.IsNullOrWhiteSpace(remoteState.PlayerId))
        {
            return;
        }

        var instanceId = remoteState.PlayerId.Trim();
        var explicitlyAdmitted =
            !string.IsNullOrWhiteSpace(remoteState.VehicleInstanceId) &&
            string.Equals(
                remoteState.VehicleInstanceId.Trim(),
                instanceId,
                StringComparison.OrdinalIgnoreCase);
        if (!explicitlyAdmitted)
        {
            // Raw service telemetry remains useful to RemoteVehicleRegistry,
            // but only a state explicitly admitted by the room-bound openOMSI
            // v6 path may touch native OMSI lifecycle state. SignalR sidecar
            // frames never receive VehicleInstanceId and cannot move buses.
            return;
        }

        var normalized = NormalizeSpawn(remoteState with
        {
            Type = PluginBridgeProtocol.SpawnRemoteVehicle,
            CommandId = NextInternalCommandId(instanceId),
            VehicleInstanceId = instanceId
        });
        var now = Environment.TickCount64;

        lock (Sync)
        {
            // The room-bound openOMSI v6 stream is the admission authority for
            // physical players. The plugin still enforces a usable native
            // target and the bounded player count before creating a lifecycle
            // entry. Raw RemoteVehicleState sidecar messages never carry
            // VehicleInstanceId and therefore cannot auto-admit themselves.
            if (PendingRemovals.Contains(instanceId))
            {
                return;
            }

            if (!Entries.TryGetValue(instanceId, out var entry))
            {
                if (!HasUsablePhysicalTarget(normalized) ||
                    Entries.Count >= MaxEntries)
                {
                    return;
                }

                entry = new LifecycleEntry(instanceId, normalized, now);
                Entries.Add(instanceId, entry);
                PluginLogWriter.Enqueue(
                    $"physical-lifecycle stream-admit id={instanceId} vehicle={normalized.VehiclePath ?? "-"}");
            }

            // RemoteVehicleState is a high-rate mirror of the server payload.
            // Its legacy GridX/GridY fields belong to navigation and must never
            // replace the Kachel-coherent physical grid that the desktop
            // coordinator already admitted. Prefer explicit PhysicalGridX/Y;
            // when an older payload omits them, preserve the previously
            // approved physical grid and articulated section poses.
            var physicalGridX =
                normalized.PhysicalGridX ??
                entry.DesiredSpawn.GridX;
            var physicalGridY =
                normalized.PhysicalGridY ??
                entry.DesiredSpawn.GridY;
            var physicalSections =
                normalized.RearSections ??
                entry.DesiredSpawn.RearSections;
            var receiverVehiclePath =
                !string.IsNullOrWhiteSpace(entry.DesiredSpawn.VehiclePath)
                    ? entry.DesiredSpawn.VehiclePath
                    : normalized.VehiclePath;

            entry.DesiredSpawn = normalized with
            {
                // VehiclePath on the admitted command was resolved locally by
                // fingerprint. Never replace it with the sender's installation
                // path from the raw telemetry feed, or a later materialization
                // retry can fail on otherwise identical bus content.
                VehiclePath = receiverVehiclePath,
                GridX = physicalGridX,
                GridY = physicalGridY,
                MapTileIndex = null,
                RearSections = physicalSections
            };
            normalized = entry.DesiredSpawn;
            entry.LastIntentTickMs = now;
            entry.ObservedTargetCount++;
            entry.TargetRateWindowStartedTickMs =
                entry.TargetRateWindowStartedTickMs <= 0
                    ? now
                    : entry.TargetRateWindowStartedTickMs;

            var sourceTimestamp = normalized.TimestampUnixMilliseconds;
            var targetChanged =
                sourceTimestamp is null ||
                entry.LastAppliedSourceTimestampMs != sourceTimestamp;
            if (targetChanged ||
                !string.Equals(entry.State, "active", StringComparison.Ordinal))
            {
                entry.NextAttemptTickMs =
                    Math.Min(entry.NextAttemptTickMs, now);
            }
        }
    }

    public static void RequestRemoteRemoval(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var normalized = instanceId.Trim();
        lock (Sync)
        {
            Entries.Remove(normalized);
            PendingRemovals.Add(normalized);
        }
    }

    public static void RequestClearRemoteVehicles()
    {
        lock (Sync)
        {
            foreach (var instanceId in Entries.Keys)
            {
                PendingRemovals.Add(instanceId);
            }

            Entries.Clear();
        }
    }

    public static void ObserveCommand(PluginBridgeMessage command)
    {
        if (!IsRemoteLifecycleCommand(command.Type) ||
            !TryGetInstanceId(command, out var instanceId))
        {
            return;
        }

        if (string.Equals(
                command.Type,
                PluginBridgeProtocol.DespawnRemoteVehicle,
                StringComparison.Ordinal))
        {
            lock (Sync)
            {
                Entries.Remove(instanceId);
            }
            return;
        }

        if (!HasUsablePhysicalTarget(command))
        {
            return;
        }

        var now = Environment.TickCount64;
        lock (Sync)
        {
            PendingRemovals.Remove(instanceId);

            var normalized = NormalizeSpawn(command);
            if (!Entries.TryGetValue(instanceId, out var entry))
            {
                if (Entries.Count >= MaxEntries)
                {
                    return;
                }

                entry = new LifecycleEntry(instanceId, normalized, now);
                Entries.Add(instanceId, entry);
            }
            else
            {
                entry.DesiredSpawn = normalized;
                entry.LastIntentTickMs = now;
            }

            var sourceTimestamp = normalized.TimestampUnixMilliseconds;
            var targetChanged =
                sourceTimestamp is null ||
                entry.LastAppliedSourceTimestampMs != sourceTimestamp;
            if (targetChanged ||
                !string.Equals(entry.State, "active", StringComparison.Ordinal))
            {
                entry.NextAttemptTickMs = Math.Min(entry.NextAttemptTickMs, now);
            }
        }
    }

    public static void ObserveResult(
        PluginBridgeMessage command,
        PluginBridgeMessage result)
    {
        if (!IsRemoteLifecycleCommand(command.Type) ||
            !TryGetInstanceId(command, out var instanceId) ||
            string.Equals(
                command.Type,
                PluginBridgeProtocol.DespawnRemoteVehicle,
                StringComparison.Ordinal))
        {
            return;
        }

        var now = Environment.TickCount64;
        lock (Sync)
        {
            if (!Entries.TryGetValue(instanceId, out var entry))
            {
                return;
            }

            entry.LastErrorCode = result.ErrorCode;
            entry.LastErrorMessage = result.ErrorMessage;

            if (result.Success == true)
            {
                var previousAppliedSourceTimestamp =
                    entry.LastAppliedSourceTimestampMs;
                var appliedSourceTimestamp =
                    command.TimestampUnixMilliseconds ??
                    entry.DesiredSpawn.TimestampUnixMilliseconds;

                entry.State = "active";
                entry.NextAttemptTickMs = now;
                entry.LastAppliedSourceTimestampMs =
                    appliedSourceTimestamp;
                entry.LastLoggedErrorCode = null;

                if (string.Equals(
                        command.Type,
                        PluginBridgeProtocol.UpdateRemoteVehicle,
                        StringComparison.Ordinal) &&
                    (appliedSourceTimestamp is null ||
                     previousAppliedSourceTimestamp != appliedSourceTimestamp))
                {
                    entry.AppliedTargetCount++;
                }

                if (entry.TargetRateWindowStartedTickMs > 0 &&
                    now - entry.TargetRateWindowStartedTickMs >=
                        TargetRateLogIntervalMs)
                {
                    var elapsedSeconds =
                        Math.Max(
                            0.001d,
                            (now - entry.TargetRateWindowStartedTickMs) /
                            1000d);
                    var observedHz =
                        entry.ObservedTargetCount / elapsedSeconds;
                    var appliedHz =
                        entry.AppliedTargetCount / elapsedSeconds;
                    PluginLogWriter.Enqueue(
                        $"physical-target-rate id={entry.InstanceId} " +
                        $"observedHz={observedHz:F1} appliedHz={appliedHz:F1} " +
                        $"observed={entry.ObservedTargetCount} applied={entry.AppliedTargetCount} " +
                        $"state={entry.State}");
                    entry.TargetRateWindowStartedTickMs = now;
                    entry.ObservedTargetCount = 0;
                    entry.AppliedTargetCount = 0;
                }

                return;
            }

            if (string.Equals(
                    result.ErrorCode,
                    "spawn-model-pending",
                    StringComparison.Ordinal))
            {
                entry.State = "materializing";
                entry.NextAttemptTickMs = now + MaterializationRetryMs;
                return;
            }

            entry.State = "retry";
            entry.NextAttemptTickMs = now + GetRetryDelay(result.ErrorCode);
        }

        if (string.Equals(
                result.ErrorCode,
                "spawn-pointer-unresolved",
                StringComparison.Ordinal) &&
            instanceId.StartsWith(
                "sim-",
                StringComparison.OrdinalIgnoreCase) &&
            Interlocked.CompareExchange(
                ref _randomBusControlProbeAttempted,
                1,
                0) == 0)
        {
            RunRandomBusControlProbe(instanceId);
        }
    }

    public static void RequestReset() =>
        Interlocked.Exchange(ref _resetRequested, 1);

    public static void ClearManagedState()
    {
        lock (Sync)
        {
            Entries.Clear();
            PendingRemovals.Clear();
            LastVarProbeKey.Clear();
            LastVarPinKey.Clear();
            LastVisualPinKey.Clear();
        }

        Interlocked.Exchange(ref _resetRequested, 0);
        Interlocked.Exchange(ref _randomBusControlProbeAttempted, 0);
    }

    /// <summary>
    /// Must run only from OMSI's callback thread.
    /// </summary>
    public static void Tick()
    {
        if (Interlocked.Exchange(ref _resetRequested, 0) != 0)
        {
            PhysicalVehicleBackend.MarkAllOwnedVehiclesForRemoval();
            ClearManagedState();
            PluginLogWriter.Enqueue("physical-lifecycle reset");
            return;
        }

        var now = Environment.TickCount64;

        string? pendingRemoval = null;
        lock (Sync)
        {
            if (PendingRemovals.Count > 0)
            {
                pendingRemoval = PendingRemovals.First();
                PendingRemovals.Remove(pendingRemoval);
            }
        }

        if (pendingRemoval is not null)
        {
            DespawnOwnedInstance(pendingRemoval);
            return;
        }

        lock (Sync)
        {
            TickScratch.Clear();
            foreach (var entry in Entries.Values)
            {
                var desiredTimestamp =
                    entry.DesiredSpawn.TimestampUnixMilliseconds;
                TickScratch.Add(new LifecycleTickEntry(
                    entry.InstanceId,
                    entry.LastIntentTickMs,
                    entry.NextAttemptTickMs,
                    entry.State,
                    desiredTimestamp is null ||
                    entry.LastAppliedSourceTimestampMs != desiredTimestamp));
            }
        }

        var spawnAttempts = 0;
        foreach (var entry in TickScratch)
        {
            if (now - entry.LastIntentTickMs > StaleIntentAfterMs)
            {
                RemoveStaleEntry(entry.InstanceId, now);
                continue;
            }

            if (PhysicalVehicleInstanceRegistry.TryGet(
                    entry.InstanceId,
                    out var instance))
            {
                var stalePart = instance
                    .GetOwnedVehiclePointers()
                    .FirstOrDefault(pointer =>
                        OmsiNativeInterop.IsRoadVehiclePointer(pointer) != 1);
                if (stalePart != 0)
                {
                    PhysicalVehicleMotionController.Remove(entry.InstanceId);
                    PhysicalVehicleInstanceRegistry.TryRemove(
                        entry.InstanceId,
                        out _);
                    SetRetry(
                        entry.InstanceId,
                        now,
                        "vehicle-consist-pointer-stale",
                        100);
                    PluginLogWriter.Enqueue(
                        $"physical-lifecycle stale-part id={entry.InstanceId} pointer=0x{stalePart:X8} parts={instance.PartCount}");
                }
                else if (string.Equals(
                             entry.State,
                             "active",
                             StringComparison.Ordinal))
                {
                    ProbeRemoteScriptVarBounds(entry.InstanceId, instance);
                    PinRemoteVisualSyncVars(entry.InstanceId, instance);
                    PinRemoteScriptVars(entry.InstanceId, instance);
                    if (now < entry.NextAttemptTickMs ||
                        !entry.HasPendingTargetUpdate ||
                        !TryBuildInternalUpdate(
                            entry.InstanceId,
                            now,
                            out var update))
                    {
                        continue;
                    }
                    var updateResult = PhysicalVehicleBackend.Execute(update);
                    ObserveResult(update, updateResult);
                    if (updateResult.Success != true)
                    {
                        LogTransition(entry.InstanceId, updateResult);
                    }
                    continue;
                }
            }

            // Spawning remains deliberately serialized because MakeVehicle can
            // allocate/materialize a full OMSI consist. Active network target
            // updates are lightweight buffer/visual-state feeds and must not
            // share this one-spawn-per-callback budget; otherwise N remote
            // players divide the effective state rate by N.
            if (spawnAttempts >= 1 ||
                now < entry.NextAttemptTickMs ||
                !TryBuildInternalSpawn(
                    entry.InstanceId,
                    now,
                    out var command))
            {
                continue;
            }
            var result = PhysicalVehicleBackend.Execute(command);
            ObserveResult(command, result);
            LogTransition(entry.InstanceId, result);
            spawnAttempts++;
        }

        TickScratch.Clear();
    }

    private static void RemoveStaleEntry(
        string instanceId,
        long now)
    {
        var removed = false;
        lock (Sync)
        {
            if (!Entries.TryGetValue(instanceId, out var current) ||
                now - current.LastIntentTickMs <= StaleIntentAfterMs)
            {
                return;
            }

            Entries.Remove(instanceId);
            PendingRemovals.Add(instanceId);
            removed = true;
        }

        if (removed)
        {
            PluginLogWriter.Enqueue(
                $"physical-lifecycle stale-remove id={instanceId}");
        }
    }

    private static void DespawnOwnedInstance(string instanceId)
    {
        if (!PhysicalVehicleInstanceRegistry.TryGet(instanceId, out _))
        {
            PhysicalVehicleMotionController.Remove(instanceId);
            return;
        }

        var despawn = new PluginBridgeMessage(
            PluginBridgeProtocol.DespawnRemoteVehicle,
            PluginBridgeProtocol.Version,
            PlayerId: instanceId,
            CommandId: NextInternalCommandId(instanceId),
            VehicleInstanceId: instanceId);
        var result = PhysicalVehicleBackend.Execute(despawn);
        PluginLogWriter.Enqueue(
            $"physical-lifecycle remove id={instanceId} success={result.Success} error={result.ErrorCode ?? "-"}");
    }

    private static bool TryBuildInternalSpawn(
        string instanceId,
        long now,
        out PluginBridgeMessage command)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(instanceId, out var entry) ||
                now < entry.NextAttemptTickMs)
            {
                command = null!;
                return false;
            }

            command = entry.DesiredSpawn with
            {
                Type = PluginBridgeProtocol.SpawnRemoteVehicle,
                CommandId = NextInternalCommandId(entry.InstanceId),
                VehicleInstanceId = entry.InstanceId
            };
            return true;
        }
    }

    private static bool TryBuildInternalUpdate(
        string instanceId,
        long now,
        out PluginBridgeMessage command)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(instanceId, out var entry) ||
                !string.Equals(
                    entry.State,
                    "active",
                    StringComparison.Ordinal) ||
                now < entry.NextAttemptTickMs)
            {
                command = null!;
                return false;
            }

            var desiredTimestamp =
                entry.DesiredSpawn.TimestampUnixMilliseconds;
            if (desiredTimestamp is not null &&
                entry.LastAppliedSourceTimestampMs == desiredTimestamp)
            {
                command = null!;
                return false;
            }

            command = entry.DesiredSpawn with
            {
                Type = PluginBridgeProtocol.UpdateRemoteVehicle,
                CommandId = NextInternalCommandId(entry.InstanceId),
                VehicleInstanceId = entry.InstanceId
            };
            return true;
        }
    }

    private static PluginBridgeMessage NormalizeSpawn(
        PluginBridgeMessage command) =>
        command with
        {
            Type = PluginBridgeProtocol.SpawnRemoteVehicle,
            VehicleInstanceId =
                command.VehicleInstanceId ??
                command.PlayerId
        };

    private static bool HasUsablePhysicalTarget(PluginBridgeMessage command) =>
        !string.IsNullOrWhiteSpace(command.VehiclePath) &&
        command.LocalX is double x && double.IsFinite(x) &&
        command.LocalY is double y && double.IsFinite(y) &&
        command.LocalZ is double z && double.IsFinite(z) &&
        command.RotationX is double qx && double.IsFinite(qx) &&
        command.RotationY is double qy && double.IsFinite(qy) &&
        command.RotationZ is double qz && double.IsFinite(qz) &&
        command.RotationW is double qw && double.IsFinite(qw);

    private static bool IsCompatibleRemoteState(
        PluginBridgeMessage? localState,
        PluginBridgeMessage remoteState)
    {
        if (localState?.IsInGame != true ||
            remoteState.IsInGame != true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(localState.MapCompatibilityId) &&
            !string.IsNullOrWhiteSpace(remoteState.MapCompatibilityId))
        {
            return string.Equals(
                localState.MapCompatibilityId,
                remoteState.MapCompatibilityId,
                StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(localState.MapName) &&
               !string.IsNullOrWhiteSpace(remoteState.MapName) &&
               string.Equals(
                   localState.MapName,
                   remoteState.MapName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemoteLifecycleCommand(string type) =>
        string.Equals(
            type,
            PluginBridgeProtocol.SpawnRemoteVehicle,
            StringComparison.Ordinal) ||
        string.Equals(
            type,
            PluginBridgeProtocol.UpdateRemoteVehicle,
            StringComparison.Ordinal) ||
        string.Equals(
            type,
            PluginBridgeProtocol.DespawnRemoteVehicle,
            StringComparison.Ordinal);

    private static bool TryGetInstanceId(
        PluginBridgeMessage command,
        out string instanceId)
    {
        instanceId = (
            command.VehicleInstanceId ??
            command.PlayerId ??
            string.Empty).Trim();
        return instanceId.Length is > 0 and <= 128;
    }

    private static void ProbeRemoteScriptVarBounds(
        string instanceId,
        PhysicalVehicleInstance instance)
    {
        if (!RemoteVehicleVarsRegistry.TryGet(
                instanceId,
                out var snapshot) ||
            snapshot.VarTableHash is not uint varTableHash ||
            snapshot.Floats.Count == 0)
        {
            return;
        }

        var count =
            OmsiNativeInterop.TryGetRoadVehiclePublicVarCount(
                instance.VehiclePointer);
        if (count < 0)
        {
            return;
        }

        var maxId = snapshot.Floats.Keys.Max();
        var available = snapshot.Floats.Keys.Count(id => id < count);
        var compatible = available == snapshot.Floats.Count;
        var key =
            $"{varTableHash:X8}:{count}:{maxId}:{available}:{compatible}";
        lock (Sync)
        {
            if (LastVarProbeKey.TryGetValue(instanceId, out var previous) &&
                string.Equals(previous, key, StringComparison.Ordinal))
            {
                return;
            }

            LastVarProbeKey[instanceId] = key;
        }

        PluginLogWriter.Enqueue(
            $"physical-vars-probe id={instanceId} " +
            $"table={varTableHash:X8} publicVars={count} " +
            $"maxRemoteId={maxId} availableVars={available} " +
            $"totalVars={snapshot.Floats.Count} compatible={compatible}");
    }

    private static void PinRemoteScriptVars(
        string instanceId,
        PhysicalVehicleInstance instance)
    {
        if (!RemoteVehicleVarsRegistry.TryGet(
                instanceId,
                out var snapshot) ||
            snapshot.VarTableHash is not uint varTableHash ||
            snapshot.Floats.Count == 0)
        {
            return;
        }

        var smoothIds = snapshot.LampIds
            .Concat(snapshot.SwitchIds)
            .Concat(snapshot.ValueIds)
            .Concat(snapshot.DoorIds)
            .ToHashSet();
        var scriptFloats = snapshot.Floats
            .Where(pair => !smoothIds.Contains(pair.Key))
            .Take(256)
            .ToArray();

        var count =
            OmsiNativeInterop.TryGetRoadVehiclePublicVarCount(
                instance.VehiclePointer);
        // Some vehicles expose fewer live slots while their script initializes.
        // Only apply verified in-bounds PublicVars; one unavailable slot must
        // not prevent valid indicators and controls from synchronizing.
        if (count <= 0)
        {
            return;
        }

        var availableFloats = scriptFloats
            .Where(pair => pair.Key < count)
            .ToArray();
        var skipped = scriptFloats.Length - availableFloats.Length;
        if (availableFloats.Length == 0)
        {
            return;
        }

        var applied = 0;
        foreach (var pair in availableFloats)
        {
            if (!float.IsFinite(pair.Value) ||
                pair.Key >= count ||
                !OmsiNativeInterop.TryWriteRoadVehiclePublicVar(
                    instance.VehiclePointer,
                    pair.Key,
                    pair.Value))
            {
                var failedKey =
                    $"{varTableHash:X8}:fail:{pair.Key}:{count}";
                lock (Sync)
                {
                    if (!LastVarPinKey.TryGetValue(
                            instanceId,
                            out var previous) ||
                        !string.Equals(
                            previous,
                            failedKey,
                            StringComparison.Ordinal))
                    {
                        LastVarPinKey[instanceId] = failedKey;
                        PluginLogWriter.Enqueue(
                            $"physical-vars-pin id={instanceId} " +
                            $"table={varTableHash:X8} status=write-failed " +
                            $"var={pair.Key} publicVars={count}");
                    }
                }
                return;
            }

            applied++;
        }

        // Remote StringVars remain receive/read-only for now. They are kept
        // in RemoteVehicleVarsRegistry so the protocol path can be observed,
        // but are deliberately not written into OMSI's Delphi UnicodeString
        // slots until a verified simulator/RTL assignment routine owns the
        // allocation and refcount lifecycle.
        var receivedStrings = snapshot.Strings.Count;

        var successKey =
            $"{varTableHash:X8}:ok:{count}:{applied}:skipped={skipped}:strings-readonly={receivedStrings}";
        lock (Sync)
        {
            if (LastVarPinKey.TryGetValue(
                    instanceId,
                    out var previous) &&
                string.Equals(
                    previous,
                    successKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            LastVarPinKey[instanceId] = successKey;
        }

        PluginLogWriter.Enqueue(
            $"physical-vars-pin id={instanceId} " +
            $"table={varTableHash:X8} status=active " +
            $"vars={applied} skippedOutOfRange={skipped} publicVars={count} " +
            $"stringsReceived={receivedStrings} stringMode=read-only");
    }

    private static void PinRemoteVisualSyncVars(
        string instanceId,
        PhysicalVehicleInstance instance)
    {
        if (!RemoteVehicleVarsRegistry.TryGet(
                instanceId,
                out var snapshot) ||
            snapshot.SyncTableHash is not uint syncTableHash)
        {
            return;
        }

        var total =
            snapshot.Lamps.Length +
            snapshot.Switches.Length +
            snapshot.Values.Length +
            snapshot.Doors.Length;
        if (total == 0)
        {
            return;
        }

        var count =
            OmsiNativeInterop.TryGetRoadVehiclePublicVarCount(
                instance.VehiclePointer);
        if (count <= 0)
        {
            return;
        }

        bool Apply(
            IReadOnlyList<ushort> ids,
            IReadOnlyList<float> values,
            string kind,
            ref int applied)
        {
            if (ids.Count != values.Count)
            {
                return false;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var value = values[i];
                if (id >= count ||
                    !float.IsFinite(value) ||
                    !OmsiNativeInterop.TryWriteRoadVehiclePublicVar(
                        instance.VehiclePointer,
                        id,
                        value))
                {
                    var failedKey =
                        $"{syncTableHash:X8}:{kind}:fail:{id}:{count}";
                    lock (Sync)
                    {
                        if (!LastVisualPinKey.TryGetValue(
                                instanceId,
                                out var previous) ||
                            !string.Equals(
                                previous,
                                failedKey,
                                StringComparison.Ordinal))
                        {
                            LastVisualPinKey[instanceId] =
                                failedKey;
                            PluginLogWriter.Enqueue(
                                $"physical-visual-pin id={instanceId} " +
                                $"table={syncTableHash:X8} " +
                                $"status=write-failed kind={kind} " +
                                $"var={id} publicVars={count}");
                        }
                    }

                    return false;
                }

                applied++;
            }

            return true;
        }

        var applied = 0;
        if (!Apply(
                snapshot.LampIds,
                snapshot.Lamps,
                "lamp",
                ref applied) ||
            !Apply(
                snapshot.SwitchIds,
                snapshot.Switches,
                "switch",
                ref applied) ||
            !Apply(
                snapshot.ValueIds,
                snapshot.Values,
                "value",
                ref applied) ||
            !Apply(
                snapshot.DoorIds,
                snapshot.Doors,
                "door",
                ref applied))
        {
            return;
        }

        var successKey =
            $"{syncTableHash:X8}:ok:{count}:{applied}";
        lock (Sync)
        {
            if (LastVisualPinKey.TryGetValue(
                    instanceId,
                    out var previous) &&
                string.Equals(
                    previous,
                    successKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            LastVisualPinKey[instanceId] =
                successKey;
        }

        PluginLogWriter.Enqueue(
            $"physical-visual-pin id={instanceId} " +
            $"table={syncTableHash:X8} status=active " +
            $"vars={applied} publicVars={count}");
    }

    private static void RunRandomBusControlProbe(string instanceId)
    {
        var invoked = OmsiNativeInterop.TryRunPlaceRandomBusProbe(
            out var probe);
        PluginLogWriter.Enqueue(
            $"physical-control-probe id={instanceId} " +
            $"invoked={invoked} raw={probe.RawReturn} " +
            $"before={probe.BeforeCount} after={probe.AfterCount} " +
            $"delta={probe.DeltaCount} detail={probe.Detail}");
    }

    private static long GetRetryDelay(string? errorCode) =>
        errorCode switch
        {
            "spawn-model-pending" => MaterializationRetryMs,
            "tile-unavailable" => TransientRetryMs,
            "omsi-runtime-unavailable" => TransientRetryMs,
            "roadvehicles-unavailable" => TransientRetryMs,
            "makevehicle-lock-failed" => TransientRetryMs,
            "temp-list-failed" => TransientRetryMs,
            "spawn-pointer-unresolved" => TransientRetryMs,
            PluginBridgeProtocol.ErrorMotionWorldOriginUnavailable => TransientRetryMs,
            _ => SlowRetryMs
        };

    private static void SetRetry(
        string instanceId,
        long now,
        string errorCode,
        long delayMs)
    {
        lock (Sync)
        {
            if (!Entries.TryGetValue(instanceId, out var entry))
            {
                return;
            }

            entry.State = "retry";
            entry.LastErrorCode = errorCode;
            entry.NextAttemptTickMs = now + delayMs;
        }
    }

    private static void LogTransition(
        string instanceId,
        PluginBridgeMessage result)
    {
        string? logLine = null;
        lock (Sync)
        {
            if (!Entries.TryGetValue(instanceId, out var entry))
            {
                return;
            }

            var code = result.Success == true
                ? "success"
                : result.ErrorCode ?? "unknown";
            if (string.Equals(
                    entry.LastLoggedErrorCode,
                    code,
                    StringComparison.Ordinal))
            {
                return;
            }

            entry.LastLoggedErrorCode = code;
            var detail = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "-"
                : result.ErrorMessage!
                    .Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Trim();
            if (detail.Length > 280)
            {
                detail = detail[..280];
            }

            logLine =
                $"physical-lifecycle id={instanceId} state={entry.State} " +
                $"result={code} detail={detail}";
        }

        if (logLine is not null)
        {
            PluginLogWriter.Enqueue(logLine);
        }
    }

    private static string NextInternalCommandId(string instanceId)
    {
        var sequence = Interlocked.Increment(ref _internalCommandSequence);
        return $"plugin-lifecycle-{sequence:x}-{instanceId}";
    }

    private readonly record struct LifecycleTickEntry(
        string InstanceId,
        long LastIntentTickMs,
        long NextAttemptTickMs,
        string State,
        bool HasPendingTargetUpdate);

    private sealed class LifecycleEntry
    {
        public LifecycleEntry(
            string instanceId,
            PluginBridgeMessage desiredSpawn,
            long now)
        {
            InstanceId = instanceId;
            DesiredSpawn = desiredSpawn;
            LastIntentTickMs = now;
            NextAttemptTickMs = now;
        }

        public string InstanceId { get; }
        public PluginBridgeMessage DesiredSpawn { get; set; }
        public long LastIntentTickMs { get; set; }
        public long NextAttemptTickMs { get; set; }
        public string State { get; set; } = "requested";
        public string? LastErrorCode { get; set; }
        public string? LastErrorMessage { get; set; }
        public string? LastLoggedErrorCode { get; set; }
        public long? LastAppliedSourceTimestampMs { get; set; }
        public long TargetRateWindowStartedTickMs { get; set; }
        public int ObservedTargetCount { get; set; }
        public int AppliedTargetCount { get; set; }
    }
}
