using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal sealed class RemoteVehicleRegistry
{
    private const int MaxRemoteVehicles = 64;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan InterpolationDelay = TimeSpan.FromMilliseconds(100);

    private readonly object _sync = new();
    private readonly Dictionary<string, RemoteVehicleEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _staleIds =
        new(MaxRemoteVehicles);

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
            var compatible = 0;
            foreach (var entry in _entries.Values)
            {
                if (IsCompatible(localState, entry.Current))
                {
                    compatible++;
                }
            }

            return compatible;
        }
    }

    public (int Total, int Compatible) CountSnapshot(
        PluginBridgeMessage? localState)
    {
        lock (_sync)
        {
            PruneStaleUnsafe(DateTimeOffset.UtcNow);
            var compatible = 0;
            foreach (var entry in _entries.Values)
            {
                if (IsCompatible(localState, entry.Current))
                {
                    compatible++;
                }
            }

            return (_entries.Count, compatible);
        }
    }

    public PluginBridgeMessage? LatestCompatible(PluginBridgeMessage? localState)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            PruneStaleUnsafe(now);
            RemoteVehicleEntry? latest = null;
            foreach (var candidate in _entries.Values)
            {
                if (!IsCompatible(localState, candidate.Current) ||
                    (latest is not null &&
                     candidate.ReceivedAtUtc <= latest.ReceivedAtUtc))
                {
                    continue;
                }

                latest = candidate;
            }

            return latest?.Sample(now - InterpolationDelay);
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

            if (_entries.TryGetValue(message.PlayerId!, out var existing))
            {
                existing.Push(message, now);
                return true;
            }

            if (_entries.Count >= MaxRemoteVehicles)
            {
                string? oldestPlayerId = null;
                var oldestReceivedAtUtc = DateTimeOffset.MaxValue;
                foreach (var pair in _entries)
                {
                    if (pair.Value.ReceivedAtUtc >= oldestReceivedAtUtc)
                    {
                        continue;
                    }

                    oldestPlayerId = pair.Key;
                    oldestReceivedAtUtc = pair.Value.ReceivedAtUtc;
                }

                if (!string.IsNullOrWhiteSpace(oldestPlayerId))
                {
                    _entries.Remove(oldestPlayerId);
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

    public (int Removed, int Total, int Compatible) PruneAndCount(
        PluginBridgeMessage? localState)
    {
        lock (_sync)
        {
            var removed = PruneStaleUnsafe(DateTimeOffset.UtcNow);
            var compatible = 0;
            foreach (var entry in _entries.Values)
            {
                if (IsCompatible(localState, entry.Current))
                {
                    compatible++;
                }
            }

            return (removed, _entries.Count, compatible);
        }
    }

    private int PruneStaleUnsafe(DateTimeOffset now)
    {
        _staleIds.Clear();
        foreach (var pair in _entries)
        {
            if (now - pair.Value.ReceivedAtUtc > StaleAfter)
            {
                _staleIds.Add(pair.Key);
            }
        }

        foreach (var playerId in _staleIds)
        {
            _entries.Remove(playerId);
        }

        var removed = _staleIds.Count;
        _staleIds.Clear();
        return removed;
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

    private sealed class RemoteVehicleEntry
    {
        private PluginBridgeMessage? _previous;
        private DateTimeOffset _previousReceivedAtUtc;

        public RemoteVehicleEntry(PluginBridgeMessage message, DateTimeOffset receivedAtUtc)
        {
            Current = message;
            ReceivedAtUtc = receivedAtUtc;
        }

        public PluginBridgeMessage Current { get; private set; }
        public DateTimeOffset ReceivedAtUtc { get; private set; }

        public void Push(PluginBridgeMessage message, DateTimeOffset receivedAtUtc)
        {
            _previous = Current;
            _previousReceivedAtUtc = ReceivedAtUtc;
            Current = message;
            ReceivedAtUtc = receivedAtUtc;
        }

        public PluginBridgeMessage Sample(DateTimeOffset targetUtc)
        {
            if (_previous is null ||
                ReceivedAtUtc <= _previousReceivedAtUtc ||
                targetUtc <= _previousReceivedAtUtc)
            {
                return _previous ?? Current;
            }

            if (targetUtc >= ReceivedAtUtc)
            {
                return Current;
            }

            var totalMilliseconds = (ReceivedAtUtc - _previousReceivedAtUtc).TotalMilliseconds;
            if (totalMilliseconds <= 0.001d)
            {
                return Current;
            }

            var elapsedMilliseconds = (targetUtc - _previousReceivedAtUtc).TotalMilliseconds;
            var amount = Math.Clamp(elapsedMilliseconds / totalMilliseconds, 0d, 1d);

            return Current with
            {
                TimestampUnixMilliseconds = targetUtc.ToUnixTimeMilliseconds(),
                X = Lerp(_previous.X!.Value, Current.X!.Value, amount),
                Y = Lerp(_previous.Y!.Value, Current.Y!.Value, amount),
                Z = Lerp(_previous.Z!.Value, Current.Z!.Value, amount),
                HeadingDegrees = LerpHeading(
                    _previous.HeadingDegrees!.Value,
                    Current.HeadingDegrees!.Value,
                    amount),
                SpeedKph = Lerp(_previous.SpeedKph!.Value, Current.SpeedKph!.Value, amount)
            };
        }

        private static double Lerp(double from, double to, double amount) =>
            from + (to - from) * amount;

        private static double LerpHeading(double from, double to, double amount)
        {
            var delta = ((to - from + 540d) % 360d) - 180d;
            var value = from + delta * amount;
            value %= 360d;
            return value < 0d ? value + 360d : value;
        }
    }
}
