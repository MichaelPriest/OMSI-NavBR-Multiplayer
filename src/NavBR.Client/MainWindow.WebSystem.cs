using NavBR.Client.Diagnostics;
using NavBR.Client.Omsi;

namespace NavBR.Client;

public partial class MainWindow
{
    private object BuildWebSystemState()
    {
        var profiles = OmsiInstallationProfileStore.Load();
        var currentInstall = _currentOmsi?.InstallDirectory;

        FileInfo? logInfo = null;
        try
        {
            if (File.Exists(NavBRAppLog.LogPath))
            {
                logInfo = new FileInfo(NavBRAppLog.LogPath);
            }
        }
        catch
        {
            logInfo = null;
        }

        return new
        {
            installations = profiles
                .Select(profile => new
                {
                    id = profile.Id,
                    name = profile.Name,
                    installDirectory = profile.InstallDirectory,
                    executablePath = profile.ExecutablePath,
                    executableExists = File.Exists(profile.ExecutablePath),
                    isPreferred = profile.IsPreferred,
                    launchArguments = profile.LaunchArguments,
                    lastUsedAtUtc = profile.LastUsedAtUtc,
                    isRunning = !string.IsNullOrWhiteSpace(currentInstall) &&
                                string.Equals(
                                    Path.TrimEndingDirectorySeparator(currentInstall),
                                    Path.TrimEndingDirectorySeparator(profile.InstallDirectory),
                                    StringComparison.OrdinalIgnoreCase)
                })
                .ToArray(),
            diagnostics = new
            {
                enabled = DiagnosticsConsentStore.IsEnabled,
                logPath = NavBRAppLog.LogPath,
                logExists = logInfo is not null,
                logSizeBytes = logInfo?.Length ?? 0L,
                logUpdatedAtUtc = logInfo?.LastWriteTimeUtc
            }
        };
    }

    private void DiscoverOmsiProfilesFromWeb(string? preferredPath)
    {
        OmsiInstallationProfileStore.DiscoverAndMerge(
            string.IsNullOrWhiteSpace(preferredPath) ? _currentOmsi?.InstallDirectory : preferredPath.Trim());
    }

    private void LaunchOmsiProfileFromWeb(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var profile = OmsiInstallationProfileStore.Load()
            .FirstOrDefault(item => string.Equals(item.Id, profileId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new InvalidOperationException("O perfil OMSI selecionado não existe mais.");
        }

        var result = OmsiLauncherService.Launch(profile);
        StatusText.Text = result.AlreadyRunning
            ? $"OMSI já está em execução • {profile.Name}"
            : $"OMSI iniciado • {profile.Name}";
        _ = RefreshOmsiStatusAsync();
    }

    private static void SetPreferredOmsiProfileFromWeb(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var profile = OmsiInstallationProfileStore.Load()
            .FirstOrDefault(item => string.Equals(item.Id, profileId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            OmsiInstallationProfileStore.Upsert(profile with { IsPreferred = true });
        }
    }

    private static void UpdateOmsiProfileFromWeb(
        string? profileId,
        string? name,
        string? launchArguments)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var profile = OmsiInstallationProfileStore.Load()
            .FirstOrDefault(item => string.Equals(item.Id, profileId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        OmsiInstallationProfileStore.Upsert(profile with
        {
            Name = string.IsNullOrWhiteSpace(name) ? profile.Name : name.Trim(),
            LaunchArguments = string.IsNullOrWhiteSpace(launchArguments)
                ? null
                : launchArguments.Trim()
        });
    }

    private static void RemoveOmsiProfileFromWeb(string? profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            OmsiInstallationProfileStore.Remove(profileId.Trim());
        }
    }

    private static void SetDiagnosticsEnabledFromWeb(bool enabled)
    {
        DiagnosticsConsentStore.SetEnabled(enabled);
        RemoteDiagnosticsService.OnConsentChanged(enabled);
    }

    private static Task FlushDiagnosticsFromWebAsync() =>
        RemoteDiagnosticsService.TryFlushAsync();

    private static void PurgeDiagnosticsFromWeb() =>
        RemoteDiagnosticsService.PurgeQueuedEvents();
}
