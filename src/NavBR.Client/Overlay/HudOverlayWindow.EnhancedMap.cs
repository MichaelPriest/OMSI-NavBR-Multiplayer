using System.Windows;
using System.Windows.Media;
using NavBR.Client.Maps;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private string? _routeTraceCacheKey;
    private IReadOnlyList<OmsiRouteTracePoint> _routeTracePoints = Array.Empty<OmsiRouteTracePoint>();
    private bool _enhancedMapRenderingStarted;

    private void StartEnhancedMapRendering()
    {
        if (_enhancedMapRenderingStarted)
        {
            return;
        }

        _enhancedMapRenderingStarted = true;
        MiniMapStatusText.MaxWidth = 238d;
        MiniMapStatusText.TextWrapping = TextWrapping.Wrap;
        RenderEnhancedMiniMap();
    }

    /// <summary>
    /// Applies presentation-only enhancements to the minimap.
    ///
    /// IMPORTANT: this method must not change MiniMapImage Width/Height/Left/Top.
    /// HudOverlayWindow.RenderMiniMap remains the single source of bitmap geometry.
    /// Keeping one geometry renderer prevents the roadmap from alternating between
    /// two different view sizes, which caused visible flicker in alpha.7.
    /// </summary>
    private void RenderEnhancedMiniMap()
    {
        var telemetry = _localTelemetry;
        var bitmap = _mapBitmap;
        var layout = _mapLayout;
        var map = _activeMap;

        MiniMapImage.Opacity = _hudSettings.HudMapOpacity;

        // GTA-like zoom is applied around the center of the already-positioned
        // canvas. The local vehicle marker lives outside MiniMapCanvas, so it
        // stays fixed while the map moves/zooms underneath it.
        var zoom = GetSmoothedHudZoom(telemetry);
        if (Math.Abs(MiniMapContentScale.ScaleX - zoom) > 0.002d)
        {
            MiniMapContentScale.ScaleX = zoom;
            MiniMapContentScale.ScaleY = zoom;
        }

        // The polyline lives inside the scaled canvas. Compensate its stroke so
        // 10x zoom does not turn the route into an oversized band on screen.
        var safeZoom = Math.Max(0.01d, zoom);
        ActiveRoutePolyline.StrokeThickness = 4.5d / safeZoom;
        ActiveRouteShadow.StrokeThickness = 8d / safeZoom;

        if (map is not null)
        {
            MiniMapStatusText.Text = BuildMiniMapTitle(map, telemetry);
        }

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
            ActiveRoutePolyline.Visibility = Visibility.Collapsed;
            ActiveRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        EnsureRouteTrace(
            map,
            layout,
            telemetry.Line,
            telemetry.Route,
            telemetry.DestinationName);
        RenderRouteTrace(
            layout,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            localPixelX,
            localPixelY);
    }

    private static string BuildMiniMapTitle(OmsiMapInfo map, NavBR.Shared.Telemetry.VehicleTelemetry? telemetry)
    {
        string title;
        if (!string.IsNullOrWhiteSpace(telemetry?.Line))
        {
            title = $"Linha {telemetry.Line}";

            if (!string.IsNullOrWhiteSpace(telemetry.DestinationName))
            {
                title += $" • Destino: {telemetry.DestinationName}";
            }
            else if (!string.IsNullOrWhiteSpace(telemetry.Route))
            {
                // Keep a useful fallback for maps/patches that expose only the
                // track field, while preferring the passenger-facing destination
                // whenever OMSI provides it separately.
                title += $" • Rota: {telemetry.Route}";
            }
        }
        else
        {
            title = $"{map.DisplayName} • Sem linha ativa";
        }

        if (!string.IsNullOrWhiteSpace(telemetry?.NextStopName))
        {
            title += $"\nPróx.: {telemetry.NextStopName}";
        }

        return title;
    }

    private void EnsureRouteTrace(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        string? lineName,
        string? routeName,
        string? destinationName)
    {
        // Some OMSI maps/patches expose the passenger-facing destination but
        // leave the technical track/route field empty. OmsiRouteTraceReader can
        // resolve line + destination through the .ttp [trip] block to the real
        // .ttr, so keep display semantics separate while still using destination
        // as a lookup fallback.
        var lookupTarget = !string.IsNullOrWhiteSpace(routeName)
            ? routeName
            : destinationName;
        var cacheKey = $"{map.DirectoryPath}|{lineName}|{routeName}|{destinationName}";
        if (string.Equals(cacheKey, _routeTraceCacheKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _routeTraceCacheKey = cacheKey;
        _routeTracePoints = OmsiRouteTraceReader.TryRead(map, layout, lookupTarget, lineName);
    }

    private void RenderRouteTrace(
        OmsiMapLayout layout,
        int bitmapWidth,
        int bitmapHeight,
        double localPixelX,
        double localPixelY)
    {
        if (_routeTracePoints.Count < 2)
        {
            ActiveRoutePolyline.Visibility = Visibility.Collapsed;
            ActiveRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        // These values intentionally match the legacy renderer that owns
        // MiniMapImage geometry. Zoom is applied afterwards by the Canvas
        // ScaleTransform, so route and roadmap remain perfectly aligned.
        const double canvasWidth = 296d;
        const double canvasHeight = 186d;
        const double sourceViewWidth = 900d;
        var scale = canvasWidth / sourceViewWidth;

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
