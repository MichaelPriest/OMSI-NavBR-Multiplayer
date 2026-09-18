using System.Text.Json;
using System.Diagnostics;
using Microsoft.Win32;
using NavBR.Client.Diagnostics;
using NavBR.Client.Multiplayer;
using NavBR.Client.Overlay;
using NavBR.Client.Omsi;

namespace NavBR.Client;

public partial class MainWindow
{
    private object BuildWebSystemState()
    {
        var profiles = OmsiInstallationProfileStore.Load();
        var currentInstall = _currentOmsi?.InstallDirectory;
        var hudSettings = MultiplayerSettingsStore.Load();

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
            hud = new
            {
                enabled = hudSettings.DashboardEnabled,
                preset = hudSettings.DashboardPreset,
                theme = hudSettings.DashboardTheme,
                anchor = hudSettings.DashboardAnchor,
                scale = hudSettings.DashboardScale,
                width = hudSettings.DashboardWidth,
                height = hudSettings.DashboardHeight,
                opacity = hudSettings.DashboardOpacity,
                autoScale = hudSettings.DashboardAutoScale,
                showFuel = hudSettings.DashboardShowFuel,
                showPedals = hudSettings.DashboardShowPedals,
                showStatus = hudSettings.DashboardShowStatus,
                showMinimap = hudSettings.DashboardShowMinimap,
                showMultiplayer = hudSettings.DashboardShowMultiplayer,
                showAlerts = hudSettings.DashboardShowAlerts,
                showSideIndicators = hudSettings.DashboardShowSideIndicators,
                minimapScale = hudSettings.DashboardMinimapScale,
                multiplayerScale = hudSettings.DashboardMultiplayerScale,
                alertsScale = hudSettings.DashboardAlertsScale,
                sideIndicatorsScale = hudSettings.DashboardSideIndicatorsScale,
                presets = HudProfileCatalog.Presets
                    .Select(item => new
                    {
                        id = item.Id,
                        displayName = item.DisplayName,
                        width = item.Width,
                        scale = item.Scale,
                        opacity = item.Opacity,
                        showFuel = item.ShowFuel,
                        showPedals = item.ShowPedals,
                        showStatus = item.ShowStatus,
                        showMinimap = item.ShowMinimap,
                        showMultiplayer = item.ShowMultiplayer,
                        showAlerts = item.ShowAlerts,
                        showSideIndicators = item.ShowSideIndicators
                    })
                    .ToArray(),
                themes = HudProfileCatalog.Themes
                    .Select(item => new
                    {
                        id = item.Id,
                        displayName = item.DisplayName
                    })
                    .ToArray(),
                anchors = new[]
                {
                    new { id = "free", displayName = "Livre" },
                    new { id = "top-left", displayName = "Superior esquerdo" },
                    new { id = "top-center", displayName = "Superior centro" },
                    new { id = "top-right", displayName = "Superior direito" },
                    new { id = "bottom-left", displayName = "Inferior esquerdo" },
                    new { id = "bottom-center", displayName = "Inferior centro" },
                    new { id = "bottom-right", displayName = "Inferior direito" }
                }
            },
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

    private void SelectOmsiFolderFromWeb()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta que contém Omsi.exe",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var executable = Path.Combine(dialog.FolderName, "Omsi.exe");
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException(
                "Omsi.exe não foi encontrado na pasta selecionada.");
        }

        OmsiInstallationProfileStore.DiscoverAndMerge(dialog.FolderName);
    }

    private static void OpenOmsiProfileFolderFromWeb(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        var profile = OmsiInstallationProfileStore.Load()
            .FirstOrDefault(item =>
                string.Equals(
                    item.Id,
                    profileId.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new InvalidOperationException(
                "O perfil OMSI selecionado não existe mais.");
        }

        if (!Directory.Exists(profile.InstallDirectory))
        {
            throw new DirectoryNotFoundException(
                $"A pasta da instalação não existe mais: {profile.InstallDirectory}");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{profile.InstallDirectory}\"",
            UseShellExecute = true
        });
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

    private static void SaveHudSettingsFromWeb(JsonElement? payload)
    {
        var current = MultiplayerSettingsStore.Load();
        MultiplayerSettingsStore.Save(current with
        {
            DashboardSettingsVersion = 3,
            DashboardEnabled = GetWebPayloadBool(payload, "enabled"),
            DashboardPreset = HudProfileCatalog.ResolvePreset(
                GetWebPayloadString(payload, "preset")).Id,
            DashboardTheme = HudProfileCatalog.ResolveTheme(
                GetWebPayloadString(payload, "theme")).Id,
            DashboardAnchor = HudProfileCatalog.ResolveAnchor(
                GetWebPayloadString(payload, "anchor")),
            DashboardScale = Math.Clamp(
                GetWebPayloadDouble(payload, "scale") ?? current.DashboardScale,
                0.60d,
                1.80d),
            DashboardWidth = Math.Clamp(
                GetWebPayloadDouble(payload, "width") ?? current.DashboardWidth,
                280d,
                960d),
            DashboardHeight = Math.Clamp(
                GetWebPayloadDouble(payload, "height") ?? current.DashboardHeight,
                0d,
                720d),
            DashboardOpacity = Math.Clamp(
                GetWebPayloadDouble(payload, "opacity") ?? current.DashboardOpacity,
                0.35d,
                1d),
            DashboardAutoScale = GetWebPayloadBool(payload, "autoScale"),
            DashboardShowFuel = GetWebPayloadBool(payload, "showFuel"),
            DashboardShowPedals = GetWebPayloadBool(payload, "showPedals"),
            DashboardShowStatus = GetWebPayloadBool(payload, "showStatus"),
            DashboardShowMinimap = GetWebPayloadBool(payload, "showMinimap"),
            DashboardShowMultiplayer = GetWebPayloadBool(payload, "showMultiplayer"),
            DashboardShowAlerts = GetWebPayloadBool(payload, "showAlerts"),
            DashboardShowSideIndicators = GetWebPayloadBool(payload, "showSideIndicators"),
            DashboardMinimapScale = Math.Clamp(
                GetWebPayloadDouble(payload, "minimapScale") ?? current.DashboardMinimapScale,
                0.55d,
                2d),
            DashboardMultiplayerScale = Math.Clamp(
                GetWebPayloadDouble(payload, "multiplayerScale") ?? current.DashboardMultiplayerScale,
                0.55d,
                2d),
            DashboardAlertsScale = Math.Clamp(
                GetWebPayloadDouble(payload, "alertsScale") ?? current.DashboardAlertsScale,
                0.55d,
                2d),
            DashboardSideIndicatorsScale = Math.Clamp(
                GetWebPayloadDouble(payload, "sideIndicatorsScale") ?? current.DashboardSideIndicatorsScale,
                0.55d,
                2d)
        });
    }

    private static void ResetHudSettingsFromWeb()
    {
        var current = MultiplayerSettingsStore.Load();
        var reset = HudProfileCatalog.ApplyPreset(
            current,
            HudProfileCatalog.DefaultPreset) with
        {
            DashboardEnabled = true,
            DashboardTheme = HudProfileCatalog.DefaultTheme,
            DashboardAnchor = HudProfileCatalog.DefaultAnchor,
            DashboardHeight = 0d,
            DashboardAutoScale = true,
            DashboardMinimapScale = 1d,
            DashboardMultiplayerScale = 1d,
            DashboardAlertsScale = 1d,
            DashboardSideIndicatorsScale = 1d
        };
        MultiplayerSettingsStore.Save(reset);
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
