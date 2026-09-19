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
    private readonly OmsiRouteRejoinPathfinder _hudRouteRejoinPathfinder = new();
    private bool _enhancedMapRenderingStarted;

    private void StartEnhancedMapRendering()
    {
        if (_enhancedMapRenderingStarted)
        {
            return;
        }

        _enhancedMapRenderingStarted = true;
        MiniMapStatusText.MaxWidth = 235d;
        MiniMapStatusText.TextWrapping = TextWrapping.NoWrap;
        RenderEnhancedMiniMap();
    }

    /// <summary>
    /// Presentation layer for the in-game GPS. The local vehicle stays fixed
    /// pointing up while the roadmap, route and remote markers rotate beneath it.
    /// Route progress, remaining distance, next-stop distance and off-route state
    /// are all calculated by NavBRNavigationEngine so desktop/HUD use one source.
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
        RejoinRoutePolyline.StrokeThickness = 3.8d / safeZoom;
        RejoinRouteShadow.StrokeThickness = 7d / safeZoom;

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
            RejoinRoutePolyline.Visibility = Visibility.Collapsed;
            RejoinRouteShadow.Visibility = Visibility.Collapsed;
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

        var navigation = NavBRNavigationEngine.Evaluate(
            telemetry,
            layout,
            _routeTracePoints,
            _busStops);

        OmsiRouteRejoinPath? rejoinPath = null;
        if (navigation.RouteAvailable &&
            !navigation.IsOnRoute &&
            _routeTracePoints.Count >= 2)
        {
            rejoinPath = _hudRouteRejoinPathfinder.TryFind(
                map,
                layout,
                telemetry,
                _routeTracePoints);
        }

        RenderRejoinPath(
            rejoinPath,
            layout,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            localPixelX,
            localPixelY);

        UpdateNavigationSummary(navigation, map);
        UpdateTurnGuidance(navigation, rejoinPath);
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

    private void UpdateNavigationSummary(NavBRNavigationSnapshot navigation, OmsiMapInfo map)
    {
        if (!navigation.RouteAvailable)
        {
            MiniMapStatusText.Text = map.DisplayName;
            return;
        }

        var remaining = FormatNavigationDistance(navigation.DistanceRemainingMeters);
        MiniMapStatusText.Text = $"{map.DisplayName} • {remaining} • {navigation.RouteProgressPercent:0}%";

        if (!string.IsNullOrWhiteSpace(navigation.NextStopName))
        {
            var distance = navigation.DistanceToNextStopMeters is double meters
                ? $" • {FormatNavigationDistance(meters)}"
                : string.Empty;
            NextStopText.Text = $"{NavigationText("Próxima parada", "Next stop", "Próxima parada", "Nächster Halt", "Prochain arrêt")}: {navigation.NextStopName}{distance}";
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

    private void RenderRejoinPath(
        OmsiRouteRejoinPath? rejoinPath,
        OmsiMapLayout layout,
        int bitmapWidth,
        int bitmapHeight,
        double localPixelX,
        double localPixelY)
    {
        if (rejoinPath is null ||
            rejoinPath.Points.Count < 2 ||
            layout.TileSize is not double tileSize ||
            layout.WorldWidth is not double worldWidth ||
            layout.WorldHeight is not double worldHeight)
        {
            RejoinRoutePolyline.Visibility = Visibility.Collapsed;
            RejoinRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        const double canvasWidth = 296d;
        const double canvasHeight = 186d;
        const double sourceViewWidth = 900d;
        var scale = canvasWidth / sourceViewWidth;
        var originX = layout.MinGridX * tileSize;
        var originY = layout.MinGridY * tileSize;

        var points = new PointCollection(rejoinPath.Points.Count);
        foreach (var point in rejoinPath.Points)
        {
            var pixelX = (point.X - originX) * bitmapWidth / worldWidth;
            var pixelY = bitmapHeight -
                         ((point.Y - originY) * bitmapHeight / worldHeight);
            if (!double.IsFinite(pixelX) || !double.IsFinite(pixelY))
            {
                continue;
            }

            points.Add(new Point(
                canvasWidth / 2d + (pixelX - localPixelX) * scale,
                canvasHeight / 2d + (pixelY - localPixelY) * scale));
        }

        if (points.Count < 2)
        {
            RejoinRoutePolyline.Visibility = Visibility.Collapsed;
            RejoinRouteShadow.Visibility = Visibility.Collapsed;
            return;
        }

        RejoinRoutePolyline.Points = points;
        RejoinRouteShadow.Points = points.Clone();
        RejoinRoutePolyline.Visibility = Visibility.Visible;
        RejoinRouteShadow.Visibility = Visibility.Visible;
    }

    private void UpdateTurnGuidance(
        NavBRNavigationSnapshot navigation,
        OmsiRouteRejoinPath? rejoinPath)
    {
        if (!navigation.RouteAvailable)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (navigation.Maneuver == NavBRManeuverKind.RejoinRoute)
        {
            TurnArrowText.Text = "↺";
            TurnInstructionText.Text = NavigationText(
                $"Fora da rota • retorne em {FormatNavigationDistance(rejoinPath?.DistanceMeters ?? navigation.OffRouteDistanceMeters)}",
                $"Off route • rejoin in {FormatNavigationDistance(rejoinPath?.DistanceMeters ?? navigation.OffRouteDistanceMeters)}",
                $"Fuera de ruta • vuelva en {FormatNavigationDistance(rejoinPath?.DistanceMeters ?? navigation.OffRouteDistanceMeters)}",
                $"Route verlassen • zurück in {FormatNavigationDistance(rejoinPath?.DistanceMeters ?? navigation.OffRouteDistanceMeters)}",
                $"Hors itinéraire • retour dans {FormatNavigationDistance(rejoinPath?.DistanceMeters ?? navigation.OffRouteDistanceMeters)}");
            TurnPanel.Visibility = Visibility.Visible;
            return;
        }

        if (navigation.Maneuver == NavBRManeuverKind.None ||
            navigation.DistanceToManeuverMeters is not double distance)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            return;
        }

        TurnArrowText.Text = navigation.Maneuver switch
        {
            NavBRManeuverKind.SharpLeft => "←",
            NavBRManeuverKind.Left => "←",
            NavBRManeuverKind.SlightLeft => "↖",
            NavBRManeuverKind.SharpRight => "→",
            NavBRManeuverKind.Right => "→",
            NavBRManeuverKind.SlightRight => "↗",
            _ => "↑"
        };

        var right = navigation.Maneuver is NavBRManeuverKind.SlightRight or NavBRManeuverKind.Right or NavBRManeuverKind.SharpRight;
        var strong = navigation.Maneuver is NavBRManeuverKind.Left or NavBRManeuverKind.Right or NavBRManeuverKind.SharpLeft or NavBRManeuverKind.SharpRight;
        TurnInstructionText.Text = BuildTurnInstruction(right, strong, Math.Max(10, (int)Math.Round(distance / 10d) * 10));
        TurnPanel.Visibility = Visibility.Visible;
    }

    private static string FormatNavigationDistance(double meters)
    {
        meters = Math.Max(0d, meters);
        return meters >= 1000d
            ? $"{meters / 1000d:0.0} km"
            : $"{Math.Round(meters / 10d) * 10d:0} m";
    }

    private static string NavigationText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

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
}
