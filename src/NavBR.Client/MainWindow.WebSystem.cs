using System.Text.Json;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using NavBR.Client.Diagnostics;
using NavBR.Client.Multiplayer;
using NavBR.Client.Overlay;
using NavBR.Client.Omsi;
using NavBR.Client.Windows;
using NavBR.Client.Operations;
using NavBR.Client.PluginInstaller;

using NavBR.Shared.OpenOmsi;
using NavBR.Shared.PluginBridge;
namespace NavBR.Client;

public partial class MainWindow
{
    private string? _webOmsiLaunchNotice;
    private string? _webSessionHealthNotice;
    private const long OmsiProcessProbeCacheMs = 3_000;
    private long _webOmsiProcessProbeTickMs;
    private bool _webOmsiProcessRunningCached;
    private long _webOmsiMemoryProbeTickMs;
    private OmsiMemoryProbe? _webOmsiMemoryProbeCached;

    private object? BuildWebOmsiMemoryState()
    {
        var omsi = _currentOmsi;
        if (omsi is null || omsi.ProcessId <= 0)
        {
            _webOmsiMemoryProbeCached = null;
            _webOmsiMemoryProbeTickMs = 0;
            return null;
        }

        var nowTick = Environment.TickCount64;
        if (_webOmsiMemoryProbeCached is not null &&
            _webOmsiMemoryProbeCached.ProcessId == omsi.ProcessId &&
            _webOmsiMemoryProbeTickMs > 0 &&
            nowTick >= _webOmsiMemoryProbeTickMs &&
            nowTick - _webOmsiMemoryProbeTickMs < 2_000)
        {
            return _webOmsiMemoryProbeCached.ToWebState();
        }

        try
        {
            using var process = Process.GetProcessById(omsi.ProcessId);
            process.Refresh();

            var privateBytes = Math.Max(0L, process.PrivateMemorySize64);
            var workingSetBytes = Math.Max(0L, process.WorkingSet64);
            var peakWorkingSetBytes = Math.Max(0L, process.PeakWorkingSet64);
            var privateMiB = privateBytes / (1024d * 1024d);

            // This is intentionally an advisory pressure scale, not a claim
            // about the exact virtual-address limit of the user's OMSI build.
            // It lets NavBR warn early without modifying or trimming OMSI memory.
            var level = privateMiB switch
            {
                >= 2_800d => "critical",
                >= 2_200d => "high",
                >= 1_600d => "elevated",
                _ => "normal"
            };

            var probe = new OmsiMemoryProbe(
                omsi.ProcessId,
                privateBytes,
                workingSetBytes,
                peakWorkingSetBytes,
                level,
                DateTimeOffset.UtcNow);

            _webOmsiMemoryProbeCached = probe;
            _webOmsiMemoryProbeTickMs = nowTick;
            return probe.ToWebState();
        }
        catch (ArgumentException)
        {
            _webOmsiMemoryProbeCached = null;
            _webOmsiMemoryProbeTickMs = nowTick;
            return null;
        }
        catch (InvalidOperationException)
        {
            _webOmsiMemoryProbeCached = null;
            _webOmsiMemoryProbeTickMs = nowTick;
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            _webOmsiMemoryProbeCached = null;
            _webOmsiMemoryProbeTickMs = nowTick;
            return null;
        }
    }

    private static object? BuildApplicationUpdateState()
    {
        if (Application.Current is not App app)
        {
            return null;
        }

        var update = app.AutoUpdater.GetSnapshot();
        return new
        {
            status = update.Status,
            currentVersion = update.CurrentVersion,
            availableVersion = update.AvailableVersion,
            releaseUrl = update.ReleaseUrl,
            progressPercent = update.ProgressPercent,
            updateAvailable = update.UpdateAvailable,
            readyToInstall = update.ReadyToInstall,
            checkedAtUtc = update.CheckedAtUtc,
            message = update.Message
        };
    }

