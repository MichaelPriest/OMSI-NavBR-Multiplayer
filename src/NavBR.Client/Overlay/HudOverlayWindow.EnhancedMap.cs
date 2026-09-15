using System.Windows;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Shared.Telemetry;

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
        MiniMapStatusText.MaxWidth = 180d;
        MiniMapStatusText.TextWrapping = TextWrapping.NoWrap;
        RenderEnhancedMiniMap();
    }

    /// <summary>
    /// Presentation layer for the in-game GPS. The local vehicle stays fixed
    /// pointing up while the roadmap, route and remote markers rotate beneath it.
    /// </summary>
    private void RenderEnhancedMiniMap()
    {
        var telemetry = _localTelemetry;
        var bitmap = _mapBitmap;
        var layout = _mapLayout;
        var map = _activeMap;

        MiniMapImage.Opacity = _hudSettings.HudMapOpacity;
        UpdateTripInfo(telemetry, map);

        if (telemetry is not null)
        {
            LocalMarkerRotation.Angle = 0d;
            MiniMapHeadingRotation.Angle = NormalizeAngle(-telemetry.HeadingDegrees);
        }
        else
        {
            MiniMapHeadingRotation.Angle = 0d;
        }

        var zoom = GetSmoothedHudZoom(telemetry);
        if (Math.Abs(MiniMapContentScale.ScaleX - zoom) > 0.002d)
        {
            MiniMapContentScale.ScaleX = zoom;
            MiniMapContentScale.ScaleY = zoom;
        }

        var safeZoom = Math.Max(0.01d, zoom);
        ActiveRoutePolyline.StrokeThickness = 4.5d / safeZoom;
        ActiveRouteShadow.StrokeThickness = 8d / safeZoom;

        if (map is not null)
        {
            MiniMapStatusText.Text = map.DisplayName;
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
            TurnPanel.Visibility = Visibility.Collapsed;
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

        UpdateTurnGuidance(
            telemetry,
            layout,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            localPixelX,
            localPixelY);
    }

    private void UpdateTripInfo(VehicleTelemetry? telemetry, OmsiMapInfo? map)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var lineLabel = language switch
        {
            "es" => "LÍNEA",
            "de" => "LINIE",
            "fr" => "LIGNE",
            _ => "LINHA"
        };
        var nextStopLabel = language switch
        {
            "es" => "Próxima parada",
            "de" => "Nächster Halt",
            "fr" => "Prochain arrêt",
            _ => "Próxima parada"
        };
        var noDestination = language switch
        {
            "es" => "Destino no informado",
            "de" => "Ziel nicht verfügbar",
            "fr" => "Destination indisponible",
            _ => "Destino não informado"
        };

        LineText.Text = string.IsNullOrWhiteSpace(telemetry?.Line)
            ? $"{lineLabel} —"
            : $"{lineLabel} {telemetry.Line}";

        DestinationText.Text = !string.IsNullOrWhiteSpace(telemetry?.DestinationName)
            ? telemetry.DestinationName
            : !string.IsNullOrWhiteSpace(telemetry?.Route)
                ? telemetry.Route
                : noDestination;

        NextStopText.Text = string.IsNullOrWhiteSpace(telemetry?.NextStopName)
            ? $"{nextStopLabel}: —"
            : $"{nextStopLabel}: {telemetry.NextStopName}";

        if (map is null && telemetry is null)
        {
            MiniMapStatusText.Text = "NavBR";
        }
    }

    private void EnsureRouteTrace(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        string? lineName,
        string? routeName,
        string? destinationName)
    {
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

    private void UpdateTurnGuidance(
        VehicleTelemetry telemetry,
        OmsiMapLayout layout,
        int bitmapWidth,
        int bitmapHeight,
        double localPixelX,
        double localPixelY)
    {
        if (_routeTracePoints.Count < 3 ||
            layout.TileSize is not double tileSize ||
            telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var localWorldX = gridX * tileSize + tileX;
        var localWorldY = gridY * tileSize + tileY;
        var nearestIndex = -1;
        var nearestDistanceSquared = double.MaxValue;

        for (var index = 0; index < _routeTracePoints.Count; index++)
        {
            var point = _routeTracePoints[index];
            var worldX = point.GridX * tileSize + point.TileX;
            var worldY = point.GridY * tileSize + point.TileY;
            var dx = worldX - localWorldX;
            var dy = worldY - localWorldY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestIndex = index;
            }
        }

        if (nearestIndex < 0 || Math.Sqrt(nearestDistanceSquared) > 90d)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var targetIndex = nearestIndex;
        var accumulated = 0d;
        for (var index = nearestIndex + 1; index < _routeTracePoints.Count; index++)
        {
            var previous = _routeTracePoints[index - 1];
            var current = _routeTracePoints[index];
            var previousX = previous.GridX * tileSize + previous.TileX;
            var previousY = previous.GridY * tileSize + previous.TileY;
            var currentX = current.GridX * tileSize + current.TileX;
            var currentY = current.GridY * tileSize + current.TileY;
            var segment = Math.Sqrt(
                Math.Pow(currentX - previousX, 2d) +
                Math.Pow(currentY - previousY, 2d));

            // A very large segment normally means tile-centre fallback geometry;
            // do not invent turn-by-turn instructions from coarse data.
            if (segment > 160d)
            {
                TurnPanel.Visibility = Visibility.Collapsed;
                return;
            }

            accumulated += segment;
            targetIndex = index;
            if (accumulated >= 55d)
            {
                break;
            }
        }

        if (targetIndex <= nearestIndex || accumulated < 18d || accumulated > 160d)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var target = _routeTracePoints[targetIndex];
        if (!RoadmapTransform.TryToPixel(
                layout,
                bitmapWidth,
                bitmapHeight,
                target.GridX,
                target.GridY,
                target.TileX,
                target.TileY,
                out var targetPixelX,
                out var targetPixelY))
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var dxPixels = targetPixelX - localPixelX;
        var dyPixels = targetPixelY - localPixelY;
        if (Math.Abs(dxPixels) < 0.01d && Math.Abs(dyPixels) < 0.01d)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var targetBearing = NormalizeAngle(Math.Atan2(dxPixels, -dyPixels) * 180d / Math.PI);
        var delta = NormalizeSignedAngle(targetBearing - telemetry.HeadingDegrees);

        if (Math.Abs(delta) < 18d)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var distance = Math.Max(10, (int)Math.Round(accumulated / 10d) * 10);
        var strongTurn = Math.Abs(delta) >= 52d;
        var turnRight = delta > 0d;

        TurnArrowText.Text = (turnRight, strongTurn) switch
        {
            (true, true) => "→",
            (true, false) => "↗",
            (false, true) => "←",
            _ => "↖"
        };
        TurnInstructionText.Text = BuildTurnInstruction(turnRight, strongTurn, distance);
        TurnPanel.Visibility = Visibility.Visible;
    }

    private static string BuildTurnInstruction(bool right, bool strongTurn, int distanceMeters)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return language switch
        {
            "es" => $"En {distanceMeters} m, {(strongTurn ? "gire" : "manténgase")} a la {(right ? "derecha" : "izquierda")}",
            "de" => $"In {distanceMeters} m {(strongTurn ? "abbiegen" : "halten")} nach {(right ? "rechts" : "links")}",
            "fr" => $"Dans {distanceMeters} m, {(strongTurn ? "tournez" : "restez")} à {(right ? "droite" : "gauche")}",
            "en" => $"In {distanceMeters} m, {(strongTurn ? "turn" : "keep")} {(right ? "right" : "left")}",
            _ => $"Em {distanceMeters} m, {(strongTurn ? "vire" : "mantenha")} à {(right ? "direita" : "esquerda")}"
        };
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360d;
        return angle < 0d ? angle + 360d : angle;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        angle = NormalizeAngle(angle);
        return angle > 180d ? angle - 360d : angle;
    }
}
