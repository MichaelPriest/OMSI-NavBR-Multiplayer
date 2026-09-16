using System.Diagnostics;

namespace NavBR.Client.Omsi;

internal sealed record OmsiLaunchResult(
    OmsiInstallationProfile Profile,
    int? ProcessId,
    bool AlreadyRunning,
    string ExecutablePath);

internal static class OmsiLauncherService
{
    public static OmsiLaunchResult Launch(
        OmsiInstallationProfile profile,
        bool preferExistingProcess = true)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(profile.InstallDirectory));
        var executable = Path.Combine(root, "Omsi.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException(
                "Omsi.exe was not found in the selected NavBR profile.",
                executable);
        }

        if (preferExistingProcess)
        {
            var existing = FindRunningOmsiForDirectory(root);
            if (existing is not null)
            {
                MarkUsed(profile);
                return new OmsiLaunchResult(
                    profile,
                    existing.Id,
                    AlreadyRunning: true,
                    executable);
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = root,
            UseShellExecute = true,
            Arguments = profile.LaunchArguments ?? string.Empty
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start OMSI.");

        MarkUsed(profile);
        return new OmsiLaunchResult(
            profile,
            process.Id,
            AlreadyRunning: false,
            executable);
    }

    public static Process? FindRunningOmsiForDirectory(string installDirectory)
    {
        var expectedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(installDirectory));

        foreach (var process in Process.GetProcessesByName("Omsi"))
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(path))
                {
                    process.Dispose();
                    continue;
                }

                var root = Path.TrimEndingDirectorySeparator(
                    Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty);
                if (string.Equals(root, expectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }
            catch
            {
                // Protected/exiting process; ignore and continue discovery.
            }

            process.Dispose();
        }

        return null;
    }

    private static void MarkUsed(OmsiInstallationProfile profile)
    {
        try
        {
            OmsiInstallationProfileStore.Upsert(profile with
            {
                LastUsedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch
        {
            // Launching OMSI must not fail because profile metadata could not be persisted.
        }
    }
}