    private object BuildWebSystemState(
        MultiplayerSettings? hudSettings = null,
        string? scope = null)
    {
        hudSettings ??= MultiplayerSettingsStore.Load();

        var requiresDetailedSystemState =
            string.IsNullOrWhiteSpace(scope) ||
            string.Equals(scope, "hud", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scope, "diagnostics", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scope, "settings", StringComparison.OrdinalIgnoreCase);
        if (!requiresDetailedSystemState)
        {
            // Most WinUI pages only consume the runtime cadence and Session
            // Health fields rendered by MainWindow. Avoid probing plugin files,
            // OMSI profiles, logs, Mobile Companion and the HUD catalog on
            // every background state poll when those modules are not visible.
            return new
            {
                installationsNotice = (string?)null,
                runtimeHost = new
                {
                    nativeHostMode = _nativeHostMode,
                    telemetryPollIntervalMilliseconds = _telemetryPollIntervalMs,
                    telemetryLastReadMilliseconds = _lastTelemetryPollMilliseconds,
                    telemetryAverageReadMilliseconds = _averageTelemetryPollMilliseconds,
                    hudRefreshIntervalMilliseconds = _hudRefreshIntervalMs
                },
                applicationUpdate = BuildApplicationUpdateState(),
                mobileCompanion = (object?)null,
                pluginInstallation = (object?)null,
                openOmsiLanGateway = (object?)null,
                installations = (object?)null,
                hud = (object?)null,
                diagnostics = (object?)null,
                sessionHealthNotice = _webSessionHealthNotice,
                sessionHealth = BuildWebSessionHealthState(
                    hudSettings.PerformanceProfile),
                legacyPreferences = (object?)null
            };
        }

        var profiles = OmsiInstallationProfileStore.Load();
        var currentInstall = _currentOmsi?.InstallDirectory;
        var alpha12Preferences = Alpha12PreferencesStore.Load();
        var pluginOmsiRoot = ResolveConfiguredOmsiRootForPlugin(profiles);
        var pluginInstall = GetPluginInstallDiagnostics(pluginOmsiRoot);
        var omsiRunningForPluginUpdate = IsOmsiProcessRunningForPluginUpdate();
        var openOmsiLanGateway =
            OpenOmsiLanGateway.Shared.GetStatus();
        var openOmsiLanRuntime =
            OpenOmsiLanRuntimeStatusReader.Read(
                _openOmsiProcessId ??
                OpenOmsiEnvironmentLocator.GetRunningProcessId(),
                _openOmsiInstanceId);
        var openOmsiRuntimePeers =
            openOmsiLanRuntime?.Players.ToDictionary(
                player => player.Id) ??
            new Dictionary<uint, OpenOmsiLanRuntimePeer>();
        var pluginInstallBlockReason =
            !OmsiPluginInstallationService.HasEmbeddedPackage
                ? "package-missing"
                : string.IsNullOrWhiteSpace(pluginOmsiRoot)
                    ? "omsi-not-found"
                    : omsiRunningForPluginUpdate
                        ? "omsi-running"
                        : null;

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
            runtimeHost = new
            {
                nativeHostMode = _nativeHostMode,
                telemetryPollIntervalMilliseconds = _telemetryPollIntervalMs,
                    telemetryLastReadMilliseconds = _lastTelemetryPollMilliseconds,
                    telemetryAverageReadMilliseconds = _averageTelemetryPollMilliseconds,
                hudRefreshIntervalMilliseconds = _hudRefreshIntervalMs
            },
            applicationUpdate = BuildApplicationUpdateState(),
            mobileCompanion = BuildMobileCompanionDesktopState(),
            pluginInstallation = new
            {
                state = pluginInstall.State.ToLowerInvariant(),
                requiredFilesFound = pluginInstall.RequiredFilesFound,
                requiredFilesTotal = 3,
                verifiedFiles = pluginInstall.VerifiedFiles,
                manifestPresent = string.Equals(pluginInstall.Manifest, "YES", StringComparison.OrdinalIgnoreCase),
                pluginsDirectory = pluginInstall.DisplayPath,
                omsiRoot = pluginOmsiRoot,
                embeddedPackageAvailable = OmsiPluginInstallationService.HasEmbeddedPackage,
                installAvailable = pluginInstallBlockReason is null,
                installBlockReason = pluginInstallBlockReason,
                verificationAvailable =
                    OmsiPluginInstallationService.HasEmbeddedPackage &&
                    !string.IsNullOrWhiteSpace(pluginOmsiRoot),
                updateRequired = pluginInstall.UpdateRequired,
                autoUpdatePending =
                    Application.Current is App app &&
                    app.IsPluginUpdateScheduledFor(pluginOmsiRoot),
                expectedVersion = pluginInstall.ExpectedVersion,
                installedVersion = pluginInstall.InstalledVersion,
                checkedAtUtc = pluginInstall.CheckedAtUtc,
                message = pluginInstall.Message,
                files = pluginInstall.Files.Select(file => new
                {
                    name = file.Name,
                    exists = file.Exists,
                    hashMatches = file.HashMatches
                }).ToArray(),
                omsiRunning = omsiRunningForPluginUpdate
            },
            openOmsiLanGateway = new
            {
                running = openOmsiLanGateway.Running,
                port = openOmsiLanGateway.Port,
                joinTarget = openOmsiLanGateway.Port is int gatewayPort
                    ? $"127.0.0.1:{gatewayPort}"
                    : null,
                clientConnected = openOmsiLanGateway.ClientConnected,
                clientName = openOmsiLanGateway.ClientName,
                map = openOmsiLanGateway.Map,
                vehiclePath = openOmsiLanGateway.VehiclePath,
                remotePlayers = openOmsiLanGateway.RemotePlayers,
                runtimeStatusAvailable =
                    openOmsiLanRuntime is not null,
                runtimeStatusFresh =
                    openOmsiLanRuntime?.Fresh == true,
                runtimeConnected =
                    openOmsiLanRuntime?.Connected == true,
                runtimeStatusPath =
                    openOmsiLanRuntime?.SourcePath,
                runtimeUpdatedAtUtc =
                    openOmsiLanRuntime?.UpdatedUtc,
                drawnRemotePlayers =
                    openOmsiLanRuntime?.Fresh == true &&
                    openOmsiLanRuntime.Connected
                        ? openOmsiLanGateway.Remotes.Count(remote =>
                            openOmsiRuntimePeers.TryGetValue(
                                remote.LanId,
                                out var runtimePeer) &&
                            runtimePeer.Drawn)
                        : 0,
                remotes = openOmsiLanGateway.Remotes.Select(remote =>
                {
                    openOmsiRuntimePeers.TryGetValue(
                        remote.LanId,
                        out var runtimePeer);
                    var drawn =
                        openOmsiLanRuntime?.Fresh == true &&
                        openOmsiLanRuntime.Connected &&
                        runtimePeer?.Drawn == true;
                    var localVehicleCompatibilityId =
                        ResolveOpenOmsiVehicleCompatibilityId(
                            remote.VehiclePath);
                    var expectedVehicleCompatibilityId =
                        remote.ExpectedVehicleCompatibilityId;
                    var vehicleAssetStatus =
                        string.IsNullOrWhiteSpace(
                            remote.VehiclePath)
                            ? "missing-path"
                            : string.IsNullOrWhiteSpace(
                                localVehicleCompatibilityId)
                                ? "missing"
                                : string.IsNullOrWhiteSpace(
                                    expectedVehicleCompatibilityId)
                                    ? "unverified"
                                    : string.Equals(
                                        localVehicleCompatibilityId,
                                        expectedVehicleCompatibilityId,
                                        StringComparison.OrdinalIgnoreCase)
                                        ? "match"
                                        : "mismatch";
                    var status =
                        !remote.HasInfo
                            ? "waiting-info"
                            : !remote.HasState
                                ? "waiting-state"
                                : drawn
                                    ? "drawn"
                                    : openOmsiLanRuntime?.Fresh == true
                                        ? "sent-not-drawn"
                                        : "sent-unconfirmed";
                    return new
                    {
                        playerId = remote.PlayerId,
                        lanId = remote.LanId,
                        name = remote.Name,
                        vehiclePath =
                            remote.VehiclePath,
                        expectedVehicleCompatibilityId,
                        localVehicleCompatibilityId,
                        vehicleAssetStatus,
                        hasInfo = remote.HasInfo,
                        hasState = remote.HasState,
                        drawn,
                        materializationStatus =
                            status,
                        runtimeBus =
                            runtimePeer?.Bus,
                        runtimeName =
                            runtimePeer?.Name,
                        lastSeenUtc =
                            remote.LastSeenUtc
                    };
                }).ToArray(),
                localStateFrames = openOmsiLanGateway.LocalStateFrames,
                lastLocalStateSequence = openOmsiLanGateway.LastLocalStateSequence,
                localStateRateHz = openOmsiLanGateway.LocalStateRateHz,
                lastLocalStateUtc = openOmsiLanGateway.LastLocalStateUtc,
                localStateAgeMilliseconds =
                    openOmsiLanGateway.LastLocalStateUtc is DateTimeOffset lastStateUtc
                        ? Math.Max(
                            0d,
                            (DateTimeOffset.UtcNow - lastStateUtc)
                                .TotalMilliseconds)
                        : (double?)null,
                vehicleIdentityReady =
                    !string.IsNullOrWhiteSpace(
                        _lastTelemetry?.VehicleCompatibilityId),
                vehicleCompatibilityId =
                    _lastTelemetry?.VehicleCompatibilityId,
                lastClientPacketUtc = openOmsiLanGateway.LastClientPacketUtc,
                lastError = openOmsiLanGateway.LastError
            },
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
                previewActive = MultiplayerSettingsStore.IsHudPreviewActive,
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
                minimapStyle = NormalizeDashboardMinimapStyle(
                    hudSettings.DashboardMinimapStyle),
                multiplayerScale = hudSettings.DashboardMultiplayerScale,
                alertsScale = hudSettings.DashboardAlertsScale,
                sideIndicatorsScale = hudSettings.DashboardSideIndicatorsScale,
                presets = HudProfileCatalog.Presets
                    .Select(item => new
                    {
                        id = item.Id,
                        displayName = item.DisplayName,
                        themeId = item.ThemeId,
                        inspiration = item.Inspiration,
                        description = item.Description,
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
                    new { id = "custom", displayName = "Personalizada" },
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
            sessionHealth = BuildWebSessionHealthState(hudSettings.PerformanceProfile),
            legacyPreferences = new
            {
                firstRunCompleted = alpha12Preferences.FirstRunCompleted,
                advancedModeEnabled = alpha12Preferences.AdvancedModeEnabled,
                showDrivingTips = alpha12Preferences.ShowDrivingTips
            }
        };
    }

    private void VerifyAndUpdateOmsiPluginFromWeb()
    {
        var root = ResolveConfiguredOmsiRootForPlugin();
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                "Nenhuma instalação válida do OMSI 2 foi encontrada. Cadastre a pasta, o Omsi.exe ou um atalho .lnk/.url válido em Configurações primeiro.");
        }

