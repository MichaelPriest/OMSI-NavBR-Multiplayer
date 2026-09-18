using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Client.Operations;
using NavBR.Client.Overlay;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private MultiplayerWindow? _multiplayerWindow;
    private HudOverlayWindow? _hudOverlay;
    private DispatcherTimer? _hudStateTimer;
    private int? _hudAttachedOmsiProcessId;
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
            _telemetryProvider.ReadRoleplayCharacterOptions);

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
        var processId = _currentOmsi?.ProcessId;
        hud.AttachOmsiProcess(processId);
        _hudAttachedOmsiProcessId = processId;
        hud.UpdateLocalTelemetry(_lastTelemetry, GetActiveMapForMultiplayer());
        hud.UpdateCameraProjection(_telemetryProvider.ReadCameraProjection());
        hud.Closed += (_, _) =>
        {
            hud.RoleplayButtonRequested -= HandleHudRoleplayButtonRequestedForShell;

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
                Interval = TimeSpan.FromMilliseconds(200)
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
            return;
        }

        var processId = _currentOmsi?.ProcessId;
        if (_hudAttachedOmsiProcessId != processId)
        {
            _hudOverlay.AttachOmsiProcess(processId);
            _hudAttachedOmsiProcessId = processId;
        }

        _hudOverlay.UpdateLocalTelemetry(_lastTelemetry, GetActiveMapForMultiplayer());
        _hudOverlay.UpdateCameraProjection(_telemetryProvider.ReadCameraProjection());
        UpdateHudRoleplayStateForShell();
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
