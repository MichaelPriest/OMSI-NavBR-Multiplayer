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
        var command = remoteState with
        {
            Type = PluginBridgeProtocol.SpawnRemoteVehicle,
            CommandId = NextInternalCommandId(instanceId),
            VehicleInstanceId = instanceId
        };
        ObserveCommand(command);
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
                entry.State = "active";
                entry.NextAttemptTickMs = now;
                entry.LastAppliedSourceTimestampMs =
                    command.TimestampUnixMilliseconds ??
                    entry.DesiredSpawn.TimestampUnixMilliseconds;
                entry.LastLoggedErrorCode = null;
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

        var nativeAttempts = 0;
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
                if (OmsiNativeInterop.IsRoadVehiclePointer(
                        instance.VehiclePointer) != 1)
                {
                    PhysicalVehicleMotionController.Remove(entry.InstanceId);
                    PhysicalVehicleInstanceRegistry.TryRemove(
                        entry.InstanceId,
                        out _);
                    SetRetry(entry.InstanceId, now, "vehicle-pointer-stale", 100);
                }
                else if (string.Equals(
                             entry.State,
                             "active",
                             StringComparison.Ordinal))
                {
                    if (nativeAttempts >= 1 ||
                        now < entry.NextAttemptTickMs ||
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
                    nativeAttempts++;
                    continue;
                }
            }

            if (nativeAttempts >= 1 ||
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
            nativeAttempts++;
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
    }
}