        var result = OmsiPluginInstallationService.EnsureInstalledAtStartup(root);
        InvalidatePluginInstallDiagnosticsCache();
        switch (result.Status)
        {
            case "ready":
                _webOmsiLaunchNotice =
                    "Verificação concluída: os arquivos do plugin NavBR estão atualizados e conferem com o pacote desta versão.";
                return;
            case "installed":
                _webOmsiLaunchNotice =
                    "Plugin NavBR verificado e atualizado automaticamente. Inicie o OMSI para carregar os arquivos novos.";
                return;
            case "omsi-running":
                if (Application.Current is App app)
                {
                    app.SchedulePluginUpdateWhenOmsiCloses(root);
                }

                _webOmsiLaunchNotice =
                    "O plugin precisa de atualização, mas o OMSI está aberto. A atualização foi agendada e será aplicada automaticamente assim que o OMSI fechar.";
                return;
            case "untracked":
            case "conflict":
                _webOmsiLaunchNotice =
                    result.Message ??
                    "Há arquivos do plugin que não podem ser substituídos automaticamente porque não são rastreados pelo NavBR.";
                return;
            default:
                _webOmsiLaunchNotice =
                    $"A verificação do plugin retornou '{result.Status}': {result.Message ?? "sem detalhes"}.";
                return;
        }
    }

    private void InstallOmsiPluginFromWeb()
    {
        var root = ResolveConfiguredOmsiRootForPlugin();
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                "Nenhuma instalação válida do OMSI 2 foi encontrada. Cadastre a pasta, o Omsi.exe ou um atalho .lnk/.url válido em Configurações primeiro.");
        }

        var result = OmsiPluginInstallationService.InstallOrUpdate(root);
        InvalidatePluginInstallDiagnosticsCache();
        _webOmsiLaunchNotice =
            $"Plugin NavBR instalado/atualizado em {result.PluginsDirectory}. Inicie o OMSI para carregar o plugin.";
    }

    private string? ResolveConfiguredOmsiRootForPlugin(
        IReadOnlyList<OmsiInstallationProfile>? profiles = null)
    {
        var runningRoot = _currentOmsi?.InstallDirectory;
        if (!string.IsNullOrWhiteSpace(runningRoot))
        {
            try
            {
                var normalized = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(runningRoot));
                if (File.Exists(Path.Combine(normalized, "Omsi.exe")))
                {
                    return normalized;
                }
            }
            catch
            {
            }
        }

        profiles ??= OmsiInstallationProfileStore.Load();
        foreach (var profile in profiles
                     .Where(profile => File.Exists(profile.ExecutablePath))
                     .OrderByDescending(profile => profile.IsPreferred)
                     .ThenByDescending(profile => profile.LastUsedAtUtc ?? DateTimeOffset.MinValue))
        {
            try
            {
                return Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(profile.InstallDirectory));
            }
            catch
            {
            }
        }

        return OmsiPluginInstallationService.ResolveOmsiRoot();
    }

    private bool IsOmsiProcessRunningForPluginUpdate()
    {
        if (_currentOmsi is not null)
        {
            _webOmsiProcessRunningCached = true;
            _webOmsiProcessProbeTickMs = Environment.TickCount64;
            return true;
        }

        var nowTick = Environment.TickCount64;
        if (_webOmsiProcessProbeTickMs > 0 &&
            nowTick >= _webOmsiProcessProbeTickMs &&
            nowTick - _webOmsiProcessProbeTickMs < OmsiProcessProbeCacheMs)
        {
            return _webOmsiProcessRunningCached;
        }

        var running = false;
        var processes = Process.GetProcessesByName("Omsi");
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        running = true;
                        break;
                    }
                }
                catch
                {
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        _webOmsiProcessRunningCached = running;
        _webOmsiProcessProbeTickMs = nowTick;
        return running;
    }

    private void DiscoverOmsiProfilesFromWeb(string? preferredPath)
    {
        _webOmsiLaunchNotice = null;

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            var requestedResolved =
                OmsiInstallationLocator.TryResolveInstallDirectory(
                    preferredPath.Trim());
            if (requestedResolved is null)
            {
                _webOmsiLaunchNotice =
                    "O caminho informado não resolveu uma instalação válida do OMSI 2. Informe a pasta que contém Omsi.exe, o próprio Omsi.exe ou um atalho .lnk/.url válido.";
                return;
            }

            OmsiInstallationProfileStore.DiscoverAndMerge(requestedResolved);
            SetPreferredOmsiInstallDirectory(requestedResolved);
            _webOmsiLaunchNotice =
                $"Instalação OMSI reconhecida e selecionada em {requestedResolved}.";
            return;
        }

        var profiles = OmsiInstallationProfileStore.DiscoverAndMerge(
            _currentOmsi?.InstallDirectory);
        if (profiles.Count == 0)
        {
            _webOmsiLaunchNotice =
                "Nenhuma instalação válida do OMSI 2 foi localizada automaticamente.";
        }
    }

    private void SelectOmsiExecutableFromWeb()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione Omsi.exe ou um atalho do OMSI 2",
            Filter = "OMSI 2 (Omsi.exe;*.lnk;*.url)|Omsi.exe;*.lnk;*.url|Executável OMSI (Omsi.exe)|Omsi.exe|Atalhos (*.lnk;*.url)|*.lnk;*.url|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        var owner = GetPrimaryWebDialogOwner();
        if ((owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true)
        {
            return;
        }

        var installation =
            OmsiInstallationLocator.TryResolveInstallDirectory(dialog.FileName);
        if (installation is null)
        {
            throw new InvalidOperationException(
                "O arquivo/atalho selecionado não aponta para uma instalação válida do OMSI 2.");
        }

        OmsiInstallationProfileStore.DiscoverAndMerge(installation);
        SetPreferredOmsiInstallDirectory(installation);
        _webOmsiLaunchNotice =
            $"Instalação OMSI reconhecida e selecionada em {installation}.";
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
        SetPreferredOmsiInstallDirectory(dialog.FolderName);
        _webOmsiLaunchNotice =
            $"Instalação OMSI reconhecida e selecionada em {dialog.FolderName}.";
    }

    private static void SetPreferredOmsiInstallDirectory(
        string installDirectory)
    {
        string normalized;
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(installDirectory));
        }
        catch
        {
            return;
        }

        var profile = OmsiInstallationProfileStore.Load()
            .FirstOrDefault(item =>
            {
                try
                {
                    return string.Equals(
                        Path.TrimEndingDirectorySeparator(
                            Path.GetFullPath(item.InstallDirectory)),
                        normalized,
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

        if (profile is not null)
        {
            OmsiInstallationProfileStore.Upsert(
                profile with { IsPreferred = true });
        }
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

        if (!EnsurePluginBeforeOmsiLaunch(
                profile.InstallDirectory,
                out var pluginNotice))
        {
            _webOmsiLaunchNotice = pluginNotice;
            return;
        }

        _webOmsiLaunchNotice = pluginNotice;
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

    private static string NormalizeDashboardMinimapStyle(string? value) =>
        string.Equals(
            value?.Trim(),
            "circular",
            StringComparison.OrdinalIgnoreCase)
            ? "circular"
            : "rectangular";

    private static MultiplayerSettings BuildHudSettingsFromWeb(JsonElement? payload)
    {
        var current = MultiplayerSettingsStore.Load();
        return current with
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
            DashboardMinimapStyle = NormalizeDashboardMinimapStyle(
                GetWebPayloadString(payload, "minimapStyle") ??
                current.DashboardMinimapStyle),
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
        };
    }

    private static void PreviewHudSettingsFromWeb(JsonElement? payload)
    {
        var preview = BuildHudSettingsFromWeb(payload) with
        {
            DashboardEnabled = true
        };
        MultiplayerSettingsStore.PreviewHud(preview);
    }

    private static void ClearHudPreviewFromWeb() =>
        MultiplayerSettingsStore.ClearHudPreview();

    private static void SaveHudSettingsFromWeb(JsonElement? payload)
    {
        var updated = BuildHudSettingsFromWeb(payload);
        MultiplayerSettingsStore.ClearHudPreview();
        MultiplayerSettingsStore.Save(updated);
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
            DashboardMinimapStyle = "rectangular",
            DashboardMultiplayerScale = 1d,
            DashboardAlertsScale = 1d,
            DashboardSideIndicatorsScale = 1d
        };
        MultiplayerSettingsStore.ClearHudPreview();
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

    private object BuildWebSessionHealthState(
        string? configuredPerformanceProfile = null)
    {
        var session = DispatcherSessionFeed.Snapshot();
        var network = SessionNetworkQualityFeed.Snapshot();
        var plugin = (System.Windows.Application.Current as App)?.PluginBridge.GetConnectionInfo();
        var pluginCapabilities =
            plugin?.LastCapabilities?.Capabilities ??
            plugin?.LastStatus?.Capabilities;
        var pluginRuntime =
            pluginCapabilities?.Contains(
                PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin,
                StringComparer.OrdinalIgnoreCase) == true
                ? "openomsi"
                : plugin?.IsConnected == true
                    ? "omsi2"
                    : null;
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
            pluginRuntime,
            pluginPerformance = new
            {
                pressureLevel = plugin?.LastStatus?.PluginPressureLevel,
                workMilliseconds = plugin?.LastStatus?.PluginWorkMilliseconds,
                averageWorkMilliseconds = plugin?.LastStatus?.PluginAverageWorkMilliseconds,
                averageFrameIntervalMilliseconds = plugin?.LastStatus?.PluginAverageFrameIntervalMilliseconds,
                minimumWorkIntervalMilliseconds = plugin?.LastStatus?.PluginMinimumWorkIntervalMilliseconds,
                maxCommandsPerSlice = plugin?.LastStatus?.PluginMaxCommandsPerSlice,
                lastFrameIntervalMilliseconds = plugin?.LastStatus?.PluginLastFrameIntervalMilliseconds,
                peakFrameIntervalMilliseconds = plugin?.LastStatus?.PluginPeakFrameIntervalMilliseconds,
                frameStallCount = plugin?.LastStatus?.PluginFrameStallCount,
                configuredProfile =
                    configuredPerformanceProfile
                    ?? MultiplayerSettingsStore.Load().PerformanceProfile,
                activeProfile = plugin?.LastStatus?.PerformanceProfile,
                queueBackpressureActive = plugin?.LastStatus?.PluginPressureLevel is > 0
            },
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

    private static async Task SetPerformanceProfileFromWebAsync(string? profile)
    {
        var normalized = profile?.Trim().ToLowerInvariant() switch
        {
            "stability" => "stability",
            "multiplayer" => "multiplayer",
            "quality" => "quality",
            "diagnostics" => "diagnostics",
            _ => "auto"
        };

        var settings = MultiplayerSettingsStore.Load();
        if (!string.Equals(settings.PerformanceProfile, normalized, StringComparison.Ordinal))
        {
            MultiplayerSettingsStore.Save(settings with
            {
                PerformanceProfile = normalized
            });
        }

        if (Application.Current is not App app ||
            !app.PluginBridge.GetConnectionInfo().IsConnected)
        {
            return;
        }

        await app.PluginBridge.SendMessageAsync(new PluginBridgeMessage(
            PluginBridgeProtocol.SetPerformanceProfile,
            PluginBridgeProtocol.Version,
            PerformanceProfile: normalized));
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

    private void ExportDiagnosticBundleFromWeb()
    {
        _webSessionHealthNotice = null;

        var dialog = new SaveFileDialog
        {
            Title = "Exportar pacote de diagnóstico sanitizado do NavBR",
            Filter = "NavBR Diagnostics (*.navbr-diagnostics.zip)|*.navbr-diagnostics.zip|ZIP (*.zip)|*.zip",
            FileName = $"navbr-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.navbr-diagnostics.zip",
            DefaultExt = ".zip",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "NavBR-Diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var profiles = OmsiInstallationProfileStore.Load();
            var pluginRoot = ResolveConfiguredOmsiRootForPlugin(profiles);
            var plugin = GetPluginInstallDiagnostics(pluginRoot);
            var gateway = OpenOmsiLanGateway.Shared.GetStatus();
            var update = (Application.Current as App)?.AutoUpdater.GetSnapshot();

            var summary = new
            {
                schema = "navbr-diagnostics",
                version = 1,
                exportedAtUtc = DateTimeOffset.UtcNow,
                navbrVersion = NavBRVersionInfo.Current,
                sessionHealth = BuildWebSessionHealthState(),
                omsiPlugin = new
                {
                    state = plugin.State,
                    requiredFilesFound = plugin.RequiredFilesFound,
                    verifiedFiles = plugin.VerifiedFiles,
                    manifest = plugin.Manifest,
                    updateRequired = plugin.UpdateRequired,
                    expectedVersion = plugin.ExpectedVersion,
                    installedVersion = plugin.InstalledVersion,
                    files = plugin.Files.Select(file => new
                    {
                        name = file.Name,
                        exists = file.Exists,
                        hashMatches = file.HashMatches
                    }).ToArray()
                },
                applicationUpdate = update is null
                    ? null
                    : new
                    {
                        status = update.Status,
                        currentVersion = update.CurrentVersion,
                        availableVersion = update.AvailableVersion,
                        progressPercent = update.ProgressPercent,
                        updateAvailable = update.UpdateAvailable,
                        readyToInstall = update.ReadyToInstall,
                        checkedAtUtc = update.CheckedAtUtc
                    },
                openOmsiGateway = new
                {
                    running = gateway.Running,
                    clientConnected = gateway.ClientConnected,
                    remotePlayers = gateway.RemotePlayers,
                    localStateFrames = gateway.LocalStateFrames,
                    localStateRateHz = gateway.LocalStateRateHz,
                    vehicleIdentityReady =
                        !string.IsNullOrWhiteSpace(gateway.VehiclePath),
                    hasLastError = !string.IsNullOrWhiteSpace(gateway.LastError)
                },
                hardware = new
                {
                    connected = Hardware.HardwareCockpitBridgeController.Shared
                        .Snapshot(GetCurrentTelemetryForAlpha11())
                        .Connected
                },
                privacy = new
                {
                    rawPasswordsIncluded = false,
                    rawTokensIncluded = false,
                    roomIdsIncluded = false,
                    playerIdsIncluded = false,
                    ipAddressesIncluded = false,
                    localPathsIncluded = false,
                    rawLogIncluded = false,
                    logSanitized = File.Exists(NavBRAppLog.LogPath)
                }
            };

            File.WriteAllText(
                Path.Combine(workingDirectory, "summary.json"),
                JsonSerializer.Serialize(
                    summary,
                    new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));

            var sanitizedLog = ReadSanitizedDiagnosticLogTail();
            if (!string.IsNullOrWhiteSpace(sanitizedLog))
            {
                File.WriteAllText(
                    Path.Combine(workingDirectory, "navbr-sanitized.log"),
                    sanitizedLog,
                    new UTF8Encoding(false));
            }

            File.WriteAllText(
                Path.Combine(workingDirectory, "PRIVACY.txt"),
                "This diagnostic package is sanitized by NavBR before export. " +
                "Passwords, tokens, room/player identifiers, IP addresses, email addresses " +
                "and local filesystem paths are removed or replaced. Raw logs are never included.",
                new UTF8Encoding(false));

            if (File.Exists(dialog.FileName))
            {
                File.Delete(dialog.FileName);
            }

            ZipFile.CreateFromDirectory(
                workingDirectory,
                dialog.FileName,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            _webSessionHealthNotice =
                "Pacote de diagnóstico sanitizado exportado com sucesso.";
        }
        catch (Exception ex)
        {
            _webSessionHealthNotice =
                $"Não foi possível exportar o pacote de diagnóstico: {ex.Message}";
        }
        finally
        {
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static string? ReadSanitizedDiagnosticLogTail()
    {
        try
        {
            if (!File.Exists(NavBRAppLog.LogPath))
            {
                return null;
            }

            const int maximumBytes = 2 * 1024 * 1024;
            using var stream = new FileStream(
                NavBRAppLog.LogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maximumBytes)
            {
                stream.Seek(-maximumBytes, SeekOrigin.End);
            }

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            return SanitizeDiagnosticText(text);
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeDiagnosticText(string value)
    {
        var sanitized = value;

        var userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            sanitized = sanitized.Replace(
                userProfile,
                "[user-profile]",
                StringComparison.OrdinalIgnoreCase);
        }

        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            sanitized = sanitized.Replace(
                localAppData,
                "[local-app-data]",
                StringComparison.OrdinalIgnoreCase);
        }

        sanitized = Regex.Replace(
            sanitized,
            @"(?i)\b(?:password|passwd|token|secret|invite(?:code)?|roomid|playerid)\s*[=:]\s*[^\s,;]+",
            match =>
            {
                var separator = match.Value.IndexOfAny(new[] { '=', ':' });
                return separator > 0
                    ? match.Value[..separator] + "=[redacted]"
                    : "[redacted]";
            });

        sanitized = Regex.Replace(
            sanitized,
            @"\b(?:\d{1,3}\.){3}\d{1,3}\b",
            "[ip]");

        sanitized = Regex.Replace(
            sanitized,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b",
            "[id]");

        sanitized = Regex.Replace(
            sanitized,
            @"(?i)\b[A-Z]:\\[^\r\n\t\""]+",
            "[path]");

        sanitized = Regex.Replace(
            sanitized,
            @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
            "[email]");

        return sanitized;
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


internal sealed record OmsiMemoryProbe(
    int ProcessId,
    long PrivateBytes,
    long WorkingSetBytes,
    long PeakWorkingSetBytes,
    string Level,
    DateTimeOffset SampledAtUtc)
{
    public object ToWebState() => new
    {
        processId = ProcessId,
        privateBytes = PrivateBytes,
        workingSetBytes = WorkingSetBytes,
        peakWorkingSetBytes = PeakWorkingSetBytes,
        privateMiB = PrivateBytes / (1024d * 1024d),
        workingSetMiB = WorkingSetBytes / (1024d * 1024d),
        peakWorkingSetMiB = PeakWorkingSetBytes / (1024d * 1024d),
        level = Level,
        sampledAtUtc = SampledAtUtc
    };
}
