using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Maps;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _presenceTimer;
    private readonly HashSet<int> _pressedKeys = [];
    private readonly List<ChatMessage> _chatMessages = [];
    private readonly Dictionary<string, PlayerTelemetryFrame> _remotePlayers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameworkElement> _remoteMarkers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string DisplayName, DateTimeOffset LastFrame)> _speakers = new(StringComparer.OrdinalIgnoreCase);

    private GlobalKeyboardHook? _keyboardHook;
    private int? _omsiProcessId;
    private IntPtr _omsiWindowHandle;
    private bool _chatInteractive;
    private bool _localPushToTalk;
    private string _localDisplayName = "Driver";
    private DateTimeOffset _lastChatActivity = DateTimeOffset.MinValue;
    private VehicleTelemetry? _localTelemetry;
    private OmsiMapInfo? _activeMap;
    private BitmapImage? _mapBitmap;
    private OmsiMapLayout? _mapLayout;
    private string? _loadedRoadmapPath;

    public event Action<string>? ChatSubmitted;
    public event Action<bool>? PushToTalkChanged;

    public HudOverlayWindow()
    {
        InitializeComponent();

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += (_, _) => FollowOmsiWindow();

        _presenceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _presenceTimer.Tick += (_, _) =>
        {
            RenderVoiceState();
            RenderChatVisibility();
        };

        SourceInitialized += (_, _) => SetInteractive(false);
        Loaded += (_, _) =>
        {
            InstallConflictFreeHotkeys();
            InitializeImmersiveOperationHud();
            _positionTimer.Start();
            _presenceTimer.Start();
            FollowOmsiWindow();
        };
        Closed += (_, _) =>
        {
            _positionTimer.Stop();
            _presenceTimer.Stop();
            _keyboardHook?.Dispose();
            _keyboardHook = null;
        };
    }

    public void AttachOmsiProcess(int? processId)
    {
        _omsiProcessId = processId;
        _omsiWindowHandle = IntPtr.Zero;
        FollowOmsiWindow();
    }

    public void SetLocalDisplayName(string? displayName)
    {
        _localDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "Driver"
            : displayName.Trim();
    }

    public void SetConnectionState(bool connected, string? roomId = null)
    {
        ConnectionDot.Fill = connected ? Brushes.LimeGreen : Brushes.Gray;
        HudStatusText.Text = connected
            ? string.IsNullOrWhiteSpace(roomId)
                ? "NavBR online"
                : $"Sala {roomId}"
            : "NavBR offline";
    }

    public void UpdateLocalTelemetry(VehicleTelemetry? telemetry, OmsiMapInfo? activeMap)
    {
        _localTelemetry = telemetry;
        _activeMap = activeMap;
        EnsureRoadmapLoaded(activeMap);
        RenderMiniMap();
        RefreshTelematrixPanel();
    }

    public void UpdateRemotePlayer(PlayerTelemetryFrame frame)
    {
        _remotePlayers[frame.Player.PlayerId] = frame;
        RenderMiniMap();
    }

    public void RemoveRemotePlayer(string playerId)
    {
        _remotePlayers.Remove(playerId);
        _speakers.Remove(playerId);

        if (_remoteMarkers.Remove(playerId, out var marker))
        {
            MiniMapCanvas.Children.Remove(marker);
        }

        RemoveRemoteNameplate(playerId);
        RenderVoiceState();
    }

    public void ClearRemotePlayers()
    {
        _remotePlayers.Clear();
        _speakers.Clear();

        foreach (var marker in _remoteMarkers.Values)
        {
            MiniMapCanvas.Children.Remove(marker);
        }

        _remoteMarkers.Clear();
        ClearRemoteNameplates();
        RenderVoiceState();
    }

    public void AddChatMessage(ChatMessage message)
    {
        _chatMessages.Add(message);
        if (_chatMessages.Count > 6)
        {
            _chatMessages.RemoveRange(0, _chatMessages.Count - 6);
        }

        _lastChatActivity = DateTimeOffset.UtcNow;
        ChatLinesText.Text = string.Join(
            Environment.NewLine,
            _chatMessages.Select(item => $"{item.DisplayName}: {item.Text}"));
        ChatPanel.Visibility = Visibility.Visible;
    }

    public void MarkRemoteSpeaker(string playerId, string? displayName)
    {
        _speakers[playerId] = (
            string.IsNullOrWhiteSpace(displayName) ? playerId : displayName.Trim(),
            DateTimeOffset.UtcNow);
        RenderVoiceState();
    }

    public void SetVoiceError(string message)
    {
        VoiceDot.Fill = Brushes.OrangeRed;
        VoiceStatusText.Text = $"Voz: {message}";
        VoicePanel.Visibility = Visibility.Visible;
    }

    private void SetLocalPushToTalk(bool active)
    {
        if (_localPushToTalk == active)
        {
            return;
        }

        _localPushToTalk = active;
        PushToTalkChanged?.Invoke(active);
        RenderVoiceState();
    }

    private void OpenChatInput()
    {
        SetLocalPushToTalk(false);
        _chatInteractive = true;
        ChatInputPanel.Visibility = Visibility.Visible;
        SetInteractive(true);
        Show();
        Activate();
        ChatInputBox.Focus();
    }

    private void CloseChatInput()
    {
        ChatInputBox.Clear();
        ChatInputPanel.Visibility = Visibility.Collapsed;
        _chatInteractive = false;
        SetInteractive(false);
    }

    private void ChatInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseChatInput();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        var text = ChatInputBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            ChatSubmitted?.Invoke(text);
        }

        CloseChatInput();
        e.Handled = true;
    }

    private void RenderVoiceState()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var stale in _speakers
                     .Where(item => now - item.Value.LastFrame > TimeSpan.FromMilliseconds(700))
                     .Select(item => item.Key)
                     .ToArray())
        {
            _speakers.Remove(stale);
        }

        if (_localPushToTalk)
        {
            VoiceDot.Fill = Brushes.LimeGreen;
            VoiceStatusText.Text = $"{_localDisplayName} falando";
            VoicePanel.Visibility = Visibility.Visible;
            return;
        }

        if (_speakers.Count > 0)
        {
            VoiceDot.Fill = Brushes.DeepSkyBlue;
            VoiceStatusText.Text = string.Join(", ", _speakers.Values.Select(value => value.DisplayName)) + " falando";
            VoicePanel.Visibility = Visibility.Visible;
            return;
        }

        VoicePanel.Visibility = Visibility.Collapsed;
    }

    private void RenderChatVisibility()
    {
        if (_chatInteractive)
        {
            ChatPanel.Visibility = Visibility.Visible;
            return;
        }

        if (_chatMessages.Count == 0 ||
            DateTimeOffset.UtcNow - _lastChatActivity > TimeSpan.FromSeconds(12))
        {
            ChatPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void EnsureRoadmapLoaded(OmsiMapInfo? map)
    {
        if (map is null || string.IsNullOrWhiteSpace(map.RoadmapPath))
        {
            _mapBitmap = null;
            _mapLayout = null;
            _loadedRoadmapPath = null;
            MiniMapImage.Source = null;
            return;
        }

        if (string.Equals(_loadedRoadmapPath, map.RoadmapPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(map.RoadmapPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            _mapBitmap = bitmap;
            _mapLayout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
            _loadedRoadmapPath = map.RoadmapPath;
            MiniMapImage.Source = bitmap;
        }
        catch
        {
            _mapBitmap = null;
            _mapLayout = null;
            _loadedRoadmapPath = null;
            MiniMapImage.Source = null;
        }
    }

    private void RenderMiniMap()
    {
        var telemetry = _localTelemetry;
        var bitmap = _mapBitmap;
        var layout = _mapLayout;
        var map = _activeMap;

        if (telemetry is null ||
            bitmap is null ||
            layout is null ||
            map is null ||
            telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY ||
            !RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                gridX,
                gridY,
                tileX,
                tileY,
                out var localPixelX,
                out var localPixelY))
        {
            MiniMapStatusText.Text = telemetry?.MapName ?? "Sem mapa";
            LocalMarker.Visibility = Visibility.Collapsed;
            HideAllRemoteMarkers();
            return;
        }

        LocalMarker.Visibility = Visibility.Visible;
        LocalMarkerRotation.Angle = telemetry.HeadingDegrees;
        MiniMapStatusText.Text = string.IsNullOrWhiteSpace(telemetry.MapName)
            ? "NavBR"
            : telemetry.MapName;

        const double canvasWidth = 296d;
        const double canvasHeight = 186d;
        const double sourceViewWidth = 900d;
        var scale = canvasWidth / sourceViewWidth;

        MiniMapImage.Width = bitmap.PixelWidth * scale;
        MiniMapImage.Height = bitmap.PixelHeight * scale;
        Canvas.SetLeft(MiniMapImage, canvasWidth / 2d - localPixelX * scale);
        Canvas.SetTop(MiniMapImage, canvasHeight / 2d - localPixelY * scale);

        foreach (var frame in _remotePlayers.Values)
        {
            RenderRemoteMarker(
                frame,
                map,
                layout,
                bitmap,
                localPixelX,
                localPixelY,
                scale,
                canvasWidth,
                canvasHeight);
        }
    }

    private void RenderRemoteMarker(
        PlayerTelemetryFrame frame,
        OmsiMapInfo localMap,
        OmsiMapLayout layout,
        BitmapImage bitmap,
        double localPixelX,
        double localPixelY,
        double scale,
        double canvasWidth,
        double canvasHeight)
    {
        var telemetry = frame.Telemetry;
        var compatible = string.IsNullOrWhiteSpace(localMap.CompatibilityId) ||
                         string.IsNullOrWhiteSpace(frame.Player.MapCompatibilityId) ||
                         string.Equals(
                             localMap.CompatibilityId,
                             frame.Player.MapCompatibilityId,
                             StringComparison.OrdinalIgnoreCase);

        if (!compatible ||
            telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY ||
            !RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                gridX,
                gridY,
                tileX,
                tileY,
                out var pixelX,
                out var pixelY))
        {
            HideRemoteMarker(frame.Player.PlayerId);
            return;
        }

        var x = canvasWidth / 2d + (pixelX - localPixelX) * scale;
        var y = canvasHeight / 2d + (pixelY - localPixelY) * scale;
        if (x < -12 || x > canvasWidth + 12 || y < -12 || y > canvasHeight + 12)
        {
            HideRemoteMarker(frame.Player.PlayerId);
            return;
        }

        var marker = GetOrCreateRemoteMarker(frame.Player.PlayerId, frame.Player.DisplayName);
        UpdateRemoteMarkerVisual(
            marker,
            frame.Player.DisplayName,
            telemetry.HeadingDegrees,
            MiniMapHeadingRotation.Angle,
            MiniMapContentScale.ScaleX);
        Canvas.SetLeft(marker, x - marker.Width / 2d);
        Canvas.SetTop(marker, y - marker.Height / 2d);
        marker.ToolTip = $"{frame.Player.DisplayName} • {telemetry.SpeedKph:F1} km/h";
        marker.Visibility = Visibility.Visible;
    }

    private FrameworkElement GetOrCreateRemoteMarker(string playerId, string displayName)
    {
        if (_remoteMarkers.TryGetValue(playerId, out var existing))
        {
            UpdateRemoteMarkerName(existing, displayName);
            return existing;
        }

        // Modern navigation marker shared by the new HUDs: layered ring,
        // soft inner halo and a clean vector pointer. The local player uses
        // NavBR amber; remote players use interaction blue.
        var marker = new Grid
        {
            Width = 126d,
            Height = 42d,
            ToolTip = displayName,
            ClipToBounds = false,
            RenderTransformOrigin = new Point(0.5d, 0.5d)
        };

        var icon = new Grid
        {
            Width = 42d,
            Height = 42d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            Tag = "remote-player-icon"
        };
        icon.Children.Add(new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromArgb(227, 10, 17, 24)),
            Stroke = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
            StrokeThickness = 2.1d
        });
        icon.Children.Add(new Ellipse
        {
            Width = 34d,
            Height = 34d,
            Fill = new SolidColorBrush(Color.FromArgb(224, 18, 38, 52)),
            Stroke = new SolidColorBrush(Color.FromArgb(88, 255, 255, 255)),
            StrokeThickness = 1d
        });
        icon.Children.Add(new Ellipse
        {
            Width = 27d,
            Height = 27d,
            Fill = new SolidColorBrush(Color.FromArgb(54, 113, 198, 255))
        });
        icon.Children.Add(new Path
        {
            Data = Geometry.Parse("M 21,5 L 31,32 L 21,26.8 L 11,32 Z"),
            Fill = new SolidColorBrush(Color.FromRgb(113, 198, 255)),
            Stroke = Brushes.White,
            StrokeThickness = 1.5d,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        icon.Children.Add(new Ellipse
        {
            Width = 5.5d,
            Height = 5.5d,
            Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        marker.Children.Add(icon);

        var nameText = new TextBlock
        {
            Text = NormalizeRemoteDisplayName(displayName),
            Foreground = Brushes.White,
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 108d,
            Tag = "remote-player-name"
        };
        var namePlate = new Border
        {
            Padding = new Thickness(6d, 2d, 6d, 2d),
            Background = new SolidColorBrush(Color.FromArgb(220, 4, 15, 23)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 113, 198, 255)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(5d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, -24d, 0d, 0d),
            Child = nameText,
            Tag = "remote-player-nameplate"
        };
        marker.Children.Add(namePlate);

        Panel.SetZIndex(marker, 20);
        MiniMapCanvas.Children.Add(marker);
        _remoteMarkers[playerId] = marker;
        return marker;
    }

    private static void UpdateRemoteMarkerVisual(
        FrameworkElement marker,
        string displayName,
        double remoteHeadingDegrees,
        double mapRotationDegrees,
        double mapScale)
    {
        UpdateRemoteMarkerName(marker, displayName);

        if (marker is not Grid markerGrid)
        {
            return;
        }

        var safeScale = Math.Max(0.01d, Math.Abs(mapScale));
        markerGrid.RenderTransform = new TransformGroup
        {
            Children = new TransformCollection
            {
                new ScaleTransform(1d / safeScale, 1d / safeScale),
                // The minimap canvas rotates under the local bus. Counter-rotate
                // the marker shell so the username always stays upright.
                new RotateTransform(-mapRotationDegrees)
            }
        };

        var icon = markerGrid.Children
            .OfType<Grid>()
            .FirstOrDefault(child => string.Equals(child.Tag as string, "remote-player-icon", StringComparison.Ordinal));
        if (icon is null)
        {
            return;
        }

        icon.RenderTransform = new RotateTransform(
            NormalizeMarkerAngle(remoteHeadingDegrees + mapRotationDegrees),
            19d,
            19d);
    }

    private static void UpdateRemoteMarkerName(FrameworkElement marker, string displayName)
    {
        if (marker is not Grid markerGrid)
        {
            return;
        }

        var text = markerGrid.Children
            .OfType<Border>()
            .Select(border => border.Child)
            .OfType<TextBlock>()
            .FirstOrDefault(block => string.Equals(block.Tag as string, "remote-player-name", StringComparison.Ordinal));
        if (text is not null)
        {
            text.Text = NormalizeRemoteDisplayName(displayName);
        }
    }

    private static string NormalizeRemoteDisplayName(string? displayName)
    {
        var value = string.IsNullOrWhiteSpace(displayName)
            ? "Driver"
            : displayName.Trim();
        return value.Length <= 28 ? value : value[..28];
    }

    private static double NormalizeMarkerAngle(double angle)
    {
        angle %= 360d;
        return angle < 0d ? angle + 360d : angle;
    }

    private void HideRemoteMarker(string playerId)
    {
        if (_remoteMarkers.TryGetValue(playerId, out var marker))
        {
            marker.Visibility = Visibility.Collapsed;
        }
    }

    private void HideAllRemoteMarkers()
    {
        foreach (var marker in _remoteMarkers.Values)
        {
            marker.Visibility = Visibility.Collapsed;
        }
    }

    private void FollowOmsiWindow()
    {
        if (_omsiProcessId is not int processId)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect))
            {
                return;
            }

            _omsiWindowHandle = handle;
            var dpi = GetDpiForWindow(handle);
            var scale = dpi > 0 ? 96d / dpi : 1d;

            Left = rect.Left * scale;
            Top = rect.Top * scale;
            Width = Math.Max(1d, (rect.Right - rect.Left) * scale);
            Height = Math.Max(1d, (rect.Bottom - rect.Top) * scale);
        }
        catch
        {
            _omsiWindowHandle = IntPtr.Zero;
        }
    }

    private bool IsOmsiForeground()
    {
        if (_omsiWindowHandle == IntPtr.Zero)
        {
            FollowOmsiWindow();
        }

        return _omsiWindowHandle != IntPtr.Zero &&
               GetForegroundWindow() == _omsiWindowHandle;
    }

    private void SetInteractive(bool interactive)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle) | WsExToolWindow;
        if (interactive)
        {
            style &= ~WsExTransparent;
            style &= ~WsExNoActivate;
        }
        else
        {
            style |= WsExTransparent;
            style |= WsExNoActivate;
        }

        SetWindowLong(handle, GwlExStyle, style);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
