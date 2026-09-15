using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Maps;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private DispatcherTimer? _enhancedMapTimer;
    private string? _routeTraceCacheKey;
    private IReadOnlyList<OmsiRouteTracePoint> _routeTracePoints = Array.Empty<OmsiRouteTracePoint>();

    private void StartEnhancedMapRendering()
    {
        if (_enhancedMapTimer is null)
        {
            _enhancedMapTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _enhancedMapTimer.Tick += (_, _) => RenderEnhancedMiniMap();
            Closed += (_, _) => _enhancedMapTimer?.Stop();
        }

        if (!_enhancedMapTimer.IsEnabled)
        {
            _enhancedMapTimer.Start();
        }

        RenderEnhancedMiniMap();
    }

    private void RenderEnhancedMiniMap()
    {
        var telemetry = _localTelemetry;
        var bitmap = _mapBitmap;
        var layout = _mapLayout;
        var map = _activeMap;

        MiniMapImage.Opacity = _hudSettings.HudMapOpacity;

        if (map is not null)
        {
            var title = !string.IsNullOrWhiteSpace(telemetry?.Line)
                ? !string.IsNullOrWhiteSpace(telemetry?.Route)
                    ? $"{telemetry.Line} • {telemetry.Route}"
                    : telemetry.Line!
                : map.DisplayName;
            MiniMapStatusText.Text = title;
        }

        if (bitmap is null || map is null)
        {
            ActiveRoutePolyline.Visibility = Visibility.Collapsed;
            ActiveRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        if (telemetry is null ||
            layout is null ||
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
            RenderFittedRoadmap(bitmap);
            ActiveRoutePolyline.Visibility = Visibility.Collapsed;
            ActiveRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        const double canvasWidth = 318d;
        const double canvasHeight = 200d;
        const double baseSourceViewWidth = 720d;
        var zoom = GetSmoothedHudZoom(telemetry);
        var sourceViewWidth = baseSourceViewWidth / Math.Max(0.01d, zoom);
        var scale = canvasWidth / sourceViewWidth;

        MiniMapImage.Width = bitmap.PixelWidth * scale;
        MiniMapImage.Height = bitmap.PixelHeight * scale;
        Canvas.SetLeft(MiniMapImage, canvasWidth / 2d - localPixelX * scale);
        Canvas.SetTop(MiniMapImage, canvasHeight / 2d - localPixelY * scale);

        LocalMarker.Visibility = Visibility.Visible;
        LocalMarkerRotation.Angle = telemetry.HeadingDegrees;

        EnsureRouteTrace(map, layout, telemetry.Route);
        RenderRouteTrace(
            layout,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            localPixelX,
            localPixelY,
            scale,
            canvasWidth,
            canvasHeight);
    }

    private void RenderFittedRoadmap(System.Windows.Media.Imaging.BitmapImage bitmap)
    {
        const double canvasWidth = 318d;
        const double canvasHeight = 200d;
        var scale = Math.Min(
            canvasWidth / Math.Max(1d, bitmap.PixelWidth),
            canvasHeight / Math.Max(1d, bitmap.PixelHeight));

        MiniMapImage.Width = bitmap.PixelWidth * scale;
        MiniMapImage.Height = bitmap.PixelHeight * scale;
        Canvas.SetLeft(MiniMapImage, (canvasWidth - MiniMapImage.Width) / 2d);
        Canvas.SetTop(MiniMapImage, (canvasHeight - MiniMapImage.Height) / 2d);
        LocalMarker.Visibility = Visibility.Collapsed;
    }

    private void EnsureRouteTrace(OmsiMapInfo map, OmsiMapLayout layout, string? routeName)
    {
        var cacheKey = $"{map.DirectoryPath}|{routeName}";
        if (string.Equals(cacheKey, _routeTraceCacheKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _routeTraceCacheKey = cacheKey;
        _routeTracePoints = OmsiRouteTraceReader.TryRead(map, layout, routeName);
    }

    private void RenderRouteTrace(
        OmsiMapLayout layout,
        int bitmapWidth,
        int bitmapHeight,
        double localPixelX,
        double localPixelY,
        double scale,
        double canvasWidth,
        double canvasHeight)
    {
        if (_routeTracePoints.Count < 2)
        {
            ActiveRoutePolyline.Visibility = Visibility.Collapsed;
            ActiveRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        var points = new PointCollection(_routeTracePoints.Count);
        foreach (var routePoint in _routeTracePoints)
        {
            if (!RoadmapTransform.TryToPixel(
                    layout,
                    bitmapWidth,
                    bitmapHeight,
                    routePoint.GridX,
                    routePoint.GridY,
                    routePoint.TileX,
                    routePoint.TileY,
                    out var pixelX,
                    out var pixelY))
            {
                continue;
            }

            points.Add(new Point(
                canvasWidth / 2d + (pixelX - localPixelX) * scale,
                canvasHeight / 2d + (pixelY - localPixelY) * scale));
        }

        if (points.Count < 2)
        {
            ActiveRoutePolyline.Visibility = Visibility.Collapsed;
            ActiveRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        ActiveRoutePolyline.Points = points;
        ActiveRouteShadow.Points = points.Clone();
        ActiveRoutePolyline.Visibility = Visibility.Visible;
        ActiveRouteShadow.Visibility = Visibility.Visible;
    }
}
