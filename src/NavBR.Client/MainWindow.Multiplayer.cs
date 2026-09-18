using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
    private DispatcherTimer? _remoteMotionTimer;
    private int? _hudAttachedOmsiProcessId;
    private readonly Dictionary<string, Grid> _remotePlayerMarkers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RemoteMotionSmoother> _remotePlayerMotion = new(StringComparer.OrdinalIgnoreCase);
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
            StopRemoteMotionTimer();
            DispatcherSessionFeed.SetConnected(false);

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

    internal void OpenMultiplayerCentralForShell(bool showWindow = true)
    {
        if (_multiplayerWindow is not null)
        {
            if (!showWindow)
            {
                return;
            }

            if (_multiplayerWindow.WindowState == WindowState.Minimized)
            {
                _multiplayerWindow.WindowState = WindowState.Normal;
            }

            _multiplayerWindow.Show();
            _multiplayerWindow.Activate();
            return;
        }

        var window = new MultiplayerWindow(
            () => _lastTelemetry,
            GetActiveMapForMultiplayer,
            _telemetryProvider.ReadRoleplayCharacterOptions)
        {
            Owner = this
        };

        var hud = EnsureHudOverlay();
        hud.SetLocalDisplayName(window.CurrentDisplayName);
        EnsureRemoteMotionTimer();

        window.RemoteTelemetryReceived += frame =>
        {
            DispatcherSessionFeed.Update(frame);
            RenderRemotePlayer(frame);
            hud.SetRemotePhysicalVehicleActive(
                frame.Player.PlayerId,
                window.IsRemotePhysicalVehicleSpawned(frame.Player.PlayerId));
            hud.UpdateRemotePlayerSmooth(frame);
        };
        window.RemotePlayerLeft += playerId =>
        {
            DispatcherSessionFeed.Remove(playerId);
            RemoveRemotePlayerMarker(playerId);
            hud.RemoveRemotePlayerSmooth(playerId);
        };
        window.RemotePlayersReset += () =>
        {
            DispatcherSessionFeed.Clear();
            ClearRemotePlayerMarkers();
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
            ClearRemotePlayerMarkers();
            _multiplayerWindow = null;
            StopRemoteMotionTimer();

            // Não fecha nem para o timer do HUD: o minimapa continua sendo
            // um recurso principal do NavBR mesmo sem sessão multiplayer.
            UpdateHudLocalState();
        };

        _multiplayerWindow = window;
        UpdateHudLocalState();
        window.Show();
        if (!showWindow)
        {
            window.Hide();
        }
    }

    internal void OpenMultiplayerRoleplayTabForShell()
    {
        OpenMultiplayerCentralForShell();
        _multiplayerWindow?.ShowRoleplayTab();
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
        new HudCustomizationWindow(this).ShowDialog();
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

    private void EnsureRemoteMotionTimer()
    {
        if (_remoteMotionTimer is null)
        {
            _remoteMotionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _remoteMotionTimer.Tick += (_, _) => UpdateRemoteMarkerMotion();
        }

        if (!_remoteMotionTimer.IsEnabled)
        {
            _remoteMotionTimer.Start();
        }
    }

    private void StopRemoteMotionTimer()
    {
        _remoteMotionTimer?.Stop();
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

    private void RenderRemotePlayer(PlayerTelemetryFrame frame)
    {
        var local = _lastTelemetry;
        var layout = _loadedRoadmapLayout;
        var bitmap = _loadedRoadmapBitmap;
        var localMap = GetActiveMapForMultiplayer();

        var mapCompatibilityMatches = localMap is null ||
                                      string.IsNullOrWhiteSpace(localMap.CompatibilityId) ||
                                      string.IsNullOrWhiteSpace(frame.Player.MapCompatibilityId) ||
                                      string.Equals(
                                          localMap.CompatibilityId,
                                          frame.Player.MapCompatibilityId,
                                          StringComparison.OrdinalIgnoreCase);

        if (!mapCompatibilityMatches ||
            local is null ||
            layout is null ||
            bitmap is null ||
            string.IsNullOrWhiteSpace(local.MapName) ||
            string.IsNullOrWhiteSpace(frame.Telemetry.MapName) ||
            NormalizeMapName(local.MapName) != NormalizeMapName(frame.Telemetry.MapName) ||
            frame.Telemetry.GridX is not int gridX ||
            frame.Telemetry.GridY is not int gridY ||
            frame.Telemetry.TileX is not double tileX ||
            frame.Telemetry.TileY is not double tileY ||
            !RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                gridX,
                gridY,
                tileX,
                tileY,
                out var pixelX,
                out var pixelY) ||
            pixelX < 0 || pixelX > bitmap.PixelWidth ||
            pixelY < 0 || pixelY > bitmap.PixelHeight)
        {
            if (_remotePlayerMarkers.TryGetValue(frame.Player.PlayerId, out var hiddenMarker))
            {
                hiddenMarker.Visibility = Visibility.Collapsed;
            }

            if (_remotePlayerMotion.TryGetValue(frame.Player.PlayerId, out var hiddenMotion))
            {
                hiddenMotion.Reset();
            }

            return;
        }

        var marker = GetOrCreateRemoteMarker(frame.Player.PlayerId, frame.Player.DisplayName, bitmap.PixelWidth);
        var smoother = GetOrCreateRemoteMotion(frame.Player.PlayerId);
        smoother.SetTarget(
            pixelX,
            pixelY,
            frame.Telemetry.HeadingDegrees,
            frame.Telemetry.Timestamp);

        marker.ToolTip = $"{frame.Player.DisplayName} • {frame.Telemetry.SpeedKph:F1} km/h";
        marker.Visibility = Visibility.Visible;
        EnsureRemoteMotionTimer();
        UpdateRemoteMarkerMotion(frame.Player.PlayerId);
    }

    private RemoteMotionSmoother GetOrCreateRemoteMotion(string playerId)
    {
        if (_remotePlayerMotion.TryGetValue(playerId, out var existing))
        {
            return existing;
        }

        var smoother = new RemoteMotionSmoother();
        _remotePlayerMotion[playerId] = smoother;
        return smoother;
    }

    private void UpdateRemoteMarkerMotion()
    {
        foreach (var playerId in _remotePlayerMotion.Keys.ToArray())
        {
            UpdateRemoteMarkerMotion(playerId);
        }
    }

    private void UpdateRemoteMarkerMotion(string playerId)
    {
        if (!_remotePlayerMotion.TryGetValue(playerId, out var smoother) ||
            !_remotePlayerMarkers.TryGetValue(playerId, out var marker))
        {
            return;
        }

        if (DateTimeOffset.UtcNow - smoother.LastTargetUtc > TimeSpan.FromSeconds(3))
        {
            marker.Visibility = Visibility.Collapsed;
            return;
        }

        var pose = smoother.Step();
        if (!pose.IsValid)
        {
            return;
        }

        var markerSize = marker.Width;
        Canvas.SetLeft(marker, pose.X - markerSize / 2d);
        Canvas.SetTop(marker, pose.Y - markerSize / 2d);

        var icon = marker.Children
            .OfType<Grid>()
            .FirstOrDefault(child => string.Equals(child.Tag as string, "roadmap-remote-icon", StringComparison.Ordinal));
        if (icon?.RenderTransform is RotateTransform rotation)
        {
            rotation.Angle = pose.HeadingDegrees;
        }

        marker.Visibility = Visibility.Visible;
    }

    private Grid GetOrCreateRemoteMarker(string playerId, string displayName, int roadmapWidth)
    {
        if (_remotePlayerMarkers.TryGetValue(playerId, out var existing))
        {
            UpdateRoadmapRemoteName(existing, displayName);
            return existing;
        }

        const double size = 48d;
        var marker = new Grid
        {
            Width = size,
            Height = size,
            ToolTip = displayName,
            ClipToBounds = false
        };

        var icon = new Grid
        {
            Width = size,
            Height = size,
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            RenderTransform = new RotateTransform(),
            Tag = "roadmap-remote-icon"
        };
        icon.Children.Add(new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromRgb(61, 137, 196)),
            Stroke = Brushes.White,
            StrokeThickness = 4d
        });
        icon.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(24d, 6d),
                new(34d, 32d),
                new(24d, 27d),
                new(14d, 32d)
            },
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            StrokeThickness = 1d
        });
        marker.Children.Add(icon);

        var name = new TextBlock
        {
            Text = NormalizeRoadmapDisplayName(displayName),
            Foreground = Brushes.White,
            FontSize = 11d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 132d,
            Tag = "roadmap-remote-name"
        };
        marker.Children.Add(new Border
        {
            Padding = new Thickness(7d, 3d, 7d, 3d),
            Background = new SolidColorBrush(Color.FromArgb(220, 4, 15, 23)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, 113, 198, 255)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(5d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, -27d, 0d, 0d),
            Child = name
        });

        Panel.SetZIndex(marker, 20);
        RoadmapCanvas.Children.Add(marker);
        _remotePlayerMarkers[playerId] = marker;
        return marker;
    }

    private static void UpdateRoadmapRemoteName(Grid marker, string displayName)
    {
        var name = marker.Children
            .OfType<Border>()
            .Select(border => border.Child)
            .OfType<TextBlock>()
            .FirstOrDefault(block => string.Equals(block.Tag as string, "roadmap-remote-name", StringComparison.Ordinal));
        if (name is not null)
        {
            name.Text = NormalizeRoadmapDisplayName(displayName);
        }
    }

    private static string NormalizeRoadmapDisplayName(string? displayName)
    {
        var value = string.IsNullOrWhiteSpace(displayName) ? "Driver" : displayName.Trim();
        return value.Length <= 28 ? value : value[..28];
    }

    private void RemoveRemotePlayerMarker(string playerId)
    {
        _remotePlayerMotion.Remove(playerId);

        if (!_remotePlayerMarkers.Remove(playerId, out var marker))
        {
            return;
        }

        RoadmapCanvas.Children.Remove(marker);
    }

    private void ClearRemotePlayerMarkers()
    {
        foreach (var marker in _remotePlayerMarkers.Values)
        {
            RoadmapCanvas.Children.Remove(marker);
        }

        _remotePlayerMarkers.Clear();
        _remotePlayerMotion.Clear();
    }
}
