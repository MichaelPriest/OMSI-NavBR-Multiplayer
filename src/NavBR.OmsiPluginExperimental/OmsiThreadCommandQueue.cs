using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Handoff between the named-pipe worker and OMSI's plugin callback thread.
/// Raw simulator calls must never execute directly on the bridge worker.
///
/// Superseding movement state is kept separately from event/lifecycle commands.
/// This avoids rebuilding and allocating the whole queue for every network pose
/// update while OMSI is rendering.
/// </summary>
internal static class OmsiThreadCommandQueue
{
    private const int MaxPendingCommands = 64;

    private static readonly object Sync = new();
    private static readonly Queue<PluginBridgeMessage> Lifecycle = new();
    private static readonly Queue<PluginBridgeMessage> Commands = new();
    private static readonly Queue<UpdateKey> UpdateOrder = new();
    private static readonly Dictionary<UpdateKey, PluginBridgeMessage> LatestUpdates =
        new(UpdateKeyComparer.Instance);
    private static int _publishedCount;

    public static int Count => Volatile.Read(ref _publishedCount);

    public static bool TryEnqueue(PluginBridgeMessage command)
    {
        lock (Sync)
        {
            // Network movement is state, not an event stream. Replacing the
            // latest state for the same target prevents backlog replay and now
            // does so without Queue.ToArray()/List allocations.
            if (IsLightweightUpdate(command.Type) &&
                TryGetTargetId(command, out var targetId))
            {
                var key = BuildUpdateKey(command.Type, targetId);
                if (LatestUpdates.ContainsKey(key))
                {
                    LatestUpdates[key] = command;
                    return true;
                }

                if (CountUnsafe() >= MaxPendingCommands)
                {
                    return false;
                }

                LatestUpdates.Add(key, command);
                UpdateOrder.Enqueue(key);
                PublishCountUnsafe();
                return true;
            }

            if (CountUnsafe() >= MaxPendingCommands)
            {
                return false;
            }

            if (IsLifecycleCommand(command.Type))
            {
                Lifecycle.Enqueue(command);
            }
            else
            {
                Commands.Enqueue(command);
            }

            PublishCountUnsafe();
            return true;
        }
    }

    public static int DrainFrame(
        int maxCommands,
        Action<PluginBridgeMessage> resultSink)
    {
        if (maxCommands <= 0)
        {
            return 0;
        }

        var processed = 0;

        // Exactly one event/lifecycle command is allowed to lead the slice.
        // Expensive spawn/acquire work therefore stays bounded even if several
        // requests arrive during a long simulator frame.
        if (TryDequeueFirst(out var first))
        {
            Process(first, resultSink);
            processed++;
        }

        // Pose updates are cheap and superseding. Process a few fresh targets
        // after the first command, within the governor's total slice budget.
        while (processed < maxCommands &&
               TryDequeueLightweightUpdate(out var update))
        {
            Process(update, resultSink);
            processed++;
        }

        return processed;
    }

    private static bool TryDequeueFirst(out PluginBridgeMessage command)
    {
        lock (Sync)
        {
            if (Lifecycle.TryDequeue(out command!))
            {
                PublishCountUnsafe();
                return true;
            }

            if (Commands.TryDequeue(out command!))
            {
                PublishCountUnsafe();
                return true;
            }

            return TryDequeueUpdateUnsafe(out command);
        }
    }

    private static bool TryDequeueLightweightUpdate(out PluginBridgeMessage command)
    {
        lock (Sync)
        {
            return TryDequeueUpdateUnsafe(out command);
        }
    }

    private static bool TryDequeueUpdateUnsafe(out PluginBridgeMessage command)
    {
        while (UpdateOrder.Count > 0)
        {
            var key = UpdateOrder.Dequeue();
            if (!LatestUpdates.Remove(key, out command!))
            {
                continue;
            }

            PublishCountUnsafe();
            return true;
        }

        command = null!;
        return false;
    }

    private static int CountUnsafe() =>
        Lifecycle.Count +
        Commands.Count +
        LatestUpdates.Count;

    private static void PublishCountUnsafe() =>
        Volatile.Write(ref _publishedCount, CountUnsafe());

    private static bool IsLightweightUpdate(string type) =>
        string.Equals(
            type,
            PluginBridgeProtocol.UpdateRemoteVehicle,
            StringComparison.Ordinal) ||
        string.Equals(
            type,
            PluginBridgeProtocol.UpdateGhostVehicle,
            StringComparison.Ordinal);

    private static bool IsLifecycleCommand(string type) =>
        string.Equals(
            type,
            PluginBridgeProtocol.SpawnRemoteVehicle,
            StringComparison.Ordinal) ||
        string.Equals(
            type,
            PluginBridgeProtocol.DespawnRemoteVehicle,
            StringComparison.Ordinal) ||
        string.Equals(
            type,
            PluginBridgeProtocol.SpawnGhostVehicle,
            StringComparison.Ordinal) ||
        string.Equals(
            type,
            PluginBridgeProtocol.DespawnGhostVehicle,
            StringComparison.Ordinal);

    private static bool TryGetTargetId(
        PluginBridgeMessage command,
        out string targetId)
    {
        targetId = (
            command.VehicleInstanceId ??
            command.PlayerId ??
            string.Empty).Trim();
        return targetId.Length is > 0 and <= 128;
    }

    private static UpdateKey BuildUpdateKey(
        string type,
        string targetId) =>
        new(type, targetId);

    private readonly record struct UpdateKey(
        string Type,
        string TargetId);

    private sealed class UpdateKeyComparer : IEqualityComparer<UpdateKey>
    {
        public static UpdateKeyComparer Instance { get; } = new();

        public bool Equals(UpdateKey left, UpdateKey right) =>
            string.Equals(
                left.Type,
                right.Type,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                left.TargetId,
                right.TargetId,
                StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(UpdateKey key)
        {
            unchecked
            {
                return
                    (StringComparer.OrdinalIgnoreCase.GetHashCode(key.Type) * 397) ^
                    StringComparer.OrdinalIgnoreCase.GetHashCode(key.TargetId);
            }
        }
    }

    private static void Process(
        PluginBridgeMessage command,
        Action<PluginBridgeMessage> resultSink)
    {
        var result = LocalVehicleCommandProcessor.IsCommandType(command.Type)
            ? LocalVehicleCommandProcessor.ProcessOnOmsiThread(command)
            : RoleplayCharacterCommandProcessor.IsCharacterCommandType(command.Type)
                ? RoleplayCharacterCommandProcessor.ProcessOnOmsiThread(command)
                : ExperimentalVehicleCommandProcessor.ProcessOnOmsiThread(command);
        resultSink(result);
    }

    public static int Clear()
    {
        lock (Sync)
        {
            var removed = CountUnsafe();
            Lifecycle.Clear();
            Commands.Clear();
            UpdateOrder.Clear();
            LatestUpdates.Clear();
            Volatile.Write(ref _publishedCount, 0);
            return removed;
        }
    }
}
