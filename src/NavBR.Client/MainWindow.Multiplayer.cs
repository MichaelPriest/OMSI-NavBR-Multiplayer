using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Client.Overlay;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private MultiplayerWindow? _multiplayerWindow;
    private HudOverlayWindow? _hudOverlay;
    private DispatcherTimer? _hudStateTimer;
    private readonly Dictionary<string, Grid> _remotePlayerMarkers = new(StringComparer.OrdinalIgnoreCase);
    private bool _multiplayerLocalizationHooked;

    private void MultiplayerButton_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizeMultiplayerButton();

        if (_multiplayerLocalizationHooked)
        {
            return;
        }

        _multiplayerLocalizationHooked = true;
        LanguageComboBox.SelectionChanged += (_, _) => LocalizeMultiplayerButton();
    }

    private void LocalizeMultiplayerButton()
    {
        MultiplayerButton.Content = LocalizationService.Get("MultiplayerOpen");
    }

    private void MultiplayerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_multiplayerWindow is not null)
        {
            if (_multiplayerWindow.WindowState == WindowState.Minimized)
            {
                _multiplayerWindow.WindowState = WindowState.Normal;
            }

            _multiplayerWindow.Activate();
            return;
        }

        var window = new MultiplayerWindow(
            () => _lastTelemetry,
            GetActiveMapForMultiplayer)
        {
            Owner = this
        };

        var hud = EnsureHudOverlay();
        hud.SetLocalDisplayName(window.CurrentDisplayName);

        window.RemoteTelemetryReceived += frame =>
        {
            RenderRemotePlayer(frame);
            hud.UpdateRemotePlayer(frame);
        };
        window.RemotePlayerLeft += playerId =>
        {
            RemoveRemotePlayerMarker(playerId);
            hud.RemoveRemotePlayer(playerId);
        };
        window.RemotePlayersReset += () =>
        {
            ClearRemotePlayerMarkers();
            hud.ClearRemotePlayers();
        };
        window.ChatMessageReceived += hud.AddChatMessage;
        window.RemoteSpeakerActive += hud.MarkRemoteSpeaker;
        window.VoiceError += hud.SetVoiceError;
        window.MultiplayerConnectionChanged += hud.SetConnectionState;
        window.LocalDisplayNameChanged += hud.SetLocalDisplayName;
        hud.ChatSubmitted += text => _ = window.SendChatFromOverlayAsync(text);
        hud.PushToTalkChanged += window.SetPushToTalk;

        window.Closed += (_, _) =>
        {
            ClearRemotePlayerMarkers();
            _multiplayerWindow = null;
            StopHudRefreshTimer();
            if (_hudOverlay is not null)
            {
                _hudOverlay.Close();
                _hudOverlay = null;
            }
        };

        _multiplayerWindow = window;
        UpdateHudLocalState();
        window.Show();
    }

    private HudOverlayWindow EnsureHudOverlay()
    {
        if (_hudOverlay is not null)
        {
            StartHudRefreshTimer();
            return _hudOverlay;
        }

        var hud = new HudOverlayWindow();
        hud.AttachOmsiProcess(_currentOmsi?.ProcessId);
        hud.UpdateLocalTelemetry(_lastTelemetry, GetActiveMapForMultiplayer());
        hud.Closed += (_, _) =>
        {
            if (ReferenceEquals(_hudOverlay, hud))
            {
                _hudOverlay = null;
            }

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

        _hudOverlay.AttachOmsiProcess(_currentOmsi?.ProcessId);
        _hudOverlay.UpdateLocalTelemetry(_lastTelemetry, GetActiveMapForMultiplayer());
    }

    private OmsiMapInfo? GetActiveMapForMultiplayer()
    {
        var mapName = _lastTelemetry?.MapName;
        return string.IsNullOrWhiteSpace(mapName)
            ? null
            : FindActiveMap(mapName);
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

            return;
        }

        var marker = GetOrCreateRemoteMarker(frame.Player.PlayerId, frame.Player.DisplayName, bitmap.PixelWidth);
        var markerSize = marker.Width;
        Canvas.SetLeft(marker, pixelX - markerSize / 2d);
        Canvas.SetTop(marker, pixelY - markerSize / 2d);

        if (marker.RenderTransform is RotateTransform rotation)
        {
            rotation.Angle = frame.Telemetry.HeadingDegrees;
        }

        marker.ToolTip = $"{frame.Player.DisplayName} • {frame.Telemetry.SpeedKph:F1} km/h";
        marker.Visibility = Visibility.Visible;
    }

    private Grid GetOrCreateRemoteMarker(string playerId, string displayName, int roadmapWidth)
    {
        if (_remotePlayerMarkers.TryGetValue(playerId, out var existing))
        {
            return existing;
        }

        var size = Math.Clamp(roadmapWidth * 0.014d, 30d, 90d);
        var marker = new Grid
        {
            Width = size,
            Height = size,
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            RenderTransform = new RotateTransform(),
            ToolTip = displayName
        };

        marker.Children.Add(new Ellipse
        {
            Fill = Brushes.DodgerBlue,
            Stroke = Brushes.White,
            StrokeThickness = Math.Max(2d, size * 0.07d)
        });

        marker.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(size * 0.50d, size * 0.12d),
                new(size * 0.69d, size * 0.70d),
                new(size * 0.50d, size * 0.58d),
                new(size * 0.31d, size * 0.70d)
            },
            Fill = Brushes.White,
            Stroke = Brushes.Black,
            StrokeThickness = 1d
        });

        Panel.SetZIndex(marker, 20);
        RoadmapCanvas.Children.Add(marker);
        _remotePlayerMarkers[playerId] = marker;
        return marker;
    }

    private void RemoveRemotePlayerMarker(string playerId)
    {
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
    }
}
