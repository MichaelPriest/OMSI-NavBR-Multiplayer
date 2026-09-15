using Microsoft.AspNetCore.SignalR;
using NavBR.Server.Multiplayer;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Server.Hubs;

public sealed class MultiplayerHub(MultiplayerRoomRegistry registry) : Hub
{
    private const int MaxChatLength = 280;
    private const int MaxVoicePayloadBytes = 1500;
    private const int MaxMapNameLength = 256;
    private const int MaxMapCompatibilityIdLength = 256;
    private const int MaxVehicleNameLength = 256;
    private const int MaxVehiclePathLength = 512;
    private const int MaxCompatibilityIdLength = 256;
    private const int MaxHofNameLength = 256;
    private const int MaxLineLength = 128;
    private const int MaxRouteLength = 128;
    private const int MaxStopNameLength = 256;
    private const int MaxDestinationLength = 256;
    private const int MaxVersionLength = 64;
    private const int MaxDeploymentLength = 64;
    private const int MaxCapabilities = 32;
    private const int MaxCapabilityLength = 64;

    public async Task<RoomSnapshot> JoinRoom(JoinRoomRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var roomId = NormalizeRequired(request.RoomId, 64, "room id");
        var playerId = NormalizeRequired(request.PlayerId, 64, "player id");
        var displayName = NormalizeRequired(request.DisplayName, 32, "display name");
        var mapName = NormalizeOptional(request.MapName, MaxMapNameLength, "map name");
        var mapCompatibilityId = NormalizeOptional(
            request.MapCompatibilityId,
            MaxMapCompatibilityIdLength,
            "map compatibility id");
        var compatibility = NormalizeCompatibility(request.Compatibility, mapName, mapCompatibilityId);

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
            mapName,
            mapCompatibilityId,
            compatibility);

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
            Timestamp = DateTimeOffset.UtcNow,
            MapName = NormalizeOptional(telemetry.MapName, MaxMapNameLength, "map name"),
            MapCompatibilityId = NormalizeOptional(
                telemetry.MapCompatibilityId,
                MaxMapCompatibilityIdLength,
                "map compatibility id"),
            VehicleName = NormalizeOptional(telemetry.VehicleName, MaxVehicleNameLength, "vehicle name"),
            VehiclePath = NormalizeOptional(telemetry.VehiclePath, MaxVehiclePathLength, "vehicle path"),
            VehicleCompatibilityId = NormalizeOptional(
                telemetry.VehicleCompatibilityId,
                MaxCompatibilityIdLength,
                "vehicle compatibility id"),
            HofName = NormalizeOptional(telemetry.HofName, MaxHofNameLength, "HOF name"),
            HofCompatibilityId = NormalizeOptional(
                telemetry.HofCompatibilityId,
                MaxCompatibilityIdLength,
                "HOF compatibility id"),
            Line = NormalizeOptional(telemetry.Line, MaxLineLength, "line"),
            Route = NormalizeOptional(telemetry.Route, MaxRouteLength, "route"),
            NextStopName = NormalizeOptional(telemetry.NextStopName, MaxStopNameLength, "next stop"),
            DestinationName = NormalizeOptional(
                telemetry.DestinationName,
                MaxDestinationLength,
                "destination")
        };

        var updatedPresence = registry.UpdateMap(
            Context.ConnectionId,
            safeTelemetry.MapName,
            safeTelemetry.MapCompatibilityId);
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

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new HubException($"Invalid {fieldName}.");
        }

        return normalized;
    }

    private static OmsiCompatibilityManifest? NormalizeCompatibility(
        OmsiCompatibilityManifest? compatibility,
        string? normalizedMapName,
        string? normalizedMapCompatibilityId)
    {
        if (compatibility is null)
        {
            return null;
        }

        if (compatibility.PluginProtocolVersion is < 1 or > 100)
        {
            throw new HubException("Invalid plugin protocol version.");
        }

        var capabilities = (compatibility.Capabilities ?? Array.Empty<string>())
            .Select(value => NormalizeRequired(value, MaxCapabilityLength, "capability"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCapabilities + 1)
            .ToArray();

        if (capabilities.Length > MaxCapabilities)
        {
            throw new HubException("Too many compatibility capabilities.");
        }

        return compatibility with
        {
            OmsiVersion = NormalizeOptional(compatibility.OmsiVersion, MaxVersionLength, "OMSI version"),
            NavBRVersion = NormalizeOptional(compatibility.NavBRVersion, MaxVersionLength, "NavBR version"),
            MapName = normalizedMapName ?? NormalizeOptional(compatibility.MapName, MaxMapNameLength, "map name"),
            MapCompatibilityId = normalizedMapCompatibilityId ?? NormalizeOptional(
                compatibility.MapCompatibilityId,
                MaxMapCompatibilityIdLength,
                "map compatibility id"),
            VehiclePath = NormalizeOptional(compatibility.VehiclePath, MaxVehiclePathLength, "vehicle path"),
            VehicleCompatibilityId = NormalizeOptional(
                compatibility.VehicleCompatibilityId,
                MaxCompatibilityIdLength,
                "vehicle compatibility id"),
            HofName = NormalizeOptional(compatibility.HofName, MaxHofNameLength, "HOF name"),
            HofCompatibilityId = NormalizeOptional(
                compatibility.HofCompatibilityId,
                MaxCompatibilityIdLength,
                "HOF compatibility id"),
            PluginDeployment = NormalizeOptional(
                compatibility.PluginDeployment,
                MaxDeploymentLength,
                "plugin deployment"),
            Capabilities = capabilities
        };
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

        ValidateOptionalFinite(telemetry.TileX, "tile X");
        ValidateOptionalFinite(telemetry.TileY, "tile Y");
        ValidateOptionalFinite(telemetry.AccelerationMps2, "acceleration");
        ValidateOptionalFinite(telemetry.FuelPercent, "fuel");
        ValidateOptionalFinite(telemetry.ThrottlePercent, "throttle");
        ValidateOptionalFinite(telemetry.BrakePercent, "brake");
        ValidateOptionalFinite(telemetry.SteeringDegrees, "steering");

        if (telemetry.FuelPercent is < 0 or > 100 ||
            telemetry.ThrottlePercent is < 0 or > 100 ||
            telemetry.BrakePercent is < 0 or > 100)
        {
            throw new HubException("Telemetry contains invalid percentage values.");
        }

        _ = NormalizeOptional(telemetry.MapName, MaxMapNameLength, "map name");
        _ = NormalizeOptional(
            telemetry.MapCompatibilityId,
            MaxMapCompatibilityIdLength,
            "map compatibility id");
        _ = NormalizeOptional(telemetry.VehicleName, MaxVehicleNameLength, "vehicle name");
        _ = NormalizeOptional(telemetry.VehiclePath, MaxVehiclePathLength, "vehicle path");
        _ = NormalizeOptional(telemetry.VehicleCompatibilityId, MaxCompatibilityIdLength, "vehicle compatibility id");
        _ = NormalizeOptional(telemetry.HofName, MaxHofNameLength, "HOF name");
        _ = NormalizeOptional(telemetry.HofCompatibilityId, MaxCompatibilityIdLength, "HOF compatibility id");
        _ = NormalizeOptional(telemetry.Line, MaxLineLength, "line");
        _ = NormalizeOptional(telemetry.Route, MaxRouteLength, "route");
        _ = NormalizeOptional(telemetry.NextStopName, MaxStopNameLength, "next stop");
        _ = NormalizeOptional(telemetry.DestinationName, MaxDestinationLength, "destination");
    }

    private static void ValidateOptionalFinite(double? value, string fieldName)
    {
        if (value is double number && !double.IsFinite(number))
        {
            throw new HubException($"Telemetry contains invalid {fieldName}.");
        }
    }
}
