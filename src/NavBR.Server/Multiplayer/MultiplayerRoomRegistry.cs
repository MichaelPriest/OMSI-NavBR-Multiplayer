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

    public IReadOnlyList<string> GetActiveRoomIds()
    {
        return _connections.Values
            .Select(player => player.RoomId)
            .Where(roomId => !string.IsNullOrWhiteSpace(roomId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roomId => roomId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<PublicRoomSummary> GetPublicRoomSummaries(RoomAccessPolicyStore policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        var now = DateTimeOffset.UtcNow;
        var rooms = new List<PublicRoomSummary>();
        foreach (var roomId in GetActiveRoomIds())
        {
            var descriptor = policies.Describe(roomId, this);
            if (descriptor.IsPrivate)
            {
                continue;
            }

            var players = GetRoomPlayers(roomId);
            if (players.Count == 0)
            {
                continue;
            }

            var authorityPlayerId = GetTrafficAuthorityPlayerId(roomId);
            var authority = players.FirstOrDefault(player =>
                string.Equals(player.PlayerId, authorityPlayerId, StringComparison.OrdinalIgnoreCase));
            var requirements = authority?.Compatibility ??
                               players.Select(player => player.Compatibility).FirstOrDefault(value => value is not null);

            var mapName = NormalizeOptional(requirements?.MapName) ??
                          NormalizeOptional(authority?.MapName) ??
                          players.Select(player => NormalizeOptional(player.MapName)).FirstOrDefault(value => value is not null);
            var mapCompatibilityId = NormalizeOptional(requirements?.MapCompatibilityId) ??
                                     NormalizeOptional(authority?.MapCompatibilityId) ??
                                     players.Select(player => NormalizeOptional(player.MapCompatibilityId)).FirstOrDefault(value => value is not null);

            rooms.Add(new PublicRoomSummary(
                RoomId: roomId,
                PlayerCount: players.Count,
                MapName: mapName,
                MapCompatibilityId: mapCompatibilityId,
                UpdatedAtUtc: now,
                OmsiVersion: NormalizeOptional(requirements?.OmsiVersion),
                NavBRVersion: NormalizeOptional(requirements?.NavBRVersion),
                VehiclePath: NormalizeOptional(requirements?.VehiclePath),
                VehicleCompatibilityId: NormalizeOptional(requirements?.VehicleCompatibilityId),
                HofName: NormalizeOptional(requirements?.HofName),
                HofCompatibilityId: NormalizeOptional(requirements?.HofCompatibilityId),
                PluginProtocolVersion: requirements?.PluginProtocolVersion ?? 0));
        }

        return rooms;
    }

    public string? GetTrafficAuthorityPlayerId(string roomId)
    {
        return _connections.Values
            .Where(player => string.Equals(player.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.ConnectedAtUtc)
            .ThenBy(player => player.PlayerId, StringComparer.OrdinalIgnoreCase)
            .Select(player => player.PlayerId)
            .FirstOrDefault();
    }

    public bool IsTrafficAuthority(string connectionId)
    {
        if (!TryGet(connectionId, out var presence) || presence is null)
        {
            return false;
        }

        var authorityPlayerId = GetTrafficAuthorityPlayerId(presence.RoomId);
        return !string.IsNullOrWhiteSpace(authorityPlayerId) &&
               string.Equals(
                   authorityPlayerId,
                   presence.PlayerId,
                   StringComparison.OrdinalIgnoreCase);
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

    public PlayerPresence? UpdateClientStatus(
        string connectionId,
        bool voiceEnabled,
        int? latencyMs)
    {
        while (_connections.TryGetValue(connectionId, out var current))
        {
            var normalizedLatency = latencyMs is null
                ? null
                : Math.Clamp(latencyMs.Value, 0, 5000);

            if (current.VoiceEnabled == voiceEnabled &&
                current.LatencyMs == normalizedLatency)
            {
                return null;
            }

            var updated = current with
            {
                VoiceEnabled = voiceEnabled,
                LatencyMs = normalizedLatency
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
