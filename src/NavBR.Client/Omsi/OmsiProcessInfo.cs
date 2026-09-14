namespace NavBR.Client.Omsi;

public sealed record OmsiProcessInfo(
    int ProcessId,
    string ExecutablePath,
    string InstallDirectory,
    string FileVersion,
    string Sha256)
{
    public bool IsOmsi23004 =>
        FileVersion.Contains("2.3.004", StringComparison.OrdinalIgnoreCase) ||
        FileVersion.Contains("2.3.4", StringComparison.OrdinalIgnoreCase);
}
