namespace NavBR.Client.Omsi;

public sealed record OmsiProcessInfo(
    int ProcessId,
    string ExecutablePath,
    string InstallDirectory,
    string FileVersion,
    string Sha256,
    string? RuntimeVersion = null)
{
    /// <summary>
    /// OMSI's own logfile is authoritative when available. Some patched or
    /// redistributed executables keep stale FileVersion metadata even though
    /// the running simulator reports another runtime version.
    /// </summary>
    public string EffectiveVersion =>
        string.IsNullOrWhiteSpace(RuntimeVersion)
            ? FileVersion
            : RuntimeVersion!;

    public bool IsOmsi22032 =>
        EffectiveVersion.Contains("2.2.032", StringComparison.OrdinalIgnoreCase) ||
        EffectiveVersion.Contains("2.2.32", StringComparison.OrdinalIgnoreCase);

    public bool IsOmsi23004Exact =>
        EffectiveVersion.Contains("2.3.004", StringComparison.OrdinalIgnoreCase) ||
        EffectiveVersion.Contains("2.3.4", StringComparison.OrdinalIgnoreCase);

    public bool IsTelemetrySupported => IsOmsi23004Exact || IsOmsi22032;

    // Mantido para compatibilidade com o código existente enquanto os perfis
    // 2.3.004 e 2.2.032 compartilham o mesmo leitor de telemetria.
    public bool IsOmsi23004 => IsTelemetrySupported;
}
