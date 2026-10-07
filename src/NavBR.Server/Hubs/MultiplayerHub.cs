using Microsoft.AspNetCore.SignalR;
using NavBR.Server.Multiplayer;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;
using NavBR.Shared.Network;
using NavBR.Shared.OpenOmsi;

namespace NavBR.Server.Hubs;

public sealed partial class MultiplayerHub(MultiplayerRoomRegistry registry) : Hub
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
    private const int MaxTrafficVehicles = 48;
    private const int MaxTrafficIdLength = 128;
    private const int MaxCompanyIdLength = 96;
    private const int MaxCompanyNameLength = 96;
    private const int MaxCompanyShortNameLength = 16;
    private const int MaxEmployeeNumberLength = 12;

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
        var companyBadge = NormalizeCompanyBadge(
            request.CompanyBadge,
            request.CompanyBadgeProof,
            playerId);

        if (registry.TryGet(Context.ConnectionId, out var previous) && previous is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous.RoomId);
            registry.Remove(Context.ConnectionId);
            await Clients.Group(previous.RoomId).SendAsync("playerLeft", previous.PlayerId);
            await BroadcastTrafficAuthorityAsync(previous.RoomId);
        }

        var presence = registry.Upsert(
            Context.ConnectionId,
            roomId,
            playerId,
            displayName,
            mapName,
            mapCompatibilityId,
            compatibility,
            companyBadge);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.OthersInGroup(roomId).SendAsync("playerJoined", presence);

        var authorityPlayerId = registry.GetTrafficAuthorityPlayerId(roomId);
        await Clients.Group(roomId).SendAsync("trafficAuthorityChanged", authorityPlayerId);

        return new RoomSnapshot(
            roomId,
            registry.GetRoomPlayers(roomId),
            authorityPlayerId);
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
        await BroadcastTrafficAuthorityAsync(presence.RoomId);
    }

    public async Task PublishTelemetry(VehicleTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before publishing telemetry.");
        }

        ValidateTelemetry(telemetry);

        var serverReceivedAtUtc = DateTimeOffset.UtcNow;
        var sourceTimestampMs =
            telemetry.SourceTimestampUnixMilliseconds ??
            telemetry.Timestamp.ToUnixTimeMilliseconds();
        var sourceSkewMs =
            Math.Abs(
                (double)sourceTimestampMs -
                serverReceivedAtUtc.ToUnixTimeMilliseconds());
        if (!double.IsFinite(sourceSkewMs) ||
            sourceSkewMs > TimeSpan.FromHours(24).TotalMilliseconds)
        {
            sourceTimestampMs =
                serverReceivedAtUtc.ToUnixTimeMilliseconds();
        }

        var safeTelemetry = telemetry with
        {
            PlayerId = presence.PlayerId,
            Timestamp = serverReceivedAtUtc,
            SourceTimestampUnixMilliseconds = sourceTimestampMs,
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

        var updatedPresence =
            registry.UpdateTelemetryIdentity(
                Context.ConnectionId,
                safeTelemetry.MapName,
                safeTelemetry.MapCompatibilityId,
                safeTelemetry.VehiclePath,
                safeTelemetry.VehicleCompatibilityId,
                safeTelemetry.HofName,
                safeTelemetry.HofCompatibilityId);
        var currentPresence = updatedPresence ?? presence;

        if (updatedPresence is not null)
        {
            await Clients.Group(presence.RoomId).SendAsync("playerPresenceChanged", updatedPresence);
        }

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("telemetry", new PlayerTelemetryFrame(currentPresence, safeTelemetry));
    }

    public async Task PublishTrafficSnapshot(TrafficSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before publishing traffic.");
        }

        if (!registry.IsTrafficAuthority(Context.ConnectionId))
        {
            throw new HubException("Only the room traffic authority can publish traffic.");
        }

        if (snapshot.Sequence < 0 || snapshot.Vehicles is null || snapshot.Vehicles.Count > MaxTrafficVehicles)
        {
            throw new HubException("Invalid traffic snapshot.");
        }

        var mapName = NormalizeOptional(snapshot.MapName, MaxMapNameLength, "traffic map name");
        var mapCompatibilityId = NormalizeOptional(
            snapshot.MapCompatibilityId,
            MaxMapCompatibilityIdLength,
            "traffic map compatibility id");

        if (!MapsMatch(presence, mapName, mapCompatibilityId))
        {
            throw new HubException("Traffic snapshot does not match the authority map.");
        }

        var vehicles = snapshot.Vehicles
            .Select(NormalizeTrafficVehicle)
            .ToArray();

        var safeSnapshot = new TrafficSnapshot(
            presence.PlayerId,
            snapshot.Sequence,
            DateTimeOffset.UtcNow,
            mapName ?? presence.MapName,
            mapCompatibilityId ?? presence.MapCompatibilityId,
            vehicles);

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("trafficSnapshot", safeSnapshot);
    }

    public Task NavBrPing() => Task.CompletedTask;

    public async Task UpdateClientStatus(bool voiceEnabled, int? latencyMs)
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before updating client status.");
        }

        if (latencyMs is < 0 or > 5000)
        {
            throw new HubException("Invalid client latency.");
        }

        var updated = registry.UpdateClientStatus(
            Context.ConnectionId,
            voiceEnabled,
            latencyMs);
        if (updated is null)
        {
            return;
        }

        await Clients
            .Group(presence.RoomId)
            .SendAsync("playerPresenceChanged", updated);
    }

    public async Task UpdateOpenOmsiTransport(OpenOmsiTransportDescriptor? transport)
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before updating openOMSI transport.");
        }

        if (transport is not null && !transport.IsValid)
        {
            throw new HubException("Invalid openOMSI transport descriptor.");
        }

        var updated = registry.UpdateOpenOmsiTransport(
            Context.ConnectionId,
            transport);
        if (updated is null)
        {
            return;
        }

        await Clients
            .Group(presence.RoomId)
            .SendAsync("playerPresenceChanged", updated);
    }

    public async Task UpdatePhysicalVehicleStatus(string[]? physicalVehiclePlayerIds)
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before updating physical vehicle status.");
        }

        var ids = physicalVehiclePlayerIds ?? Array.Empty<string>();
        if (ids.Length > 32 ||
            ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128))
        {
            throw new HubException("Invalid physical vehicle status.");
        }

        var updated = registry.UpdatePhysicalVehicleStatus(
            Context.ConnectionId,
            ids);
        if (updated is null)
        {
            return;
        }

        await Clients
            .Group(presence.RoomId)
            .SendAsync("playerPresenceChanged", updated);
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

    public async Task PublishVoiceFrame(long sequence, byte[] opusPayload, string? channel = null)
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

        var normalizedChannel = NormalizeVoiceChannel(channel);
        var frame = new VoiceFrame(
            presence.PlayerId,
            sequence,
            opusPayload,
            DateTimeOffset.UtcNow,
            normalizedChannel);

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
            await BroadcastTrafficAuthorityAsync(presence.RoomId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Task BroadcastTrafficAuthorityAsync(string roomId) =>
        Clients.Group(roomId).SendAsync(
            "trafficAuthorityChanged",
            registry.GetTrafficAuthorityPlayerId(roomId));

    private static bool MapsMatch(
        PlayerPresence presence,
        string? mapName,
        string? mapCompatibilityId)
    {
        if (!string.IsNullOrWhiteSpace(presence.MapCompatibilityId) &&
            !string.IsNullOrWhiteSpace(mapCompatibilityId))
        {
            return string.Equals(
                presence.MapCompatibilityId,
                mapCompatibilityId,
                StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(presence.MapName) &&
            !string.IsNullOrWhiteSpace(mapName))
        {
            return string.Equals(
                presence.MapName,
                mapName,
                StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static TrafficVehicleState NormalizeTrafficVehicle(TrafficVehicleState vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        var trafficId = NormalizeRequired(vehicle.TrafficId, MaxTrafficIdLength, "traffic id");
        var vehiclePath = NormalizeOptional(vehicle.VehiclePath, MaxVehiclePathLength, "traffic vehicle path");
        var compatibilityId = NormalizeOptional(
            vehicle.VehicleCompatibilityId,
            MaxCompatibilityIdLength,
            "traffic vehicle compatibility id");

        ValidateTrafficFinite(vehicle.X, "traffic X");
        ValidateTrafficFinite(vehicle.Y, "traffic Y");
        ValidateTrafficFinite(vehicle.Z, "traffic Z");
        ValidateTrafficFinite(vehicle.LocalX, "traffic local X");
        ValidateTrafficFinite(vehicle.LocalY, "traffic local Y");
        ValidateTrafficFinite(vehicle.LocalZ, "traffic local Z");
        ValidateTrafficFinite(vehicle.RotationX, "traffic rotation X");
        ValidateTrafficFinite(vehicle.RotationY, "traffic rotation Y");
        ValidateTrafficFinite(vehicle.RotationZ, "traffic rotation Z");
        ValidateTrafficFinite(vehicle.RotationW, "traffic rotation W");
        ValidateTrafficFinite(vehicle.SpeedKph, "traffic speed");

        if (vehicle.SpeedKph is < -20d or > 250d)
        {
            throw new HubException("Traffic vehicle contains invalid speed.");
        }

        return vehicle with
        {
            TrafficId = trafficId,
            VehiclePath = vehiclePath,
            VehicleCompatibilityId = compatibilityId
        };
    }

    private static void ValidateTrafficFinite(double value, string fieldName)
    {
        if (!double.IsFinite(value))
        {
            throw new HubException($"Traffic snapshot contains invalid {fieldName}.");
        }
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

    private static CompanyEmployeeBadge? NormalizeCompanyBadge(
        CompanyEmployeeBadge? badge,
        CompanyBadgePresenceProof? proof,
        string sessionPlayerId)
    {
        if (badge is null)
        {
            if (proof is not null)
            {
                throw new HubException("Company badge proof has no badge.");
            }

            return null;
        }

        if (proof is null)
        {
            throw new HubException("Company badge requires identity proof.");
        }

        var companyId = NormalizeRequired(
            badge.CompanyId,
            MaxCompanyIdLength,
            "company id");
        var companyName = NormalizeRequired(
            badge.CompanyName,
            MaxCompanyNameLength,
            "company name");
        var companyShortName = NormalizeRequired(
            badge.CompanyShortName,
            MaxCompanyShortNameLength,
            "company short name");
        var employeeNumber = NormalizeRequired(
            badge.EmployeeNumber,
            MaxEmployeeNumberLength,
            "employee number");
        var badgePlayerId = NormalizeRequired(
            badge.PlayerId,
            64,
            "company badge player id");
        var badgeDisplayName = NormalizeRequired(
            badge.DisplayName,
            64,
            "company badge display name");

        if (!Enum.IsDefined(typeof(CompanyRole), badge.Role) ||
            (badge.Permissions & ~CompanyPermission.All) != 0)
        {
            throw new HubException("Invalid company badge role or permissions.");
        }

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset proofTime;
        try
        {
            proofTime = DateTimeOffset.FromUnixTimeMilliseconds(
                proof.TimestampUnixMilliseconds);
        }
        catch
        {
            throw new HubException("Invalid company badge proof timestamp.");
        }

        if (Math.Abs((now - proofTime).TotalMinutes) > 5d ||
            badge.IssuedAtUtc > now + TimeSpan.FromMinutes(5d))
        {
            throw new HubException("Expired or invalid company badge proof.");
        }

        var normalized = badge with
        {
            CompanyId = companyId,
            CompanyName = companyName,
            CompanyShortName = companyShortName,
            PlayerId = badgePlayerId,
            DisplayName = badgeDisplayName,
            EmployeeNumber = employeeNumber
        };

        if (!CompanyBadgePresenceSignatures.Verify(
                sessionPlayerId,
                normalized,
                proof))
        {
            throw new HubException("Invalid company badge identity proof.");
        }

        return normalized;
    }

    private static string NormalizeVoiceChannel(string? value)
    {
        var normalized = (value ?? "general").Trim().ToLowerInvariant();
        return normalized switch
        {
            "" or "general" => "general",
            "company" or "team" => "company",
            "dispatch" or "cco" => "dispatch",
            "proximity" => "proximity",
            _ => throw new HubException("Invalid voice channel.")
        };
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
        ValidateOptionalFinite(telemetry.LocalX, "local X");
        ValidateOptionalFinite(telemetry.LocalY, "local Y");
        ValidateOptionalFinite(telemetry.LocalZ, "local Z");
        ValidateOptionalFinite(telemetry.RotationX, "rotation X");
        ValidateOptionalFinite(telemetry.RotationY, "rotation Y");
        ValidateOptionalFinite(telemetry.RotationZ, "rotation Z");
        ValidateOptionalFinite(telemetry.RotationW, "rotation W");
        ValidateOptionalVector(
            telemetry.VelocityX,
            telemetry.VelocityY,
            telemetry.VelocityZ,
            150d,
            "physical velocity");
        ValidateOptionalVector(
            telemetry.AccelerationLocalX,
            telemetry.AccelerationLocalY,
            telemetry.AccelerationLocalZ,
            100d,
            "local physical acceleration");
        ValidateRearSections(telemetry.RearSections);

        if (telemetry.FuelPercent is < 0 or > 100 ||
            telemetry.ThrottlePercent is < 0 or > 100 ||
            telemetry.BrakePercent is < 0 or > 100)
        {
            throw new HubException("Telemetry contains invalid percentage values.");
        }

        if ((telemetry.GridX is null) != (telemetry.GridY is null))
        {
            throw new HubException("Telemetry OMSI grid must include both GridX and GridY.");
        }

        if (telemetry.GridX is int gridX &&
            telemetry.GridY is int gridY &&
            (Math.Abs((long)gridX) > 100_000L ||
             Math.Abs((long)gridY) > 100_000L))
        {
            throw new HubException("Telemetry contains invalid OMSI grid coordinates.");
        }

        if ((telemetry.PhysicalGridX is null) !=
            (telemetry.PhysicalGridY is null))
        {
            throw new HubException(
                "Telemetry physical OMSI grid must include both coordinates.");
        }

        if (telemetry.PhysicalGridX is int physicalGridX &&
            telemetry.PhysicalGridY is int physicalGridY &&
            (Math.Abs((long)physicalGridX) > 100_000L ||
             Math.Abs((long)physicalGridY) > 100_000L))
        {
            throw new HubException(
                "Telemetry contains invalid physical OMSI grid coordinates.");
        }

        if (telemetry.MapTileIndex is int mapTileIndex &&
            mapTileIndex is < 0 or > 200_000)
        {
            throw new HubException("Telemetry contains invalid OMSI Kachel index.");
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

    private static void ValidateRearSections(
        VehicleSectionPose[]? sections)
    {
        if (sections is null)
        {
            return;
        }

        if (sections.Length > 3)
        {
            throw new HubException(
                "Telemetry contains too many articulated vehicle sections.");
        }

        foreach (var section in sections)
        {
            if (!double.IsFinite(section.LocalX) ||
                !double.IsFinite(section.LocalY) ||
                !double.IsFinite(section.LocalZ) ||
                !double.IsFinite(section.RotationX) ||
                !double.IsFinite(section.RotationY) ||
                !double.IsFinite(section.RotationZ) ||
                !double.IsFinite(section.RotationW) ||
                Math.Abs(section.LocalX) > 100_000d ||
                Math.Abs(section.LocalY) > 100_000d ||
                Math.Abs(section.LocalZ) > 100_000d ||
                Math.Abs((long)section.GridX) > 100_000L ||
                Math.Abs((long)section.GridY) > 100_000L ||
                section.MapTileIndex is < 0 or > 200_000)
            {
                throw new HubException(
                    "Telemetry contains an invalid articulated vehicle section.");
            }

            var quaternionLengthSquared =
                section.RotationX * section.RotationX +
                section.RotationY * section.RotationY +
                section.RotationZ * section.RotationZ +
                section.RotationW * section.RotationW;
            if (!double.IsFinite(quaternionLengthSquared) ||
                quaternionLengthSquared < 0.00000001d)
            {
                throw new HubException(
                    "Telemetry contains an invalid articulated section rotation.");
            }
        }
    }

    private static void ValidateOptionalFinite(double? value, string fieldName)
    {
        if (value is double number && !double.IsFinite(number))
        {
            throw new HubException($"Telemetry contains invalid {fieldName}.");
        }
    }

    private static void ValidateOptionalVector(
        double? x,
        double? y,
        double? z,
        double maximumMagnitude,
        string fieldName)
    {
        if (x is null && y is null && z is null)
        {
            return;
        }

        if (x is not double valueX ||
            y is not double valueY ||
            z is not double valueZ ||
            !double.IsFinite(valueX) ||
            !double.IsFinite(valueY) ||
            !double.IsFinite(valueZ))
        {
            throw new HubException(
                $"Telemetry contains incomplete or invalid {fieldName}.");
        }

        var magnitudeSquared =
            valueX * valueX +
            valueY * valueY +
            valueZ * valueZ;
        if (!double.IsFinite(magnitudeSquared) ||
            magnitudeSquared > maximumMagnitude * maximumMagnitude)
        {
            throw new HubException(
                $"Telemetry contains out-of-range {fieldName}.");
        }
    }
}
