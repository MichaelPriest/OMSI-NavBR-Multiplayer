using Microsoft.AspNetCore.SignalR;
using NavBR.Server.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Server.Hubs;

internal sealed class RoomPrivacyHubFilter(
    RoomAccessPolicyStore policies,
    MultiplayerRoomRegistry registry,
    IHubContext<MultiplayerHub> hubContext) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (string.Equals(invocationContext.HubMethodName, "JoinRoom", StringComparison.Ordinal) &&
            invocationContext.HubMethodArguments.Count > 0 &&
            invocationContext.HubMethodArguments[0] is JoinRoomRequest request)
        {
            return await InvokeJoinRoomAsync(invocationContext, request, next);
        }

        if (string.Equals(invocationContext.HubMethodName, "LeaveRoom", StringComparison.Ordinal))
        {
            var previousRoomId = GetCurrentRoom(invocationContext.Context.ConnectionId);
            try
            {
                return await next(invocationContext);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousRoomId))
                {
                    await BroadcastOwnerAsync(previousRoomId);
                    policies.CleanupIfEmpty(previousRoomId, registry);
                }
            }
        }

        return await next(invocationContext);
    }

    public Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next) =>
        next(context);

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        var roomId = GetCurrentRoom(context.Context.ConnectionId);
        await next(context, exception);
        if (!string.IsNullOrWhiteSpace(roomId))
        {
            await BroadcastOwnerAsync(roomId);
            policies.CleanupIfEmpty(roomId, registry);
        }
    }

    private async ValueTask<object?> InvokeJoinRoomAsync(
        HubInvocationContext invocationContext,
        JoinRoomRequest request,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var roomId = NormalizeRequired(request.RoomId, 64);
        var playerId = NormalizeRequired(request.PlayerId, 64);
        var password = request.RoomPassword;
        if (password is { Length: > 128 })
        {
            throw new HubException("NAVBR_ROOM_PASSWORD_INVALID");
        }

        var previousRoomId = GetCurrentRoom(invocationContext.Context.ConnectionId);
        var decision = policies.Authorize(
            roomId,
            playerId,
            request.CreatePrivateRoom,
            password,
            registry);

        if (!decision.Allowed)
        {
            throw new HubException(decision.ErrorCode ?? "NAVBR_ROOM_ACCESS_DENIED");
        }

        try
        {
            var result = await next(invocationContext);
            if (result is RoomSnapshot snapshot)
            {
                var descriptor = policies.Describe(roomId, registry);
                await hubContext.Clients.Group(roomId).SendAsync(
                    "roomOwnerChanged",
                    descriptor.OwnerPlayerId);
                return snapshot with
                {
                    IsPrivate = descriptor.IsPrivate,
                    OwnerPlayerId = descriptor.OwnerPlayerId
                };
            }
            return result;
        }
        finally
        {
            policies.CompleteAuthorization(roomId, registry);
            if (!string.IsNullOrWhiteSpace(previousRoomId) &&
                !string.Equals(previousRoomId, roomId, StringComparison.OrdinalIgnoreCase))
            {
                await BroadcastOwnerAsync(previousRoomId);
                policies.CleanupIfEmpty(previousRoomId, registry);
            }
        }
    }

    private async Task BroadcastOwnerAsync(string roomId)
    {
        var descriptor = policies.Describe(roomId, registry);
        await hubContext.Clients.Group(roomId).SendAsync(
            "roomOwnerChanged",
            descriptor.OwnerPlayerId);
    }

    private string? GetCurrentRoom(string connectionId) =>
        registry.TryGet(connectionId, out var presence) && presence is not null
            ? presence.RoomId
            : null;

    private static string NormalizeRequired(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length is < 1 || normalized.Length > maxLength)
        {
            throw new HubException("NAVBR_ROOM_INVALID_REQUEST");
        }
        return normalized;
    }
}
