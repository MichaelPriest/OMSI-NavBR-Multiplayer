using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class LocalVehicleVarsSampler
{
    private static readonly object Sync = new();
    private static uint? _varTableHash;
    private static ushort[] _floatIds = [];
    private static ushort[] _stringIds = [];
    private static long _lastSampleTickMs;
    private static PluginBridgeMessage? _pending;
    private const long MinimumSampleIntervalMs = 100;

    internal static bool Configure(
        PluginBridgeMessage message,
        out string? rejectionReason)
    {
        rejectionReason = null;
        if (message.VarTableHash is not uint hash || hash == 0)
        {
            rejectionReason = "missing-table";
            return false;
        }

        var ids = message.VariableIndices ?? [];
        var stringIds = message.StringVariableIndices ?? [];
        if (ids.Length > 512 ||
            stringIds.Length > 64 ||
            ids.Distinct().Count() != ids.Length ||
            stringIds.Distinct().Count() != stringIds.Length)
        {
            rejectionReason = "invalid-variable-ids";
            return false;
        }

        lock (Sync)
        {
            _varTableHash = hash;
            _floatIds = ids.ToArray();
            _stringIds = stringIds.ToArray();
            _lastSampleTickMs = 0;
            _pending = null;
        }

        return true;
    }

    internal static void Clear()
    {
        lock (Sync)
        {
            _varTableHash = null;
            _floatIds = [];
            _stringIds = [];
            _lastSampleTickMs = 0;
            _pending = null;
        }
    }

    /// <summary>
    /// Must run on OMSI's callback thread. Reads only the live player vehicle.
    /// </summary>
    internal static void Sample()
    {
        uint hash;
        ushort[] ids;
        ushort[] stringIds;
        lock (Sync)
        {
            if (_varTableHash is not uint configured ||
                (_floatIds.Length == 0 && _stringIds.Length == 0))
            {
                return;
            }

            var now = Environment.TickCount64;
            if (_lastSampleTickMs > 0 &&
                now >= _lastSampleTickMs &&
                now - _lastSampleTickMs < MinimumSampleIntervalMs)
            {
                return;
            }

            _lastSampleTickMs = now;
            hash = configured;
            ids = _floatIds;
            stringIds = _stringIds;
        }

        var player = OmsiNativeInterop.GetPlayerVehiclePointer();
        if (player == 0)
        {
            return;
        }

        var count =
            OmsiNativeInterop.TryGetRoadVehiclePublicVarCount(player);
        var stringCount =
            OmsiNativeInterop.TryGetRoadVehicleStringVarCount(player);
        // A vehicle may expose only a subset of the declared script slots
        // (notably StringVars while a bus is still loading). Do not let one
        // missing slot suppress otherwise valid float/visual STATE samples.
        var sampledFloatIds = new List<ushort>(ids.Length);
        var sampledValues = new List<float>(ids.Length);
        if (count > 0)
        {
            foreach (var id in ids)
            {
                if (id >= count ||
                    !OmsiNativeInterop.TryReadRoadVehiclePublicVar(
                        player, id, out var value) ||
                    !float.IsFinite(value))
                {
                    continue;
                }

                sampledFloatIds.Add(id);
                sampledValues.Add(value);
            }
        }

        var sampledStringIds = new List<ushort>(stringIds.Length);
        var sampledStringValues = new List<string>(stringIds.Length);
        if (stringCount > 0)
        {
            foreach (var id in stringIds)
            {
                if (id >= stringCount ||
                    !OmsiNativeInterop.TryReadRoadVehicleStringVar(
                        player, id, out var value) ||
                    value.Length > 255)
                {
                    continue;
                }

                sampledStringIds.Add(id);
                sampledStringValues.Add(value);
            }
        }

        if (sampledFloatIds.Count == 0 &&
            sampledStringIds.Count == 0)
        {
            return;
        }

        var snapshot = new PluginBridgeMessage(
            PluginBridgeProtocol.LocalVehicleVars,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            TimestampUnixMilliseconds:
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            VarTableHash: hash,
            VariableIndices: sampledFloatIds.ToArray(),
            VariableValues: sampledValues.ToArray(),
            StringVariableIndices: sampledStringIds.ToArray(),
            StringVariableValues: sampledStringValues.ToArray());

        lock (Sync)
        {
            if (_varTableHash == hash &&
                ReferenceEquals(_floatIds, ids) &&
                ReferenceEquals(_stringIds, stringIds))
            {
                _pending = snapshot;
            }
        }
    }

    internal static PluginBridgeMessage? TakePending()
    {
        lock (Sync)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }
}
