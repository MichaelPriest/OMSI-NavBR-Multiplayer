using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Client.Operations;
using NavBR.Client.Overlay;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client;

public partial class MainWindow
{
    private MultiplayerWindow? _multiplayerWindow;
    private HudOverlayWindow? _hudOverlay;
    private DispatcherTimer? _hudStateTimer;
    private int _hudRefreshIntervalMs = 200;
    private int? _hudAttachedOmsiProcessId;
    private DateTimeOffset _lastHudRoadTrafficReadUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<TrafficVehicleState> _lastHudRoadTraffic =
        Array.Empty<TrafficVehicleState>();
    private bool _multiplayerLocalizationHooked;
    private bool _hudLifetimeHooked;

    private void MultiplayerButton_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizeMultiplayerButton();

        // O mini HUD faz parte do NavBR base. Multiplayer apenas acrescenta
        // jogadores remotos, chat e voz ao mesmo overlay.
        EnsureHudOverlay();
        HookHudLifetimeToMainWindow();

        if (_multiplayerLocalizationHooked)
        {
            return;
        }

        _multiplayerLocalizationHooked = true;
        LanguageComboBox.SelectionChanged += (_, _) => LocalizeMultiplayerButton();
    }

    private void HookHudLifetimeToMainWindow()
    {
        if (_hudLifetimeHooked)
        {
            return;
        }

        _hudLifetimeHooked = true;
        Closed += (_, _) =>
        {
            StopHudRefreshTimer();
            DispatcherSessionFeed.SetConnected(false);

            if (_multiplayerWindow is not null)
            {
                var controller = _multiplayerWindow;
                controller.AllowApplicationShutdown();
                controller.Close();
            }

            if (_hudOverlay is not null)
            {
                _hudOverlay.Close();
                _hudOverlay = null;
            }
        };
    }

    private void LocalizeMultiplayerButton()
    {
        MultiplayerButton.Content = LocalizationService.Get("MultiplayerOpen");
    }

    private void MultiplayerButton_Click(object sender, RoutedEventArgs e) =>
        OpenMultiplayerCentralForShell();

    internal void OpenMultiplayerCentralForShell()
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        NavigatePrimaryWebShell("multiplayer");
    }

    internal void OpenMultiplayerCentralForShell(bool showWindow)
    {
        HookHudLifetimeToMainWindow();

        if (_multiplayerWindow is not null)
        {
            if (showWindow)
            {
                NavigatePrimaryWebShell("multiplayer");
            }

            return;
        }

        var window = new MultiplayerWindow(
            () => _lastTelemetry,
            GetActiveMapForMultiplayer,
            _telemetryProvider.ReadRoleplayCharacterOptions,
            () => _currentOmsi?.InstallDirectory,
            () => _openOmsiVehicleIdentityRoots);

        var hud = EnsureHudOverlay();
        hud.SetLocalDisplayName(window.CurrentDisplayName);

        window.RemoteTelemetryReceived += frame =>
        {
            DispatcherSessionFeed.Update(frame);
            hud.SetRemotePhysicalVehicleActive(
                frame.Player.PlayerId,
                window.IsRemotePhysicalVehicleSpawned(frame.Player.PlayerId));
            hud.UpdateRemotePlayerSmooth(frame);
        };
        window.RemotePlayerLeft += playerId =>
        {
            DispatcherSessionFeed.Remove(playerId);
            hud.RemoveRemotePlayerSmooth(playerId);
        };
        window.RemotePlayersReset += () =>
        {
            DispatcherSessionFeed.Clear();
            hud.ClearRemotePlayersSmooth();
        };
        window.ChatMessageReceived += hud.AddChatMessage;
        window.RemoteSpeakerActive += hud.MarkRemoteSpeaker;
        window.VoiceError += hud.SetVoiceError;
        Action<bool, string?> connectionChangedHandler = (connected, roomId) =>
        {
            DispatcherSessionFeed.SetConnected(connected, roomId);
            hud.SetConnectionState(connected);
        };
        window.MultiplayerConnectionChanged += connectionChangedHandler;
        window.LocalDisplayNameChanged += hud.SetLocalDisplayName;
        Action roleplayActionHandler = HandleHudRoleplayButtonRequestedForShell;
        window.RoleplayActionRequested += roleplayActionHandler;

        Action<string> chatSubmittedHandler = text => _ = window.SendChatFromOverlayAsync(text);
        Action<bool> pushToTalkHandler = window.SetPushToTalk;
        hud.ChatSubmitted += chatSubmittedHandler;
        hud.PushToTalkChanged += pushToTalkHandler;

        window.Closed += (_, _) =>
        {
            hud.ChatSubmitted -= chatSubmittedHandler;
            hud.PushToTalkChanged -= pushToTalkHandler;
            window.MultiplayerConnectionChanged -= connectionChangedHandler;
            window.RoleplayActionRequested -= roleplayActionHandler;
            DispatcherSessionFeed.SetConnected(false);
            hud.SetConnectionState(false);
            hud.ClearRemotePlayersSmooth();
            _multiplayerWindow = null;

            // Não fecha nem para o timer do HUD: o minimapa continua sendo
            // um recurso principal do NavBR mesmo sem sessão multiplayer.
            UpdateHudLocalState();
        };

        _multiplayerWindow = window;
        UpdateHudLocalState();

        // MultiplayerWindow still owns native controller/services that are
        // being detached incrementally from WPF. Initialize those services
        // explicitly without ever creating or showing the retired WPF surface.
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.InitializeControllerForWebShell();

        if (showWindow)
        {
            NavigatePrimaryWebShell("multiplayer");
        }
    }

    internal void OpenMultiplayerRoleplayTabForShell()
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        NavigatePrimaryWebShell("roleplay");
    }

    internal void ToggleHudLayoutForShell()
    {
        var hud = EnsureHudOverlay();
        hud.SetLayoutEditMode(!hud.IsLayoutEditMode);
        if (hud.IsLayoutEditMode)
        {
            hud.Show();
            hud.Activate();
        }
    }

    internal void OpenHudEditorForShell()
    {
        _ = EnsureHudOverlay();
        NavigatePrimaryWebShell("settings-hud");
    }

    private HudOverlayWindow EnsureHudOverlay()
    {
        if (_hudOverlay is not null)
        {
            StartHudRefreshTimer();
            return _hudOverlay;
        }

        var hud = new HudOverlayWindow();
        hud.RoleplayButtonRequested += HandleHudRoleplayButtonRequestedForShell;
        hud.InGamePanelOpened += HandleHudInGamePanelOpened;
        hud.InGameConnectRequested += HandleHudInGameConnectRequested;
        hud.InGameAssistanceRequested += HandleHudInGameAssistanceRequested;
        hud.InGameIncidentRequested += HandleHudInGameIncidentRequested;
        hud.InGameOperationalResolvedRequested += HandleHudInGameOperationalResolvedRequested;
        hud.InGameDispatchAcknowledgeRequested += HandleHudInGameDispatchAcknowledgeRequested;
        hud.InGameDispatchResolveRequested += HandleHudInGameDispatchResolveRequested;
        hud.InGameRoleplaySelectionRequested += HandleHudInGameRoleplaySelectionRequestedForShell;
        hud.InGameRoleplayFreeRoamChanged += HandleHudInGameRoleplayFreeRoamChanged;
        hud.InGamePerformanceProfileChanged += HandleHudInGamePerformanceProfileChanged;
        hud.InGameVoiceEnabledChanged += HandleHudInGameVoiceEnabledChanged;
        hud.InGamePhysicalVehiclesChanged += HandleHudInGamePhysicalVehiclesChanged;
        var processId = GetActiveSimulatorProcessIdForHud();
        hud.AttachOmsiProcess(processId);
        _hudAttachedOmsiProcessId = processId;
        hud.UpdateLocalTelemetry(_lastTelemetry, GetActiveMapForMultiplayer());
        RefreshHudRoadTraffic(force: true);
        hud.UpdateLocalRoadTraffic(_lastHudRoadTraffic);
        hud.UpdateCameraProjection(_telemetryProvider.ReadCameraProjection());
        hud.Closed += (_, _) =>
        {
            hud.RoleplayButtonRequested -= HandleHudRoleplayButtonRequestedForShell;
            hud.InGamePanelOpened -= HandleHudInGamePanelOpened;
            hud.InGameConnectRequested -= HandleHudInGameConnectRequested;
            hud.InGameAssistanceRequested -= HandleHudInGameAssistanceRequested;
            hud.InGameIncidentRequested -= HandleHudInGameIncidentRequested;
            hud.InGameOperationalResolvedRequested -= HandleHudInGameOperationalResolvedRequested;
            hud.InGameDispatchAcknowledgeRequested -= HandleHudInGameDispatchAcknowledgeRequested;
            hud.InGameDispatchResolveRequested -= HandleHudInGameDispatchResolveRequested;
            hud.InGameRoleplaySelectionRequested -= HandleHudInGameRoleplaySelectionRequestedForShell;
            hud.InGameRoleplayFreeRoamChanged -= HandleHudInGameRoleplayFreeRoamChanged;
            hud.InGamePerformanceProfileChanged -= HandleHudInGamePerformanceProfileChanged;
            hud.InGameVoiceEnabledChanged -= HandleHudInGameVoiceEnabledChanged;
            hud.InGamePhysicalVehiclesChanged -= HandleHudInGamePhysicalVehiclesChanged;

            if (ReferenceEquals(_hudOverlay, hud))
            {
                _hudOverlay = null;
            }

            _hudAttachedOmsiProcessId = null;
            StopHudRefreshTimer();
        };
        _hudOverlay = hud;
        hud.Show();
        StartHudRefreshTimer();
        return hud;
    }

    private void StartHudRefreshTimer()
    {
        if (_hudStateTimer is null)
        {
            _hudStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_hudRefreshIntervalMs)
            };
            _hudStateTimer.Tick += (_, _) => UpdateHudLocalState();
        }

        if (!_hudStateTimer.IsEnabled)
        {
            _hudStateTimer.Start();
        }
    }

    private void StopHudRefreshTimer()
    {
        _hudStateTimer?.Stop();
    }

    private void UpdateHudLocalState()
    {
        if (_hudOverlay is null)
        {
            if (!_nativeRuntimeStarted)
            {
                return;
            }

            // Self-heal the overlay if it was never created by the retired WPF
            // surface or if its reference was lost while the React shell kept
            // the native runtime alive.
            EnsureHudOverlay();
            if (_hudOverlay is null)
            {
                return;
            }
        }

        var processId = GetActiveSimulatorProcessIdForHud();
        if (_hudAttachedOmsiProcessId != processId)
        {
            _hudOverlay.AttachOmsiProcess(processId);
            _hudAttachedOmsiProcessId = processId;
        }

        _hudOverlay.UpdateLocalTelemetry(_lastTelemetry, GetActiveMapForMultiplayer());
        RefreshHudRoadTraffic();
        _hudOverlay.UpdateLocalRoadTraffic(_lastHudRoadTraffic);

        var pluginConnection = (System.Windows.Application.Current as App)?
            .PluginBridge
            .GetConnectionInfo();
        var pluginStatus = pluginConnection?.LastStatus;
        var openOmsiRuntime =
            OpenOmsiLanGateway.Shared.IsClientConnected ||
            pluginStatus?.Capabilities?.Contains(
                PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin,
                StringComparer.OrdinalIgnoreCase) == true;

        // openOMSI does not currently expose a public camera projection matrix.
        // Clear any stale OMSI 2 matrix instead of projecting 3D nameplates
        // against the wrong simulator. 2D HUD/minimap surfaces remain active.
        _hudOverlay.UpdateCameraProjection(
            openOmsiRuntime
                ? null
                : _telemetryProvider.ReadCameraProjection());
        var averageFrameIntervalMs =
            pluginStatus?.PluginAverageFrameIntervalMilliseconds;
        double? fps =
            averageFrameIntervalMs is double frameMs &&
            double.IsFinite(frameMs) &&
            frameMs > 0.1d
                ? 1000d / frameMs
                : null;
        var network = SessionNetworkQualityFeed.Snapshot();
        var networkReady =
            network.Samples >= 2 &&
            network.RoundTripMs is not null;
        _hudOverlay.SetRuntimeMetrics(
            fps,
            networkReady ? network.RoundTripMs : null,
            networkReady ? network.JitterMs : null);

        UpdateHudRoleplayStateForShell();
        UpdateHudInGamePanelState();
        UpdateHudRefreshCadence();
    }

    private void RefreshHudRoadTraffic(bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force &&
            now - _lastHudRoadTrafficReadUtc < TimeSpan.FromMilliseconds(500d))
        {
            return;
        }

        _lastHudRoadTrafficReadUtc = now;
        if (!_telemetryProvider.IsAttached ||
            _lastTelemetry?.IsInGame != true)
        {
            _lastHudRoadTraffic = Array.Empty<TrafficVehicleState>();
            return;
        }

        _lastHudRoadTraffic = _telemetryProvider.ReadRoadTraffic(
            maxVehicles: 48,
            radiusMeters: 900d);
    }

    private void HandleHudInGameVoiceEnabledChanged(bool enabled)
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        _multiplayerWindow?.SetVoiceEnabledFromWeb(enabled);
        UpdateHudInGamePanelState();
    }

    private async void HandleHudInGamePhysicalVehiclesChanged(bool enabled)
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow is not null)
        {
            await _multiplayerWindow.ConfigurePhysicalVehiclesFromWebAsync(enabled);
        }

        UpdateHudInGamePanelState();
    }

    private void HandleHudInGameRoleplayFreeRoamChanged(bool enabled)
    {
        _roleplayCharacterController?.SetFreeRoamEnabled(enabled);
        UpdateHudRoleplayStateForShell();
    }

    private async void HandleHudInGamePerformanceProfileChanged(string profile)
    {
        await SetPerformanceProfileFromWebAsync(profile);
        UpdateHudLocalState();
    }

    private void HandleHudInGamePanelOpened()
    {
        // The in-game menu is allowed to bootstrap the controller silently.
        // It must never require the React window to have been opened first.
        OpenMultiplayerCentralForShell(showWindow: false);
        UpdateHudInGamePanelState();
        UpdateHudInGameRoleplayOptionsForShell();
    }

    private async void HandleHudInGameConnectRequested()
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow is null)
        {
            return;
        }

        if (_multiplayerWindow.IsConnected)
        {
            _hudOverlay?.SetInGameConnectionNotice(null);
            UpdateHudInGamePanelState();
            return;
        }

        try
        {
            _hudOverlay?.SetInGameConnectionNotice(
                "conectando ao último servidor/sala...");
            UpdateHudInGamePanelState();

            // Use the actual fields entered in the in-game menu. Passing
            // null retains the saved setting for backwards compatibility.
            var inputs = _hudOverlay?.GetInGameConnectionParameters();
            await _multiplayerWindow.ConnectFromWebAsync(
                serverUrl: inputs?.ServerUrl,
                roomId: inputs?.RoomId,
                displayName: inputs?.DisplayName,
                roomPassword: inputs?.RoomPassword);

            _hudOverlay?.ClearInGameRoomPassword();
            _hudOverlay?.SetInGameConnectionNotice(null);
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? "falha ao conectar"
                : ex.Message.Trim();
            if (message.Length > 120)
            {
                message = message[..120] + "…";
            }

            _hudOverlay?.SetInGameConnectionNotice(
                $"falha ao conectar • {message}");
        }
        finally
        {
            UpdateHudInGamePanelState();
        }
    }

    private async void HandleHudInGameAssistanceRequested()
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow is null)
        {
            return;
        }

        await _multiplayerWindow.SubmitOperationalReportFromWebAsync(incident: false);
        UpdateHudInGamePanelState();
    }

    private async void HandleHudInGameIncidentRequested()
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow is null)
        {
            return;
        }

        await _multiplayerWindow.SubmitOperationalReportFromWebAsync(incident: true);
        UpdateHudInGamePanelState();
    }

    private async void HandleHudInGameOperationalResolvedRequested()
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow is null)
        {
            return;
        }

        await _multiplayerWindow.ResolveOwnOperationalReportsFromWebAsync();
        UpdateHudInGamePanelState();
    }

    private async void HandleHudInGameDispatchAcknowledgeRequested(string reportId)
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow?.IsTrafficAuthority != true ||
            string.IsNullOrWhiteSpace(reportId))
        {
            return;
        }

        await _multiplayerWindow.AcknowledgeOperationalReportFromShellAsync(reportId);
        UpdateHudInGamePanelState();
    }

    private async void HandleHudInGameDispatchResolveRequested(string reportId)
    {
        OpenMultiplayerCentralForShell(showWindow: false);
        if (_multiplayerWindow?.IsTrafficAuthority != true ||
            string.IsNullOrWhiteSpace(reportId))
        {
            return;
        }

        await _multiplayerWindow.ResolveOperationalReportFromShellAsync(reportId);
        UpdateHudInGamePanelState();
    }

    private void UpdateHudInGamePanelState()
    {
        if (_hudOverlay is null)
        {
            return;
        }

        var multiplayer = _multiplayerWindow;
        var badge = (System.Windows.Application.Current as App)?
            .NetworkRuntime
            .CurrentBadge;
        var companyLabel = badge is null
            ? null
            : $"{badge.CompanyShortName} • #{badge.EmployeeNumber} • {badge.Role}";

        var canManageDispatch =
            multiplayer?.IsConnected == true &&
            multiplayer.IsTrafficAuthority &&
            (badge is null ||
             (badge.Permissions & NavBR.Shared.Network.CompanyPermission.UseDispatcher) != 0);
        var dispatchReport = canManageDispatch
            ? multiplayer!.CurrentOperationalReportsForShell
                .Where(report =>
                    report.Status != OperationalReportStatus.Resolved &&
                    !string.Equals(
                        report.PlayerId,
                        multiplayer.CurrentPlayerId,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(report => report.Severity)
                .ThenBy(report => report.CreatedAtUtc)
                .FirstOrDefault()
            : null;

        var pluginConnection = (System.Windows.Application.Current as App)?
            .PluginBridge
            .GetConnectionInfo();
        var pluginStatus = pluginConnection?.LastStatus;
        var gateway = OpenOmsiLanGateway.Shared.GetStatus();
        var openOmsiRuntime =
            gateway.ClientConnected ||
            pluginStatus?.Capabilities?.Contains(
                PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin,
                StringComparer.OrdinalIgnoreCase) == true;

        string runtimeStatus;
        bool runtimeHealthy;
        if (openOmsiRuntime)
        {
            var streamState = !gateway.ClientConnected
                ? "WAITING"
                : gateway.LocalStateFrames > 0
                    ? "STREAMING"
                    : "LINKED";
            var rate = gateway.LocalStateRateHz is double hz &&
                       double.IsFinite(hz)
                ? $" • {hz:F1} Hz"
                : string.Empty;
            var identity = !string.IsNullOrWhiteSpace(
                    _lastTelemetry?.VehicleCompatibilityId)
                ? " • SHA OK"
                : " • SHA PENDING";

            runtimeStatus =
                $"RUNTIME • openOMSI • {streamState}{rate} • REMOTOS {gateway.RemotePlayers}{identity}";
            runtimeHealthy =
                gateway.ClientConnected &&
                gateway.LocalStateFrames > 0;
        }
        else
        {
            var pluginConnected = pluginConnection?.IsConnected == true;
            var physicalCount =
                pluginStatus?.RemoteVehicleCount ?? 0;
            runtimeStatus = pluginConnected
                ? $"RUNTIME • OMSI 2 • PLUGIN X86 • FÍSICOS {physicalCount}"
                : "RUNTIME • OMSI 2 • PLUGIN OFFLINE";
            runtimeHealthy = pluginConnected;
        }

        _hudOverlay.UpdateInGameOnlineFeatureState(
            voiceEnabled: multiplayer?.VoiceEnabledForShell == true,
            physicalVehiclesEnabled:
                multiplayer?.PhysicalVehiclesEnabledForShell ??
                ExperimentalFeatureFlags.PhysicalVehiclesEnabled);

        _hudOverlay.UpdateInGamePanelState(
            connected: multiplayer?.IsConnected == true,
            roomId: multiplayer?.CurrentRoomId,
            displayName: multiplayer?.CurrentDisplayName,
            runtimeStatus: runtimeStatus,
            runtimeHealthy: runtimeHealthy,
            operationalReport: multiplayer?.CurrentOwnOperationalReportForShell,
            companyLabel: companyLabel,
            canManageDispatch: canManageDispatch,
            dispatchReport: dispatchReport);
    }

    private void UpdateHudRefreshCadence()
    {
        if (_hudStateTimer is null)
        {
            return;
        }

        var status = (System.Windows.Application.Current as App)?
            .PluginBridge
            .GetConnectionInfo()
            .LastStatus;
        var intervalMs = ComputeAdaptiveRuntimeIntervalMs(
            status?.PerformanceProfile,
            status?.PluginPressureLevel ?? 0,
            _lastTelemetry?.IsInGame == true,
            qualityMs: 125,
            multiplayerMs: 150,
            stabilityMs: 300,
            diagnosticsMs: 250,
            automaticMs: 200);

        if (_hudRefreshIntervalMs == intervalMs)
        {
            return;
        }

        _hudRefreshIntervalMs = intervalMs;
        _hudStateTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
    }

    private OmsiMapInfo? GetActiveMapForMultiplayer()
    {
        var mapName = _lastTelemetry?.MapName;
        if (!string.IsNullOrWhiteSpace(mapName))
        {
            var fromTelemetry = FindActiveMap(mapName);
            if (fromTelemetry is not null)
            {
                return fromTelemetry;
            }
        }

        // Some OMSI builds / 4GB-patched sessions expose vehicle telemetry
        // correctly while TMap.name is unavailable. OMSI itself records the
        // authoritative loaded folder in logfile.txt, so use that as a safe,
        // read-only fallback instead of leaving the HUD at "Sem mapa".
        var installDirectory = _currentOmsi?.InstallDirectory;
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return null;
        }

        var loadedFolder = OmsiLoadedMapDetector.TryGetLoadedMapFolder(installDirectory);
        if (string.IsNullOrWhiteSpace(loadedFolder))
        {
            return null;
        }

        return _installedMaps.FirstOrDefault(map =>
            string.Equals(map.FolderName, loadedFolder, StringComparison.OrdinalIgnoreCase));
    }

}
