using System.Text.Json;
using System.Diagnostics;
using Microsoft.Win32;
using NavBR.Client.Diagnostics;
using NavBR.Client.Multiplayer;
using NavBR.Client.Overlay;
using NavBR.Client.Omsi;
using NavBR.Client.Windows;
using NavBR.Client.Operations;

namespace NavBR.Client;

public partial class MainWindow
{
    private string? _webOmsiLaunchNotice;
    private string? _webSessionHealthNotice;

    private object BuildWebSystemState()
    {
        var profiles = OmsiInstallationProfileStore.Load();
        var currentInstall = _currentOmsi?.InstallDirectory;
        var hudSettings = MultiplayerSettingsStore.Load();
        var alpha12Preferences = Alpha12PreferencesStore.Load();

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
            installationsNotice = _webOmsiLaunchNotice,
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
            },
            sessionHealthNotice = _webSessionHealthNotice,
            sessionHealth = BuildWebSessionHealthState(),
            legacyPreferences = new
            {
                firstRunCompleted = alpha12Preferences.FirstRunCompleted,
                advancedModeEnabled = alpha12Preferences.AdvancedModeEnabled,
                showDrivingTips = alpha12Preferences.ShowDrivingTips
            }
        };
    }

    private void DiscoverOmsiProfilesFromWeb(string? preferredPath)
    {
        _webOmsiLaunchNotice = null;
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

        var owner = GetPrimaryWebDialogOwner();
        if ((owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true)
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
        _webOmsiLaunchNotice = null;
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

        _webOmsiLaunchNotice = null;
        _ = OmsiLauncherService.Launch(profile);
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

    private object BuildWebSessionHealthState()
    {
        var session = DispatcherSessionFeed.Snapshot();
        var network = SessionNetworkQualityFeed.Snapshot();
        var plugin = (System.Windows.Application.Current as App)?.PluginBridge.GetConnectionInfo();
        var now = DateTimeOffset.UtcNow;
        double? freshnessSeconds = null;
        if (session.Connected && session.RemoteDrivers.Count > 0)
        {
            var newest = session.RemoteDrivers.Max(driver => driver.ReceivedAtUtc);
            freshnessSeconds = Math.Max(0d, (now - newest).TotalSeconds);
        }

        var networkReady = session.Connected &&
                           network.Samples >= 2 &&
                           network.RoundTripMs is not null;
        double? telemetryRateHz = networkReady
            ? network.Level switch
            {
                SessionNetworkQualityLevel.Poor => 1.5d,
                SessionNetworkQualityLevel.Degraded => 2.5d,
                _ => 4d
            }
            : null;

        return new
        {
            omsiActive = _lastTelemetry?.IsInGame == true,
            multiplayerConnected = session.Connected,
            pluginConnected = plugin?.IsConnected == true,
            pluginVersion = plugin?.PluginComponentVersion,
            remoteDrivers = session.Connected ? session.RemoteDrivers.Count : 0,
            remoteTelemetryAgeSeconds = freshnessSeconds,
            latencyMs = networkReady ? network.RoundTripMs : null,
            jitterMs = networkReady ? network.JitterMs : null,
            lossPercent = networkReady ? network.LossPercent : null as double?,
            telemetryRateHz,
            networkLevel = network.Level.ToString(),
            samples = network.Samples,
            updatedAtUtc = now
        };
    }

    private void ExportSessionHealthFromWeb()
    {
        _webSessionHealthNotice = null;
        var report = new
        {
            schema = "navbr-session-health",
            version = 1,
            exportedAtUtc = DateTimeOffset.UtcNow,
            metrics = BuildWebSessionHealthState(),
            note = "Contains only aggregate Session Health values visible in NavBR. No room password, token, room id, PlayerId, IP address or local filesystem path is exported."
        };

        var dialog = new SaveFileDialog
        {
            Title = "Exportar relatório sanitizado de saúde da sessão NavBR",
            Filter = "NavBR Session Health (*.navbr-health.json)|*.navbr-health.json|JSON (*.json)|*.json",
            FileName = $"navbr-session-health-{DateTime.Now:yyyyMMdd-HHmmss}.navbr-health.json",
            DefaultExt = ".json",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(
            dialog.FileName,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _webSessionHealthNotice = "Relatório sanitizado de saúde da sessão exportado com sucesso.";
    }

    private static void SaveLegacyPreferencesFromWeb(bool advancedModeEnabled, bool showDrivingTips)
    {
        var current = Alpha12PreferencesStore.Load();
        Alpha12PreferencesStore.Save(current with
        {
            AdvancedModeEnabled = advancedModeEnabled,
            ShowDrivingTips = showDrivingTips
        });
    }

    private static void CompleteFirstRunFromWeb()
    {
        var current = Alpha12PreferencesStore.Load();
        if (!current.FirstRunCompleted)
        {
            Alpha12PreferencesStore.Save(current with { FirstRunCompleted = true });
        }
    }

    private static void OpenFeedbackFromWeb(string? kind)
    {
        const string issues = "https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/issues";
        var url = (kind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "bug" => issues + "/new?template=bug.yml",
            "suggestion" => issues + "/new?template=suggestion.yml",
            "general" => issues + "/new?template=feedback.yml",
            _ => issues
        };

        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }
}
