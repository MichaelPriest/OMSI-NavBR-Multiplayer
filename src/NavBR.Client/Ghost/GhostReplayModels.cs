using NavBR.Shared.Telemetry;

namespace NavBR.Client.Ghost;

internal static class GhostReplayFormat
{
    public const int Version = 1;
    public const string Extension = ".navbrghost";
}

internal sealed record GhostReplayMetadata(
    int FormatVersion,
    string Name,
    DateTimeOffset RecordedAtUtc,
    string? MapName,
    string? MapCompatibilityId,
    string? VehicleName,
    string? VehiclePath,
    string? VehicleCompatibilityId,
    string? HofName,
    string? HofCompatibilityId,
    double DurationSeconds,
    int FrameCount);

internal sealed record GhostReplayFrame(
    long OffsetMilliseconds,
    VehicleTelemetry Telemetry);

internal sealed record GhostReplayDocument(
    GhostReplayMetadata Metadata,
    IReadOnlyList<GhostReplayFrame> Frames);
