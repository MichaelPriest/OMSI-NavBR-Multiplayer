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
    private static readonly ConcurrentDictionary<string, string> AdmittedVehiclePaths =
        new(StringComparer.OrdinalIgnoreCase);

    internal static bool TryApply(
        PluginBridgeMessage message,
        out string? rejectionReason)
    {
        rejectionReason = null;
        var playerId = message.PlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(playerId) ||
            message.VarTableHash is not uint varTableHash ||
            varTableHash == 0)
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
            _ => (RemoteVehicleVarsSnapshot.Empty() with
            {
                VarTableHash = varTableHash
            }).Apply(
                floatIds,
                floatValues,
                stringIds,
                stringValues),
            (_, current) =>
            {
                if (current.VarTableHash != varTableHash)
                {
                    current = current with
                    {
                        VarTableHash = varTableHash,
                        Floats = new Dictionary<ushort, float>(),
                        Strings = new Dictionary<ushort, string>()
                    };
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
            message.SyncTableHash is not uint syncTableHash ||
            syncTableHash == 0)
        {
            rejectionReason = "missing-player-or-table";
            return false;
        }

        var lamps = message.SyncLamps ?? [];
        var switches = message.SyncSwitches ?? [];
        var values = message.SyncValues ?? [];
        var doors = message.SyncDoors ?? [];
        var lampIds = message.SyncLampVariableIndices ?? [];
        var switchIds = message.SyncSwitchVariableIndices ?? [];
        var valueIds = message.SyncValueVariableIndices ?? [];
        var doorIds = message.SyncDoorVariableIndices ?? [];
        if (lamps.Length > 127 ||
            switches.Length > 31 ||
            values.Length > 63 ||
            doors.Length > 7 ||
            lamps.Length != lampIds.Length ||
            switches.Length != switchIds.Length ||
            values.Length != valueIds.Length ||
            doors.Length != doorIds.Length ||
            lamps.Any(value => !float.IsFinite(value)) ||
            switches.Any(value => !float.IsFinite(value)) ||
            values.Any(value => !float.IsFinite(value)) ||
            doors.Any(value => !float.IsFinite(value)))
        {
            rejectionReason = "invalid-visual-sync-shape";
            return false;
        }

        States.AddOrUpdate(
            playerId,
            _ => RemoteVehicleVarsSnapshot.Empty() with
            {
                SyncTableHash = syncTableHash,
                Lamps = lamps.ToArray(),
                Switches = switches.ToArray(),
                Values = values.ToArray(),
                Doors = doors.ToArray(),
                LampIds = lampIds.ToArray(),
                SwitchIds = switchIds.ToArray(),
                ValueIds = valueIds.ToArray(),
                DoorIds = doorIds.ToArray(),
                UpdatedAtTick = Environment.TickCount64
            },
            (_, current) =>
            {
                if (current.SyncTableHash != syncTableHash)
                {
                    current = current with
                    {
                        SyncTableHash = syncTableHash,
                        Lamps = [],
                        Switches = [],
                        Values = [],
                        Doors = [],
                        LampIds = [],
                        SwitchIds = [],
                        ValueIds = [],
                        DoorIds = []
                    };
                }

                return current with
                {
                    Lamps = lamps.ToArray(),
                    Switches = switches.ToArray(),
                    Values = values.ToArray(),
                    Doors = doors.ToArray(),
                    LampIds = lampIds.ToArray(),
                    SwitchIds = switchIds.ToArray(),
                    ValueIds = valueIds.ToArray(),
                    DoorIds = doorIds.ToArray(),
                    UpdatedAtTick = Environment.TickCount64
                };
            });

        return true;
    }

    internal static void ClearVisualState(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        States.AddOrUpdate(
            playerId,
            _ => RemoteVehicleVarsSnapshot.Empty(),
            (_, current) => current with
            {
                SyncTableHash = null,
                Lamps = [],
                Switches = [],
                Values = [],
                Doors = [],
                LampIds = [],
                SwitchIds = [],
                ValueIds = [],
                DoorIds = [],
                UpdatedAtTick = Environment.TickCount64
            });
    }

    internal static void ObserveAdmittedVehicleIdentity(
        string? playerId,
        string? vehiclePath)
    {
        if (string.IsNullOrWhiteSpace(playerId) ||
            string.IsNullOrWhiteSpace(vehiclePath))
        {
            return;
        }

        var normalizedPlayer = playerId.Trim();
        var normalizedPath = vehiclePath
            .Trim()
            .Replace('/', '\\');

        while (true)
        {
            if (!AdmittedVehiclePaths.TryGetValue(
                    normalizedPlayer,
                    out var previous))
            {
                if (AdmittedVehiclePaths.TryAdd(
                        normalizedPlayer,
                        normalizedPath))
                {
                    return;
                }

                continue;
            }

            if (string.Equals(
                    previous,
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!AdmittedVehiclePaths.TryUpdate(
                    normalizedPlayer,
                    normalizedPath,
                    previous))
            {
                continue;
            }

            States.TryRemove(normalizedPlayer, out _);
            return;
        }
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
            AdmittedVehiclePaths.TryRemove(playerId, out _);
        }
    }

    internal static void Clear()
    {
        States.Clear();
        AdmittedVehiclePaths.Clear();
    }

    internal sealed record RemoteVehicleVarsSnapshot(
        uint? SyncTableHash,
        uint? VarTableHash,
        IReadOnlyDictionary<ushort, float> Floats,
        IReadOnlyDictionary<ushort, string> Strings,
        float[] Lamps,
        float[] Switches,
        float[] Values,
        float[] Doors,
        ushort[] LampIds,
        ushort[] SwitchIds,
        ushort[] ValueIds,
        ushort[] DoorIds,
        long UpdatedAtTick)
    {
        internal static RemoteVehicleVarsSnapshot Empty() =>
            new(
                null,
                null,
                new Dictionary<ushort, float>(),
                new Dictionary<ushort, string>(),
                [],
                [],
                [],
                [],
                [],
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
                SyncTableHash,
                VarTableHash,
                floats,
                strings,
                Lamps,
                Switches,
                Values,
                Doors,
                LampIds,
                SwitchIds,
                ValueIds,
                DoorIds,
                Environment.TickCount64);
        }
    }
}
