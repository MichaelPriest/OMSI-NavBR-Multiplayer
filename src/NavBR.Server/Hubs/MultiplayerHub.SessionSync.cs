using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NavBR.Shared.Multiplayer;

namespace NavBR.Server.Hubs;

public sealed partial class MultiplayerHub
{
    private static readonly ConcurrentDictionary<string, SessionOperationalState> SessionOperationalStates =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<SessionOperationalState?> GetSessionOperationalState()
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            return Task.FromResult<SessionOperationalState?>(null);
        }

        PruneSessionOperationalStates();
        var authorityPlayerId = registry.GetTrafficAuthorityPlayerId(presence.RoomId);
        if (string.IsNullOrWhiteSpace(authorityPlayerId))
        {
            SessionOperationalStates.TryRemove(presence.RoomId, out _);
            return Task.FromResult<SessionOperationalState?>(null);
        }

        if (!SessionOperationalStates.TryGetValue(presence.RoomId, out var state) ||
            !string.Equals(state.AuthorityPlayerId, authorityPlayerId, StringComparison.OrdinalIgnoreCase))
        {
            SessionOperationalStates.TryRemove(presence.RoomId, out _);
            return Task.FromResult<SessionOperationalState?>(null);
        }

        return Task.FromResult<SessionOperationalState?>(state);
    }

    public async Task<SessionOperationalState> PublishSessionOperationalState(SessionOperationalState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before publishing session state.");
        }

        if (!registry.IsTrafficAuthority(Context.ConnectionId))
        {
            throw new HubException("Only the current session authority can publish session state.");
        }

        if (state.Sequence < 0)
        {
            throw new HubException("Invalid session state sequence.");
        }

        var mapName = NormalizeOptional(state.MapName, MaxMapNameLength, "session map name");
        var mapCompatibilityId = NormalizeOptional(
            state.MapCompatibilityId,
            MaxMapCompatibilityIdLength,
            "session map compatibility id");

        if (!MapsMatch(presence, mapName, mapCompatibilityId))
        {
            throw new HubException("Session state does not match the authority map.");
        }

        var safeState = new SessionOperationalState(
            presence.PlayerId,
            state.Sequence,
            DateTimeOffset.UtcNow,
            mapName ?? presence.MapName,
            mapCompatibilityId ?? presence.MapCompatibilityId,
            NormalizeOptional(state.Line, MaxLineLength, "session line"),
            NormalizeOptional(state.Route, MaxRouteLength, "session route"),
            NormalizeOptional(state.DestinationName, MaxDestinationLength, "session destination"),
            NormalizeOptional(state.NextStopName, MaxStopNameLength, "session next stop"));

        SessionOperationalStates[presence.RoomId] = safeState;
        PruneSessionOperationalStates();

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("sessionOperationalState", safeState);

        return safeState;
    }

    private static void PruneSessionOperationalStates()
    {
        if (SessionOperationalStates.Count < 512)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(2);
        foreach (var pair in SessionOperationalStates)
        {
            if (pair.Value.ServerTimestampUtc < cutoff)
            {
                SessionOperationalStates.TryRemove(pair.Key, out _);
            }
        }
    }
}
