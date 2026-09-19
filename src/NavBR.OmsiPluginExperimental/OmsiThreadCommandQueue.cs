using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Handoff between the named-pipe worker and OMSI's plugin callback thread.
/// Raw simulator calls must never execute directly on the bridge worker.
/// </summary>
internal static class OmsiThreadCommandQueue
{
    private const int MaxPendingCommands = 64;
    private static readonly object Sync = new();
    private static readonly Queue<PluginBridgeMessage> Pending = new();

    public static int Count
    {
        get
        {
            lock (Sync)
            {
                return Pending.Count;
            }
        }
    }

    public static bool TryEnqueue(PluginBridgeMessage command)
    {
        lock (Sync)
        {
            if (Pending.Count >= MaxPendingCommands)
            {
                return false;
            }

            Pending.Enqueue(command);
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

        // Preserve FIFO semantics for one arbitrary command per OMSI frame.
        // This bounds expensive spawn/acquire work to at most one command.
        if (TryDequeueFirst(out var first))
        {
            Process(first, resultSink);
            processed++;
        }

        // Movement updates are intentionally lightweight. Consume a few extra
        // targets in the same frame so rooms with several physical buses do not
        // become limited to one network update per rendered frame.
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
            if (Pending.TryDequeue(out var candidate))
            {
                command = candidate;
                return true;
            }
        }

        command = null!;
        return false;
    }

    private static bool TryDequeueLightweightUpdate(out PluginBridgeMessage command)
    {
        command = null!;
        lock (Sync)
        {
            var count = Pending.Count;
            if (count == 0)
            {
                return false;
            }

            List<PluginBridgeMessage>? deferred = null;
            PluginBridgeMessage? selected = null;
            for (var index = 0; index < count; index++)
            {
                var candidate = Pending.Dequeue();
                if (selected is null && IsLightweightUpdate(candidate.Type))
                {
                    selected = candidate;
                    continue;
                }

                (deferred ??= new List<PluginBridgeMessage>()).Add(candidate);
            }

            if (deferred is not null)
            {
                foreach (var candidate in deferred)
                {
                    Pending.Enqueue(candidate);
                }
            }

            if (selected is null)
            {
                return false;
            }

            command = selected;
            return true;
        }
    }

    private static bool IsLightweightUpdate(string type) =>
        string.Equals(type, PluginBridgeProtocol.UpdateRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateGhostVehicle, StringComparison.Ordinal);

    private static void Process(
        PluginBridgeMessage command,
        Action<PluginBridgeMessage> resultSink)
    {
        var result = RoleplayCharacterCommandProcessor.IsCharacterCommandType(command.Type)
            ? RoleplayCharacterCommandProcessor.ProcessOnOmsiThread(command)
            : ExperimentalVehicleCommandProcessor.ProcessOnOmsiThread(command);
        resultSink(result);
    }

    public static int Clear()
    {
        lock (Sync)
        {
            var removed = Pending.Count;
            Pending.Clear();
            return removed;
        }
    }
}
