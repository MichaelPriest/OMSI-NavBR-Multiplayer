using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal sealed class RemoteVehicleRegistry
{
    private const int MaxRemoteVehicles = 64;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);

    private readonly object _sync = new();
    private readonly Dictionary<string, RemoteVehicleEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count
    {
        get
        {
            lock (_sync)
            {
                PruneStaleUnsafe(DateTimeOffset.UtcNow);
                return _entries.Count;
            }
        }
    }

    public int CountCompatible(PluginBridgeMessage? localState)
    {
        lock (_sync)
        {
            PruneStaleUnsafe(DateTimeOffset.UtcNow);
            return _entries.Values.Count(entry => IsCompatible(localState, entry.Message));
        }
    }

    public PluginBridgeMessage? LatestCompatible(PluginBridgeMessage? localState)
    {
        lock (_sync)
        {
            PruneStaleUnsafe(DateTimeOffset.UtcNow);
            return _entries.Values
                .Where(entry => IsCompatible(localState, entry.Message))
                .OrderByDescending(entry => entry.ReceivedAtUtc)
                .Select(entry => entry.Message)
                .FirstOrDefault();
        }
    }

    public bool Upsert(PluginBridgeMessage message)
    {
        if (!IsValidRemoteState(message))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            PruneStaleUnsafe(now);

            if (!_entries.ContainsKey(message.PlayerId!) && _entries.Count >= MaxRemoteVehicles)
            {
                var oldest = _entries
                    .OrderBy(pair => pair.Value.ReceivedAtUtc)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(oldest.Key))
                {
                    _entries.Remove(oldest.Key);
                }
            }

            _entries[message.PlayerId!] = new RemoteVehicleEntry(message, now);
            return true;
        }
    }

    public bool Remove(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        lock (_sync)
        {
            return _entries.Remove(playerId);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
        }
    }

    public int PruneStale()
    {
        lock (_sync)
        {
            return PruneStaleUnsafe(DateTimeOffset.UtcNow);
        }
    }

    private int PruneStaleUnsafe(DateTimeOffset now)
    {
        var staleIds = _entries
            .Where(pair => now - pair.Value.ReceivedAtUtc > StaleAfter)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var playerId in staleIds)
        {
            _entries.Remove(playerId);
        }

        return staleIds.Length;
    }

    private static bool IsCompatible(
        PluginBridgeMessage? localState,
        PluginBridgeMessage remoteState)
    {
        if (localState is null ||
            localState.IsInGame != true ||
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

    private static bool IsValidRemoteState(PluginBridgeMessage message)
    {
        if (!string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleState, StringComparison.Ordinal) ||
            message.ProtocolVersion != PluginBridgeProtocol.Version ||
            string.IsNullOrWhiteSpace(message.PlayerId) ||
            message.PlayerId.Length > 128 ||
            (message.DisplayName?.Length ?? 0) > 128 ||
            (message.MapName?.Length ?? 0) > 256 ||
            (message.MapCompatibilityId?.Length ?? 0) > 256)
        {
            return false;
        }

        return IsFinite(message.X) &&
               IsFinite(message.Y) &&
               IsFinite(message.Z) &&
               IsFinite(message.HeadingDegrees) &&
               IsFinite(message.SpeedKph);
    }

    private static bool IsFinite(double? value) =>
        value is double number && double.IsFinite(number);

    private sealed record RemoteVehicleEntry(
        PluginBridgeMessage Message,
        DateTimeOffset ReceivedAtUtc);
}
