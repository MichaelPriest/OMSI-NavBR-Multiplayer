using System.Collections.Concurrent;
using NavBR.Shared.Multiplayer;

namespace NavBR.Server.Multiplayer;

public sealed class MultiplayerRoomRegistry
{
    private readonly ConcurrentDictionary<string, PlayerPresence> _connections =
        new(StringComparer.Ordinal);

    public PlayerPresence Upsert(
        string connectionId,
        string roomId,
        string playerId,
        string displayName,
        string? mapName,
        string? mapCompatibilityId,
        OmsiCompatibilityManifest? compatibility = null)
    {
        var presence = new PlayerPresence(
            playerId,
            displayName,
            roomId,
            NormalizeOptional(mapName),
            DateTimeOffset.UtcNow,
            NormalizeOptional(mapCompatibilityId),
            compatibility);

        _connections[connectionId] = presence;
        return presence;
    }

    public bool TryGet(string connectionId, out PlayerPresence? presence)
    {
        if (_connections.TryGetValue(connectionId, out var value))
        {
            presence = value;
            return true;
        }

        presence = null;
        return false;
    }

    public IReadOnlyList<PlayerPresence> GetRoomPlayers(string roomId)
    {
        return _connections.Values
            .Where(player => string.Equals(player.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public PlayerPresence? UpdateMap(
        string connectionId,
        string? mapName,
        string? mapCompatibilityId = null)
    {
        while (_connections.TryGetValue(connectionId, out var current))
        {
            var normalizedMap = NormalizeOptional(mapName);
            var normalizedCompatibilityId = NormalizeOptional(mapCompatibilityId);
            if (string.Equals(current.MapName, normalizedMap, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(current.MapCompatibilityId, normalizedCompatibilityId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var compatibility = current.Compatibility is null
                ? null
                : current.Compatibility with
                {
                    MapName = normalizedMap,
                    MapCompatibilityId = normalizedCompatibilityId
                };

            var updated = current with
            {
                MapName = normalizedMap,
                MapCompatibilityId = normalizedCompatibilityId,
                Compatibility = compatibility
            };

            if (_connections.TryUpdate(connectionId, updated, current))
            {
                return updated;
            }
        }

        return null;
    }

    public PlayerPresence? Remove(string connectionId)
    {
        return _connections.TryRemove(connectionId, out var presence)
            ? presence
            : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
