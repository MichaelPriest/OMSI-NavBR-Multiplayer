using NavBR.Shared.Network;

namespace NavBR.Shared.Multiplayer;

public sealed record JoinRoomRequest(
    string RoomId,
    string PlayerId,
    string DisplayName,
    string? MapName,
    string? MapCompatibilityId = null,
    OmsiCompatibilityManifest? Compatibility = null,
    string? RoomPassword = null,
    bool CreatePrivateRoom = false,
    CompanyEmployeeBadge? CompanyBadge = null,
    CompanyBadgePresenceProof? CompanyBadgeProof = null);
