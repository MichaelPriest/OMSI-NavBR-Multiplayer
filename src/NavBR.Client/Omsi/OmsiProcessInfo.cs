namespace NavBR.Client.Omsi;

public sealed record OmsiProcessInfo(
    int ProcessId,
    string ExecutablePath,
    string InstallDirectory,
    string FileVersion);
