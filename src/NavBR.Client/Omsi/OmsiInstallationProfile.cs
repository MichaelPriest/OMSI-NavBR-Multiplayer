namespace NavBR.Client.Omsi;

internal sealed record OmsiInstallationProfile(
    string Id,
    string Name,
    string InstallDirectory,
    bool IsPreferred = false,
    string? LaunchArguments = null,
    string? ParentProfileId = null,
    string[]? EnabledMaps = null,
    string[]? EnabledVehicles = null,
    DateTimeOffset? LastUsedAtUtc = null)
{
    public string ExecutablePath => Path.Combine(InstallDirectory, "Omsi.exe");
}
