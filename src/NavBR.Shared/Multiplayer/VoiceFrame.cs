namespace NavBR.Shared.Multiplayer;

public sealed record VoiceFrame(
    string PlayerId,
    long Sequence,
    byte[] OpusPayload,
    DateTimeOffset TimestampUtc,
    string Channel = "general");
