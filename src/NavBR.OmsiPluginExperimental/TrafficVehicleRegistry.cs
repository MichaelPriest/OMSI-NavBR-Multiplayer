using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Holds the latest full road-traffic snapshot received from the room traffic
/// authority. This is intentionally read/state-only for now: the registry is
/// the safe hand-off point for the future OMSI physical vehicle backend.
/// </summary>
internal sealed class TrafficVehicleRegistry
{
    private const int MaxTrafficVehicles = 48;
    private const int MaxAuthorityPlayerIdLength = 128;
    private const int MaxTrafficIdLength = 128;
    private const int MaxVehiclePathLength = 512;
    private const int MaxCompatibilityIdLength = 256;
    private const int MaxMapFieldLength = 256;

    // Snapshots are published at 2 Hz. Four seconds gives enough tolerance for
    // transient scheduling/network stalls while still preventing frozen traffic.
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(4);

    private readonly object _sync = new();
    private PluginBridgeMessage? _snapshot;
    private DateTimeOffset _receivedAtUtc;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                PruneStaleUnsafe(DateTimeOffset.UtcNow);
                return _snapshot?.TrafficVehicles?.Length ?? 0;
            }
        }
    }

    public string? AuthorityPlayerId
    {
        get
        {
            lock (_sync)
            {
                PruneStaleUnsafe(DateTimeOffset.UtcNow);
                return _snapshot?.AuthorityPlayerId;
            }
        }
    }

    public long? Sequence
    {
        get
        {
            lock (_sync)
            {
                PruneStaleUnsafe(DateTimeOffset.UtcNow);
                return _snapshot?.Sequence;
            }
        }
    }

    public PluginBridgeMessage? LatestCompatible(PluginBridgeMessage? localState)
    {
        lock (_sync)
        {
            PruneStaleUnsafe(DateTimeOffset.UtcNow);
            if (_snapshot is null || !MapsMatch(localState, _snapshot))
            {
                return null;
            }

            // Do not leak our mutable array reference to a future consumer.
            return CloneSnapshot(_snapshot);
        }
    }

    public bool TryApply(
        PluginBridgeMessage message,
        PluginBridgeMessage? localState,
        out string? rejectionReason)
    {
        rejectionReason = ValidateSnapshot(message);
        if (rejectionReason is not null)
        {
            return false;
        }

        if (localState is not null &&
            localState.IsInGame == true &&
            !MapsMatch(localState, message))
        {
            rejectionReason = "map-mismatch";
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            PruneStaleUnsafe(now);

            if (_snapshot is not null &&
                string.Equals(
                    _snapshot.AuthorityPlayerId,
                    message.AuthorityPlayerId,
                    StringComparison.OrdinalIgnoreCase) &&
                message.Sequence <= _snapshot.Sequence)
            {
                rejectionReason = "stale-sequence";
                return false;
            }

            // A new elected authority may restart its sequence at zero. Changing
            // authority therefore deliberately bypasses the previous sequence.
            _snapshot = CloneSnapshot(message);
            _receivedAtUtc = now;
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            ClearUnsafe();
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
        if (_snapshot is null || now - _receivedAtUtc <= StaleAfter)
        {
            return 0;
        }

        var removed = _snapshot.TrafficVehicles?.Length ?? 0;
        ClearUnsafe();
        return removed;
    }

    private void ClearUnsafe()
    {
        _snapshot = null;
        _receivedAtUtc = default;
    }

    private static string? ValidateSnapshot(PluginBridgeMessage message)
    {
        if (!string.Equals(
                message.Type,
                PluginBridgeProtocol.TrafficSnapshotState,
                StringComparison.Ordinal) ||
            message.ProtocolVersion != PluginBridgeProtocol.Version)
        {
            return "invalid-type-or-protocol";
        }

        if (string.IsNullOrWhiteSpace(message.AuthorityPlayerId) ||
            message.AuthorityPlayerId.Length > MaxAuthorityPlayerIdLength)
        {
            return "invalid-authority";
        }

        if (message.Sequence is not long sequence || sequence < 0)
        {
            return "invalid-sequence";
        }

        if ((message.MapName?.Length ?? 0) > MaxMapFieldLength ||
            (message.MapCompatibilityId?.Length ?? 0) > MaxMapFieldLength)
        {
            return "invalid-map";
        }

        if (message.TrafficVehicles is not { } vehicles ||
            vehicles.Length > MaxTrafficVehicles)
        {
            return "invalid-vehicle-count";
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vehicle in vehicles)
        {
            if (vehicle is null)
            {
                return "null-vehicle";
            }

            if (string.IsNullOrWhiteSpace(vehicle.TrafficId) ||
                vehicle.TrafficId.Length > MaxTrafficIdLength ||
                !ids.Add(vehicle.TrafficId))
            {
                return "invalid-traffic-id";
            }

            if ((vehicle.VehiclePath?.Length ?? 0) > MaxVehiclePathLength ||
                (vehicle.VehicleCompatibilityId?.Length ?? 0) > MaxCompatibilityIdLength)
            {
                return "invalid-vehicle-identity";
            }

            if (!IsFinite(vehicle.X) ||
                !IsFinite(vehicle.Y) ||
                !IsFinite(vehicle.Z) ||
                !IsFinite(vehicle.LocalX) ||
                !IsFinite(vehicle.LocalY) ||
                !IsFinite(vehicle.LocalZ) ||
                !IsFinite(vehicle.RotationX) ||
                !IsFinite(vehicle.RotationY) ||
                !IsFinite(vehicle.RotationZ) ||
                !IsFinite(vehicle.RotationW) ||
                !IsFinite(vehicle.SpeedKph) ||
                vehicle.SpeedKph is < -20d or > 250d)
            {
                return "invalid-vehicle-state";
            }

            var quaternionNormSquared =
                (vehicle.RotationX * vehicle.RotationX) +
                (vehicle.RotationY * vehicle.RotationY) +
                (vehicle.RotationZ * vehicle.RotationZ) +
                (vehicle.RotationW * vehicle.RotationW);

            if (!double.IsFinite(quaternionNormSquared) || quaternionNormSquared < 1e-8d)
            {
                return "invalid-rotation";
            }
        }

        return null;
    }

    private static bool MapsMatch(
        PluginBridgeMessage? localState,
        PluginBridgeMessage snapshot)
    {
        if (localState is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(localState.MapCompatibilityId) &&
            !string.IsNullOrWhiteSpace(snapshot.MapCompatibilityId))
        {
            return string.Equals(
                localState.MapCompatibilityId,
                snapshot.MapCompatibilityId,
                StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(localState.MapName) &&
            !string.IsNullOrWhiteSpace(snapshot.MapName))
        {
            return string.Equals(
                localState.MapName,
                snapshot.MapName,
                StringComparison.OrdinalIgnoreCase);
        }

        // Compatibility is already checked by the desktop relay. If one side
        // temporarily lacks map metadata, keep the snapshot state but do not
        // infer a mismatch from missing data alone.
        return true;
    }

    private static PluginBridgeMessage CloneSnapshot(PluginBridgeMessage source) =>
        source with
        {
            TrafficVehicles = source.TrafficVehicles?
                .Select(CloneVehicle)
                .ToArray()
        };

    private static TrafficVehicleState CloneVehicle(TrafficVehicleState source) =>
        source with { };

    private static bool IsFinite(double value) => double.IsFinite(value);
}
