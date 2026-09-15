using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private readonly Dictionary<string, FrameworkElement> _busStopMarkers = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<OmsiBusStopPoint> _busStops = Array.Empty<OmsiBusStopPoint>();
    private DispatcherTimer? _busStopRenderTimer;
    private string? _busStopMapKey;
    private string? _busStopMarkerStyleKey;
    private BitmapSource? _customBusStopIcon;

    private void MiniMapCanvas_BusStopsLoaded(object sender, RoutedEventArgs e)
    {
        if (_busStopRenderTimer is not null)
        {
            return;
        }

        _busStopRenderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _busStopRenderTimer.Tick += BusStopRenderTimer_Tick;
        _busStopRenderTimer.Start();
        MultiplayerSettingsStore.SettingsSaved += BusStopSettingsSaved;
        Closed += BusStopWindow_Closed;
        RenderBusStops();
    }

    private void BusStopRenderTimer_Tick(object? sender, EventArgs e) => RenderBusStops();

    private void BusStopSettingsSaved(MultiplayerSettings settings)
    {
        var styleKey = BuildStopStyleKey(settings);
        if (!string.Equals(styleKey, _busStopMarkerStyleKey, StringComparison.OrdinalIgnoreCase))
        {
            _hudSettings = settings;
            InvalidateBusStopMarkers();
        }
    }

    private void BusStopWindow_Closed(object? sender, EventArgs e)
    {
        if (_busStopRenderTimer is not null)
        {
            _busStopRenderTimer.Stop();
            _busStopRenderTimer.Tick -= BusStopRenderTimer_Tick;
            _busStopRenderTimer = null;
        }

        MultiplayerSettingsStore.SettingsSaved -= BusStopSettingsSaved;
        Closed -= BusStopWindow_Closed;
    }

    private void InvalidateBusStopMarkers()
    {
        foreach (var marker in _busStopMarkers.Values)
        {
            MiniMapCanvas.Children.Remove(marker);
        }

        _busStopMarkers.Clear();
        _busStopMarkerStyleKey = null;
        _customBusStopIcon = null;
        RenderBusStops();
    }

    private void RenderBusStops()
    {
        var map = _activeMap;
        var layout = _mapLayout;
        var bitmap = _mapBitmap;
        var telemetry = _localTelemetry;
        if (map is null ||
            layout is null ||
            bitmap is null ||
            telemetry is null ||
            telemetry.GridX is not int localGridX ||
            telemetry.GridY is not int localGridY ||
            telemetry.TileX is not double localTileX ||
            telemetry.TileY is not double localTileY ||
            !RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                localGridX,
                localGridY,
                localTileX,
                localTileY,
                out var localPixelX,
                out var localPixelY))
        {
            HideBusStopMarkers();
            return;
        }

        var mapKey = $"{map.DirectoryPath}|{map.GlobalConfigPath}";
        if (!string.Equals(mapKey, _busStopMapKey, StringComparison.OrdinalIgnoreCase))
        {
            _busStopMapKey = mapKey;
            _busStops = OmsiBusStopReader.TryRead(map);
            InvalidateBusStopMarkers();
        }

        var styleKey = BuildStopStyleKey(_hudSettings);
        if (!string.Equals(styleKey, _busStopMarkerStyleKey, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var marker in _busStopMarkers.Values)
            {
                MiniMapCanvas.Children.Remove(marker);
            }
            _busStopMarkers.Clear();
            _customBusStopIcon = null;
            _busStopMarkerStyleKey = styleKey;
        }

        const double canvasWidth = 296d;
        const double canvasHeight = 186d;
        const double sourceViewWidth = 900d;
        var scale = canvasWidth / sourceViewWidth;
        var zoom = Math.Max(0.65d, MiniMapContentScale.ScaleX);
        var nextStopKey = FindNearestNextStopKey(
            telemetry.NextStopName,
            layout,
            localGridX,
            localGridY,
            localTileX,
            localTileY);
        var visibleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stop in _busStops)
        {
            if (!RoadmapTransform.TryToPixel(
                    layout,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    stop.GridX,
                    stop.GridY,
                    stop.TileX,
                    stop.TileY,
                    out var pixelX,
                    out var pixelY))
            {
                continue;
            }

            var x = canvasWidth / 2d + (pixelX - localPixelX) * scale;
            var y = canvasHeight / 2d + (pixelY - localPixelY) * scale;
            if (x < -45d || x > canvasWidth + 45d || y < -45d || y > canvasHeight + 45d)
            {
                continue;
            }

            var key = StopKey(stop);
            var isNext = string.Equals(key, nextStopKey, StringComparison.OrdinalIgnoreCase);
            var marker = GetOrCreateBusStopMarker(stop, isNext);
            UpdateBusStopMarkerVisual(marker, stop, isNext, telemetry.HeadingDegrees, zoom);
            Canvas.SetLeft(marker, x - marker.Width / 2d);
            Canvas.SetTop(marker, y - marker.Height / 2d);
            marker.Visibility = Visibility.Visible;
            visibleKeys.Add(key);
        }

        foreach (var pair in _busStopMarkers)
        {
            if (!visibleKeys.Contains(pair.Key))
            {
                pair.Value.Visibility = Visibility.Collapsed;
            }
        }
    }

    private string? FindNearestNextStopKey(
        string? nextStopName,
        OmsiMapLayout layout,
        int localGridX,
        int localGridY,
        double localTileX,
        double localTileY)
    {
        if (string.IsNullOrWhiteSpace(nextStopName) || layout.TileSize is not double tileSize)
        {
            return null;
        }

        var normalizedTarget = NormalizeStopName(nextStopName);
        if (normalizedTarget.Length == 0)
        {
            return null;
        }

        var localWorldX = localGridX * tileSize + localTileX;
        var localWorldY = localGridY * tileSize + localTileY;
        OmsiBusStopPoint? nearest = null;
        var nearestDistanceSquared = double.MaxValue;

        foreach (var stop in _busStops)
        {
            var normalizedName = NormalizeStopName(stop.Name);
            if (normalizedName.Length == 0 ||
                (!string.Equals(normalizedName, normalizedTarget, StringComparison.Ordinal) &&
                 !normalizedName.Contains(normalizedTarget, StringComparison.Ordinal) &&
                 !normalizedTarget.Contains(normalizedName, StringComparison.Ordinal)))
            {
                continue;
            }

            var worldX = stop.GridX * tileSize + stop.TileX;
            var worldY = stop.GridY * tileSize + stop.TileY;
            var dx = worldX - localWorldX;
            var dy = worldY - localWorldY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = stop;
            }
        }

        return nearest is null ? null : StopKey(nearest);
    }

    private FrameworkElement GetOrCreateBusStopMarker(OmsiBusStopPoint stop, bool isNext)
    {
        var key = StopKey(stop);
        if (_busStopMarkers.TryGetValue(key, out var marker))
        {
            return marker;
        }

        marker = BuildBusStopMarker(isNext);
        marker.ToolTip = stop.Name;
        Panel.SetZIndex(marker, isNext ? 31 : 24);
        MiniMapCanvas.Children.Add(marker);
        _busStopMarkers[key] = marker;
        return marker;
    }

    private FrameworkElement BuildBusStopMarker(bool isNext)
    {
        var size = isNext ? 20d : 14d;
        if (string.Equals(_hudSettings.StopIconStyle, "custom", StringComparison.OrdinalIgnoreCase) &&
            TryLoadCustomBusStopIcon() is BitmapSource customIcon)
        {
            return new Image
            {
                Width = size,
                Height = size,
                Source = customIcon,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5d, 0.5d)
            };
        }

        if (string.Equals(_hudSettings.StopIconStyle, "dot", StringComparison.OrdinalIgnoreCase))
        {
            return new Ellipse
            {
                Width = size,
                Height = size,
                Fill = isNext ? Brushes.Orange : Brushes.White,
                Stroke = isNext ? Brushes.White : new SolidColorBrush(Color.FromRgb(23, 139, 67)),
                StrokeThickness = isNext ? 2.2d : 1.8d,
                RenderTransformOrigin = new Point(0.5d, 0.5d)
            };
        }

        // NavBR vector rendering of the classic OMSI/German H bus-stop sign.
        // It keeps the familiar OMSI appearance without redistributing a game asset.
        var grid = new Grid
        {
            Width = size,
            Height = size,
            RenderTransformOrigin = new Point(0.5d, 0.5d)
        };
        grid.Children.Add(new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromRgb(247, 211, 47)),
            Stroke = new SolidColorBrush(Color.FromRgb(24, 122, 55)),
            StrokeThickness = isNext ? 2.5d : 2d
        });
        grid.Children.Add(new TextBlock
        {
            Text = "H",
            Foreground = new SolidColorBrush(Color.FromRgb(19, 102, 47)),
            FontSize = isNext ? 12d : 8d,
            FontWeight = FontWeights.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        });
        return grid;
    }

    private void UpdateBusStopMarkerVisual(
        FrameworkElement marker,
        OmsiBusStopPoint stop,
        bool isNext,
        double headingDegrees,
        double zoom)
    {
        var wantedSize = isNext ? 20d : 14d;
        if (Math.Abs(marker.Width - wantedSize) > 0.1d || Math.Abs(marker.Height - wantedSize) > 0.1d)
        {
            // Rebuild when the same marker transitions to/from next stop so the
            // H glyph and border also receive the correct emphasis.
            MiniMapCanvas.Children.Remove(marker);
            _busStopMarkers.Remove(StopKey(stop));
            marker = GetOrCreateBusStopMarker(stop, isNext);
        }

        marker.ToolTip = isNext ? $"Próxima parada: {stop.Name}" : stop.Name;
        Panel.SetZIndex(marker, isNext ? 31 : 24);

        var transforms = new TransformGroup();
        transforms.Children.Add(new RotateTransform(headingDegrees));
        transforms.Children.Add(new ScaleTransform(1d / zoom, 1d / zoom));
        marker.RenderTransform = transforms;
    }

    private BitmapSource? TryLoadCustomBusStopIcon()
    {
        if (_customBusStopIcon is not null)
        {
            return _customBusStopIcon;
        }

        var path = _hudSettings.StopCustomIconPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            _customBusStopIcon = image;
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void HideBusStopMarkers()
    {
        foreach (var marker in _busStopMarkers.Values)
        {
            marker.Visibility = Visibility.Collapsed;
        }
    }

    private static string BuildStopStyleKey(MultiplayerSettings settings) =>
        $"{settings.StopIconStyle}|{settings.StopCustomIconPath}";

    private static string StopKey(OmsiBusStopPoint stop) =>
        $"{stop.GridX}:{stop.GridY}:{stop.ObjectId}";

    private static string NormalizeStopName(string value) =>
        new(value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
