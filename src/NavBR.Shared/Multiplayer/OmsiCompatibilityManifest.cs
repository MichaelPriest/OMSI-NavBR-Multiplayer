namespace NavBR.Shared.Multiplayer;

public sealed record OmsiCompatibilityManifest(
    string? OmsiVersion,
    string? NavBRVersion,
    string? MapName,
    string? MapCompatibilityId,
    string? VehiclePath,
    string? VehicleCompatibilityId,
    string? HofName,
    string? HofCompatibilityId,
    int PluginProtocolVersion,
    string? PluginDeployment,
    IReadOnlyList<string>? Capabilities = null);

public enum CompatibilityIssueSeverity
{
    Info = 0,
    Warning = 1,
    Blocking = 2
}

public sealed record CompatibilityIssue(
    string Code,
    CompatibilityIssueSeverity Severity,
    string Message,
    string? LocalValue = null,
    string? RemoteValue = null);

public sealed record RoomCompatibilityReport(
    bool IsCompatible,
    IReadOnlyList<CompatibilityIssue> Issues)
{
    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Blocking);
}
