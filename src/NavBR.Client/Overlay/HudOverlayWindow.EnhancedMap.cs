using System.Windows;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private string? _routeTraceCacheKey;
    private DateTimeOffset _routeTraceLastAttemptUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<OmsiRouteTracePoint> _routeTracePoints = Array.Empty<OmsiRouteTracePoint>();
    private bool _routeTraceHasDetailedGeometry;
    private OmsiPhysicalRoadAnchorResolver? _gpsRoadAnchorResolver;
    private string? _gpsRoadAnchorInstallRoot;
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

        var hasRoadmapSurface =
            map is not null &&
            bitmap is not null &&
            layout is not null;
        if (!_immersiveOperationActive)
        {
            var hudMode =
                _hudSettings.HudSelectionMode?.Trim().ToLowerInvariant() ?? "all";
            var singleWidget =
                _hudSettings.HudSingleWidget?.Trim().ToLowerInvariant() ?? "dashboard";
            var mapSelected =
                hudMode == "all" ||
                hudMode == "single" && singleWidget == "minimap" ||
                hudMode == "selected" && _hudSettings.DashboardShowMinimap;

            MiniMapHudPanel.Visibility = hasRoadmapSurface && mapSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (telemetry is not null)
        {
            LocalMarkerRotation.Angle = 0d;
            var smoothedHeading =
                GetSmoothedHudHeading(telemetry.HeadingDegrees);
            MiniMapHeadingRotation.Angle =
                NormalizeAngle(-smoothedHeading);
        }
        else
        {
            _renderedHudHeadingDegrees = double.NaN;
            MiniMapHeadingRotation.Angle = 0d;
            LocalMarkerRotation.Angle = 0d;
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
            !TryGetGpsDisplayAnchor(
                telemetry,
                map,
                out var gridX,
                out var gridY,
                out var tileX,
                out var tileY) ||
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

        // Use the exact same projected road anchor for presentation,
        // navigation progress and rejoin routing. Previously the map was
        // centred on the snapped lane while NavBRNavigationEngine and the
        // rejoin pathfinder still consumed the raw telemetry grid position,
        // which could make the player icon appear on a road while the route
        // logic considered the bus somewhere else.
        var navigationTelemetry = telemetry with
        {
            GridX = gridX,
            GridY = gridY,
            TileX = tileX,
            TileY = tileY
        };

        var navigation = NavBRNavigationEngine.Evaluate(
            navigationTelemetry,
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
                navigationTelemetry,
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

    private bool TryGetGpsDisplayAnchor(
        VehicleTelemetry telemetry,
        OmsiMapInfo map,
        out int gridX,
        out int gridY,
        out double tileX,
        out double tileY)
    {
        gridX = 0;
        gridY = 0;
        tileX = 0d;
        tileY = 0d;

        // Prefer the physical OMSI Kachel pose when the bridge provides it.
        // GridX/TileX can be a compatibility/navigation projection; using it
        // as the display fallback is what made the marker drift to a parallel
        // or neighbouring road whenever a lane snap was temporarily unavailable.
        var hasGridTelemetry = false;
        if (telemetry.PhysicalGridX is int physicalGridX &&
            telemetry.PhysicalGridY is int physicalGridY &&
            telemetry.LocalX is double physicalLocalX &&
            telemetry.LocalZ is double physicalLocalZ)
        {
            hasGridTelemetry = true;
            gridX = physicalGridX;
            gridY = physicalGridY;
            tileX = physicalLocalX;
            tileY = physicalLocalZ;
        }
        else if (telemetry.GridX is int rawGridX &&
                 telemetry.GridY is int rawGridY &&
                 telemetry.TileX is double rawTileX &&
                 telemetry.TileY is double rawTileY)
        {
            hasGridTelemetry = true;
            gridX = rawGridX;
            gridY = rawGridY;
            tileX = rawTileX;
            tileY = rawTileY;
        }

        var installRoot = ResolveOmsiInstallRoot(map.DirectoryPath);
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            return hasGridTelemetry;
        }

        if (_gpsRoadAnchorResolver is null ||
            !string.Equals(
                _gpsRoadAnchorInstallRoot,
                installRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            _gpsRoadAnchorInstallRoot = installRoot;
            var capturedRoot = installRoot;
            _gpsRoadAnchorResolver =
                new OmsiPhysicalRoadAnchorResolver(() => capturedRoot);
        }

        var anchorTelemetry = telemetry;

        // openOMSI exposes the live world pose even when a NavBR grid/tile pair
        // is not available yet. Convert that real world position back into the
        // OMSI Kachel before trying to snap to the lane network. This keeps the
        // local marker functional instead of failing early and disappearing.
        if (!hasGridTelemetry)
        {
            if (!_gpsRoadAnchorResolver.TryResolveOpenOmsiWorldAnchor(
                    telemetry,
                    out var worldAnchor))
            {
                return false;
            }

            gridX = worldAnchor.GridX;
            gridY = worldAnchor.GridY;
            tileX = worldAnchor.LocalX;
            tileY = worldAnchor.LocalZ;

            anchorTelemetry = telemetry with
            {
                GridX = gridX,
                GridY = gridY,
                TileX = tileX,
                TileY = tileY,
                PhysicalGridX = gridX,
                PhysicalGridY = gridY,
                LocalX = worldAnchor.LocalX,
                LocalY = worldAnchor.LocalY,
                LocalZ = worldAnchor.LocalZ
            };
        }

        if (_gpsRoadAnchorResolver.TryResolveRoadAnchor(
                anchorTelemetry,
                out var roadAnchor) &&
            roadAnchor.DistanceMeters <= 18d)
        {
            gridX = roadAnchor.GridX;
            gridY = roadAnchor.GridY;
            tileX = roadAnchor.LocalX;
            tileY = roadAnchor.LocalZ;
        }

        return true;
    }

    private static string? ResolveOmsiInstallRoot(string? mapDirectory)
    {
        if (string.IsNullOrWhiteSpace(mapDirectory))
        {
            return null;
        }

        try
        {
            var map = new System.IO.DirectoryInfo(mapDirectory);
            var maps = map.Parent;
            if (maps is not null &&
                string.Equals(
                    maps.Name,
                    "maps",
                    StringComparison.OrdinalIgnoreCase))
            {
                return maps.Parent?.FullName;
            }
        }
        catch
        {
        }

        return null;
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

        var hasTripInfo =
            !string.IsNullOrWhiteSpace(telemetry?.Line) ||
            !string.IsNullOrWhiteSpace(telemetry?.Route) ||
            !string.IsNullOrWhiteSpace(telemetry?.DestinationName) ||
            !string.IsNullOrWhiteSpace(telemetry?.NextStopName);
        if (!_immersiveOperationActive)
        {
            TripInfoPanel.Visibility = hasTripInfo
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

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

        if (!_immersiveOperationActive)
        {
            TripInfoPanel.Visibility = Visibility.Visible;
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
        var sameKey = string.Equals(
            cacheKey,
            _routeTraceCacheKey,
            StringComparison.OrdinalIgnoreCase);
        if (sameKey && _routeTracePoints.Count >= 2)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (sameKey &&
            now - _routeTraceLastAttemptUtc < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _routeTraceCacheKey = cacheKey;
        _routeTraceLastAttemptUtc = now;
        _routeTracePoints = OmsiRouteTraceReader.TryRead(
            map,
            layout,
            lookupTarget,
            lineName,
            destinationName);
        _routeTraceHasDetailedGeometry =
            string.Equals(
                OmsiRouteTraceReader.LastDiagnostics?.Mode,
                "detailed",
                StringComparison.OrdinalIgnoreCase);
    }

    private void RenderRouteTrace(
        OmsiMapLayout layout,
        int bitmapWidth,
        int bitmapHeight,
        double localPixelX,
        double localPixelY)
    {
        if (!_hudSettings.MapShowRoute ||
            _routeTracePoints.Count < 2)
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
        if (!_hudSettings.MapShowRejoin ||
            rejoinPath is null ||
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
            MiniMapTurnOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        if (navigation.Maneuver == NavBRManeuverKind.RejoinRoute)
        {
            var rejoinDistance =
                rejoinPath?.DistanceMeters ??
                navigation.OffRouteDistanceMeters;
            TurnArrowText.Text = "↺";
            TurnInstructionText.Text = NavigationText(
                $"Fora da rota • retorne em {FormatNavigationDistance(rejoinDistance)}",
                $"Off route • rejoin in {FormatNavigationDistance(rejoinDistance)}",
                $"Fuera de ruta • vuelva en {FormatNavigationDistance(rejoinDistance)}",
                $"Route verlassen • zurück in {FormatNavigationDistance(rejoinDistance)}",
                $"Hors itinéraire • retour dans {FormatNavigationDistance(rejoinDistance)}");
            MiniMapTurnArrowText.Text = "↺";
            MiniMapTurnInstructionText.Text =
                FormatNavigationDistance(rejoinDistance);
            TurnPanel.Visibility = Visibility.Visible;
            MiniMapTurnOverlay.Visibility = Visibility.Visible;
            return;
        }

        if (navigation.Maneuver == NavBRManeuverKind.None ||
            navigation.DistanceToManeuverMeters is not double distance)
        {
            TurnPanel.Visibility = Visibility.Collapsed;
            MiniMapTurnOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var arrow = navigation.Maneuver switch
        {
            NavBRManeuverKind.SharpLeft => "←",
            NavBRManeuverKind.Left => "←",
            NavBRManeuverKind.SlightLeft => "↖",
            NavBRManeuverKind.SharpRight => "→",
            NavBRManeuverKind.Right => "→",
            NavBRManeuverKind.SlightRight => "↗",
            _ => "↑"
        };
        TurnArrowText.Text = arrow;
        MiniMapTurnArrowText.Text = arrow;

        var right = navigation.Maneuver is NavBRManeuverKind.SlightRight or NavBRManeuverKind.Right or NavBRManeuverKind.SharpRight;
        var strong = navigation.Maneuver is NavBRManeuverKind.Left or NavBRManeuverKind.Right or NavBRManeuverKind.SharpLeft or NavBRManeuverKind.SharpRight;
        TurnInstructionText.Text = BuildTurnInstruction(right, strong, Math.Max(10, (int)Math.Round(distance / 10d) * 10));
        MiniMapTurnInstructionText.Text =
            FormatNavigationDistance(distance);
        TurnPanel.Visibility = Visibility.Visible;
        MiniMapTurnOverlay.Visibility = Visibility.Visible;
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
