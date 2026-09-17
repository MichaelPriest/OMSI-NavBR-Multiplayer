using Microsoft.AspNetCore.SignalR;
using NavBR.Shared.Multiplayer;

namespace NavBR.Server.Hubs;

public sealed partial class MultiplayerHub
{
    private const double MaxRoleplayCharacterSpeedMps = 12d;
    private const int MaxRoleplayCharacterIdLength = 512;
    private const int MaxRoleplayCharacterNameLength = 128;

    public async Task PublishRoleplayCharacter(RoleplayCharacterState character)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before publishing a roleplay character.");
        }

        ValidateRoleplayCharacter(character);

        var mapName = NormalizeOptional(character.MapName, MaxMapNameLength, "roleplay map name");
        var mapCompatibilityId = NormalizeOptional(
            character.MapCompatibilityId,
            MaxMapCompatibilityIdLength,
            "roleplay map compatibility id");

        if (!MapsMatch(presence, mapName, mapCompatibilityId))
        {
            throw new HubException("Roleplay character does not match the current room map.");
        }

        var safe = character with
        {
            PlayerId = presence.PlayerId,
            Timestamp = DateTimeOffset.UtcNow,
            MapName = mapName ?? presence.MapName,
            MapCompatibilityId = mapCompatibilityId ?? presence.MapCompatibilityId,
            SpeedMps = Math.Clamp(character.SpeedMps, 0d, MaxRoleplayCharacterSpeedMps),
            IsActive = true,
            CharacterId = NormalizeOptional(
                character.CharacterId,
                MaxRoleplayCharacterIdLength,
                "roleplay character id"),
            CharacterName = NormalizeOptional(
                character.CharacterName,
                MaxRoleplayCharacterNameLength,
                "roleplay character name"),
            // Human array indexes are local OMSI implementation details and
            // must never be treated as portable identifiers across clients.
            HumanIndex = null
        };

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("roleplayCharacter", new RoleplayCharacterFrame(presence, safe));
    }

    public async Task ReleaseRoleplayCharacter()
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            return;
        }

        await Clients
            .OthersInGroup(presence.RoomId)
            .SendAsync("roleplayCharacterRemoved", presence.PlayerId);
    }

    private static void ValidateRoleplayCharacter(RoleplayCharacterState character)
    {
        if (!double.IsFinite(character.LocalX) ||
            !double.IsFinite(character.LocalY) ||
            !double.IsFinite(character.LocalZ) ||
            !double.IsFinite(character.HeadingDegrees) ||
            !double.IsFinite(character.SpeedMps))
        {
            throw new HubException("Roleplay character contains invalid numeric values.");
        }

        _ = NormalizeOptional(
            character.CharacterId,
            MaxRoleplayCharacterIdLength,
            "roleplay character id");
        _ = NormalizeOptional(
            character.CharacterName,
            MaxRoleplayCharacterNameLength,
            "roleplay character name");

        if (Math.Abs(character.LocalX) > 100000d ||
            Math.Abs(character.LocalY) > 100000d ||
            Math.Abs(character.LocalZ) > 100000d ||
            character.SpeedMps is < 0d or > MaxRoleplayCharacterSpeedMps ||
            !Enum.IsDefined(character.Activity))
        {
            throw new HubException("Roleplay character is outside accepted bounds.");
        }
    }
}
