namespace NavBR.Shared.Multiplayer;

public sealed record ChatMessage(
    string PlayerId,
    string DisplayName,
    string Text,
    DateTimeOffset TimestampUtc,
    bool IsSystem = false);
