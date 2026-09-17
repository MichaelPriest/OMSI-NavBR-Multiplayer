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

    public static int Drain(
        int maxCommands,
        Action<PluginBridgeMessage> resultSink)
    {
        if (maxCommands <= 0)
        {
            return 0;
        }

        var processed = 0;
        while (processed < maxCommands)
        {
            PluginBridgeMessage? command;
            lock (Sync)
            {
                if (!Pending.TryDequeue(out command))
                {
                    break;
                }
            }

            var result = RoleplayCharacterCommandProcessor.IsCharacterCommandType(command.Type)
                ? RoleplayCharacterCommandProcessor.ProcessOnOmsiThread(command)
                : ExperimentalVehicleCommandProcessor.ProcessOnOmsiThread(command);
            resultSink(result);
            processed++;
        }

        return processed;
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
