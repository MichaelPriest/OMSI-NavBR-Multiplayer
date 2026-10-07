using System.Collections.Concurrent;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class RemoteVehicleVarsRegistry
{
    private const int MaxFloatsPerMessage = 256;
    private const int MaxStringsPerMessage = 64;
    private const int MaxStringLength = 255;

    private static readonly ConcurrentDictionary<string, RemoteVehicleVarsSnapshot> States =
        new(StringComparer.OrdinalIgnoreCase);

    internal static bool TryApply(
        PluginBridgeMessage message,
        out string? rejectionReason)
    {
        rejectionReason = null;
        var playerId = message.PlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(playerId) ||
            message.SyncTableHash is not uint tableHash ||
            tableHash == 0)
        {
            rejectionReason = "missing-player-or-table";
            return false;
        }

        var floatIds = message.VariableIndices ?? [];
        var floatValues = message.VariableValues ?? [];
        var stringIds = message.StringVariableIndices ?? [];
        var stringValues = message.StringVariableValues ?? [];

        if (floatIds.Length != floatValues.Length ||
            stringIds.Length != stringValues.Length ||
            floatIds.Length > MaxFloatsPerMessage ||
            stringIds.Length > MaxStringsPerMessage)
        {
            rejectionReason = "invalid-vars-shape";
            return false;
        }

        if (floatValues.Any(value => !float.IsFinite(value)) ||
            stringValues.Any(value => (value?.Length ?? 0) > MaxStringLength))
        {
            rejectionReason = "invalid-vars-value";
            return false;
        }

        States.AddOrUpdate(
            playerId,
            _ => RemoteVehicleVarsSnapshot.Empty(tableHash).Apply(
                floatIds,
                floatValues,
                stringIds,
                stringValues),
            (_, current) =>
            {
                if (current.TableHash != tableHash)
                {
                    current = RemoteVehicleVarsSnapshot.Empty(tableHash);
                }

                return current.Apply(
                    floatIds,
                    floatValues,
                    stringIds,
                    stringValues);
            });

        return true;
    }

    internal static bool TryApplyVisualState(
        PluginBridgeMessage message,
        out string? rejectionReason)
    {
        rejectionReason = null;
        var playerId = message.PlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(playerId) ||
            message.SyncTableHash is not uint tableHash ||
            tableHash == 0)
        {
            rejectionReason = "missing-player-or-table";
            return false;
        }

        var lamps = message.SyncLamps ?? [];
        var switches = message.SyncSwitches ?? [];
        var values = message.SyncValues ?? [];
        if (lamps.Length > 127 ||
            switches.Length > 31 ||
            values.Length > 63 ||
            lamps.Any(value => !float.IsFinite(value)) ||
            switches.Any(value => !float.IsFinite(value)) ||
            values.Any(value => !float.IsFinite(value)))
        {
            rejectionReason = "invalid-visual-sync-shape";
            return false;
        }

        States.AddOrUpdate(
            playerId,
            _ => RemoteVehicleVarsSnapshot.Empty(tableHash) with
            {
                Lamps = lamps.ToArray(),
                Switches = switches.ToArray(),
                Values = values.ToArray(),
                UpdatedAtTick = Environment.TickCount64
            },
            (_, current) =>
            {
                if (current.TableHash != tableHash)
                {
                    current = RemoteVehicleVarsSnapshot.Empty(tableHash);
                }

                return current with
                {
                    Lamps = lamps.ToArray(),
                    Switches = switches.ToArray(),
                    Values = values.ToArray(),
                    UpdatedAtTick = Environment.TickCount64
                };
            });

        return true;
    }

    internal static bool TryGet(
        string? playerId,
        out RemoteVehicleVarsSnapshot snapshot)
    {
        snapshot = default!;
        return !string.IsNullOrWhiteSpace(playerId) &&
               States.TryGetValue(playerId, out snapshot);
    }

    internal static void Remove(string? playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId))
        {
            States.TryRemove(playerId, out _);
        }
    }

    internal static void Clear() => States.Clear();

    internal sealed record RemoteVehicleVarsSnapshot(
        uint TableHash,
        IReadOnlyDictionary<ushort, float> Floats,
        IReadOnlyDictionary<ushort, string> Strings,
        float[] Lamps,
        float[] Switches,
        float[] Values,
        long UpdatedAtTick)
    {
        internal static RemoteVehicleVarsSnapshot Empty(uint tableHash) =>
            new(
                tableHash,
                new Dictionary<ushort, float>(),
                new Dictionary<ushort, string>(),
                [],
                [],
                [],
                Environment.TickCount64);

        internal RemoteVehicleVarsSnapshot Apply(
            IReadOnlyList<ushort> floatIds,
            IReadOnlyList<float> floatValues,
            IReadOnlyList<ushort> stringIds,
            IReadOnlyList<string> stringValues)
        {
            var floats = new Dictionary<ushort, float>(Floats);
            for (var i = 0; i < floatIds.Count; i++)
            {
                floats[floatIds[i]] = floatValues[i];
            }

            var strings = new Dictionary<ushort, string>(Strings);
            for (var i = 0; i < stringIds.Count; i++)
            {
                strings[stringIds[i]] = stringValues[i] ?? string.Empty;
            }

            return new RemoteVehicleVarsSnapshot(
                TableHash,
                floats,
                strings,
                Lamps,
                Switches,
                Values,
                Environment.TickCount64);
        }
    }
}
