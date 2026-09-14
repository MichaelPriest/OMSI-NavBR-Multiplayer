using Microsoft.AspNetCore.SignalR;
using NavBR.Server.Multiplayer;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Server.Hubs;

public sealed class MultiplayerHub(MultiplayerRoomRegistry registry) : Hub
{
    private const int MaxChatLength = 280;
    private const int MaxVoicePayloadBytes = 1500;

    public async Task<RoomSnapshot> JoinRoom(JoinRoomRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var roomId = NormalizeRequired(request.RoomId, 64, "room id");
        var playerId = NormalizeRequired(request.PlayerId, 64, "player id");
        var displayName = NormalizeRequired(request.DisplayName, 32, "display name");

        if (registry.TryGet(Context.ConnectionId, out var previous) && previous is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous.RoomId);
            registry.Remove(Context.ConnectionId);
            await Clients.Group(previous.RoomId).SendAsync("playerLeft", previous.PlayerId);
        }

        var presence = registry.Upsert(
            Context.ConnectionId,
            roomId,
            playerId,
            displayName,
            request.MapName,
            request.MapCompatibilityId);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.OthersInGroup(roomId).SendAsync("playerJoined", presence);

        return new RoomSnapshot(roomId, registry.GetRoomPlayers(roomId));
    }

    public async Task LeaveRoom()
    {
        var presence = registry.Remove(Context.ConnectionId);
        if (presence is null)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, presence.RoomId);
        await Clients.Group(presence.RoomId).SendAsync("playerLeft", presence.PlayerId);
    }

    public async Task PublishTelemetry(VehicleTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before publishing telemetry.");
        }

        ValidateTelemetry(telemetry);

        var safeTelemetry = telemetry with
        {
            PlayerId = presence.PlayerId,
            Timestamp = DateTimeOffset.UtcNow
        };

        var updatedPresence = registry.UpdateMap(
            Context.ConnectionId,
            safeTelemetry.MapName,
            presence.MapCompatibilityId);
        var currentPresence = updatedPresence ?? presence;

        if (updatedPresence is not null)
        {
            await Clients.Group(presence.RoomId).SendAsync("playerPresenceChanged", updatedPresence);
        }

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("telemetry", new PlayerTelemetryFrame(currentPresence, safeTelemetry));
    }

    public async Task SendChatMessage(string text)
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before sending chat messages.");
        }

        var normalized = (text ?? string.Empty).Trim();
        if (normalized.Length is < 1 or > MaxChatLength)
        {
            throw new HubException("Invalid chat message.");
        }

        var message = new ChatMessage(
            presence.PlayerId,
            presence.DisplayName,
            normalized,
            DateTimeOffset.UtcNow);

        await Clients.Group(presence.RoomId).SendAsync("chatMessage", message);
    }

    public async Task PublishVoiceFrame(long sequence, byte[] opusPayload)
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before publishing voice.");
        }

        if (sequence < 0 ||
            opusPayload is null ||
            opusPayload.Length is < 1 or > MaxVoicePayloadBytes)
        {
            throw new HubException("Invalid voice frame.");
        }

        var frame = new VoiceFrame(
            presence.PlayerId,
            sequence,
            opusPayload,
            DateTimeOffset.UtcNow);

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("voiceFrame", frame);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var presence = registry.Remove(Context.ConnectionId);
        if (presence is not null)
        {
            await Clients.Group(presence.RoomId).SendAsync("playerLeft", presence.PlayerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length is < 1 || normalized.Length > maxLength)
        {
            throw new HubException($"Invalid {fieldName}.");
        }

        return normalized;
    }

    private static void ValidateTelemetry(VehicleTelemetry telemetry)
    {
        if (!double.IsFinite(telemetry.X) ||
            !double.IsFinite(telemetry.Y) ||
            !double.IsFinite(telemetry.Z) ||
            !double.IsFinite(telemetry.HeadingDegrees) ||
            !double.IsFinite(telemetry.SpeedKph))
        {
            throw new HubException("Telemetry contains invalid numeric values.");
        }

        if ((telemetry.TileX is double tileX && !double.IsFinite(tileX)) ||
            (telemetry.TileY is double tileY && !double.IsFinite(tileY)))
        {
            throw new HubException("Telemetry contains invalid tile coordinates.");
        }
    }
}
