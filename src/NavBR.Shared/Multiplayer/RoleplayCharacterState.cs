namespace NavBR.Shared.Multiplayer;

public enum RoleplayCharacterActivity
{
    Idle = 0,
    Walking = 1,
    Running = 2
}

public sealed record RoleplayCharacterState(
    string PlayerId,
    DateTimeOffset Timestamp,
    string? MapName,
    string? MapCompatibilityId,
    double LocalX,
    double LocalY,
    double LocalZ,
    double HeadingDegrees,
    double SpeedMps,
    RoleplayCharacterActivity Activity,
    bool IsActive,
    int? HumanIndex = null);
