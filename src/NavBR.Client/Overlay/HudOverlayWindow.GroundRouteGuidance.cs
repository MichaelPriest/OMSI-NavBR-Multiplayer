using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const double GroundGuidanceMaxDistanceMeters = 150d;
    private const double GroundGuidanceMinSpacingMeters = 9d;
    private const double GroundGuidanceRouteSnapMeters = 90d;
    private Canvas? _groundRouteGuidanceCanvas;
    private readonly List<Polygon> _groundRouteGuidanceArrows = [];
    private string _groundRouteGuidanceStatus = "desativado";

    internal string GroundRouteGuidanceStatus => _groundRouteGuidanceStatus;

    internal void InitializeGroundRouteGuidance()
    {
        if (_groundRouteGuidanceCanvas is not null)
        {
            return;
        }

        _groundRouteGuidanceCanvas = new Canvas
        {
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_groundRouteGuidanceCanvas, 580);
        OverlayRoot.Children.Add(_groundRouteGuidanceCanvas);
    }

    internal void RenderGroundRouteGuidance()
    {
        var canvas = _groundRouteGuidanceCanvas;
        var telemetry = _localTelemetry;
        var layout = _mapLayout;
        var projection = _cameraProjection;
        var map = _activeMap;

        if (canvas is null)
        {
            SetGroundRouteGuidanceStatus("canvas indisponível");
            return;
        }

        if (!_hudSettings.GroundRouteGuidanceEnabled)
        {
            SetGroundRouteGuidanceStatus("desativado");
            HideGroundRouteGuidance();
            return;
        }

        if (telemetry?.IsInGame != true)
        {
            SetGroundRouteGuidanceStatus("aguardando OMSI em jogo");
            HideGroundRouteGuidance();
            return;
        }

        if (map is null ||
            layout?.TileSize is not double tileSize ||
            !double.IsFinite(tileSize) ||
            tileSize <= 0d)
        {
            SetGroundRouteGuidanceStatus("mapa/layout indisponível");
            HideGroundRouteGuidance();
            return;
        }

        if (!TryGetGpsDisplayAnchor(
                telemetry,
                map,
                out var gridX,
                out var gridY,
                out var tileX,
                out var tileY))
        {
            SetGroundRouteGuidanceStatus("posição GPS indisponível");
            HideGroundRouteGuidance();
            return;
        }

        if (!TryGetOmsiClientViewport(out var viewport))
        {
            SetGroundRouteGuidanceStatus("janela OMSI indisponível");
            HideGroundRouteGuidance();
            return;
        }

        // Ground guidance must own its route-loading dependency. Previously it
        // only worked after the minimap happened to populate _routeTracePoints.
        EnsureRouteTrace(
            map,
            layout,
            telemetry.Line,
            telemetry.Route,
            telemetry.DestinationName);
        if (_routeTracePoints.Count < 2)
        {
            var routeMode =
                OmsiRouteTraceReader.LastDiagnostics?.Mode ??
                "sem diagnóstico";
            SetGroundRouteGuidanceStatus(
                $"rota não encontrada • {routeMode}");
            HideGroundRouteGuidance();
            return;
        }

        var currentWorldX = gridX * tileSize + tileX;
        var currentWorldY = gridY * tileSize + tileY;
        var nearestIndex = FindNearestGroundGuidanceRouteIndex(
            currentWorldX,
            currentWorldY,
            tileSize);
        if (nearestIndex < 0)
        {
            SetGroundRouteGuidanceStatus(
                $"rota distante • {_routeTracePoints.Count} pontos");
            HideGroundRouteGuidance();
            return;
        }

        var localX = telemetry.LocalX ?? 0d;
        var localY = telemetry.LocalY ?? 0d;
        var localZ = telemetry.LocalZ ?? 0d;
        var hasNativePose =
            telemetry.LocalX.HasValue &&
            telemetry.LocalY.HasValue &&
            telemetry.LocalZ.HasValue &&
            double.IsFinite(localX) &&
            double.IsFinite(localY) &&
            double.IsFinite(localZ);
        var hasFreshNativeProjection =
            hasNativePose &&
            projection is not null &&
            DateTimeOffset.UtcNow - projection.Value.CapturedAtUtc <=
                TimeSpan.FromSeconds(2d);

        var routeDirection = ResolveGroundGuidanceRouteDirection(
            nearestIndex,
            currentWorldX,
            currentWorldY,
            tileSize,
            telemetry.HeadingDegrees);
        var projectionHeadingDegrees =
            ResolveGroundGuidanceProjectionHeading(
                nearestIndex,
                routeDirection,
                currentWorldX,
                currentWorldY,
                tileSize,
                telemetry.HeadingDegrees);

        canvas.Visibility = Visibility.Visible;
        var used = 0;
        var accumulated = 0d;
        var lastWorldX = currentWorldX;
        var lastWorldY = currentWorldY;
        var lastPlaced = -GroundGuidanceMinSpacingMeters;

        for (var index = nearestIndex;
             index >= 0 &&
             index < _routeTracePoints.Count &&
             accumulated <= GroundGuidanceMaxDistanceMeters;
             index += routeDirection)
        {
            var nextIndex = index + routeDirection;
            if (nextIndex < 0 || nextIndex >= _routeTracePoints.Count)
            {
                break;
            }

            var point = _routeTracePoints[index];
            var next = _routeTracePoints[nextIndex];

            var worldX = point.GridX * tileSize + point.TileX;
            var worldY = point.GridY * tileSize + point.TileY;
            var nextWorldX = next.GridX * tileSize + next.TileX;
            var nextWorldY = next.GridY * tileSize + next.TileY;

            if (!double.IsFinite(worldX) ||
                !double.IsFinite(worldY) ||
                !double.IsFinite(nextWorldX) ||
                !double.IsFinite(nextWorldY))
            {
                continue;
            }

            var segmentFromLast = Math.Sqrt(
                Math.Pow(worldX - lastWorldX, 2d) +
                Math.Pow(worldY - lastWorldY, 2d));
            accumulated += segmentFromLast;
            lastWorldX = worldX;
            lastWorldY = worldY;

            if (accumulated - lastPlaced < GroundGuidanceMinSpacingMeters)
            {
                continue;
            }

            var deltaX = worldX - currentWorldX;
            var deltaY = worldY - currentWorldY;
            var nextDeltaX = nextWorldX - currentWorldX;
            var nextDeltaY = nextWorldY - currentWorldY;

            var projected = false;
            double screenX = 0d;
            double screenY = 0d;
            double nextScreenX = 0d;
            double nextScreenY = 0d;

            if (hasFreshNativeProjection)
            {
                var aheadX = localX + deltaX;
                var aheadZ = localZ + deltaY;
                var directionX = localX + nextDeltaX;
                var directionZ = localZ + nextDeltaY;
                var height = localY + 0.08d;

                projected =
                    TryProjectToViewport(
                        new Vector3(
                            (float)aheadX,
                            (float)height,
                            (float)aheadZ),
                        projection!.Value,
                        viewport,
                        out screenX,
                        out screenY) &&
                    TryProjectToViewport(
                        new Vector3(
                            (float)directionX,
                            (float)height,
                            (float)directionZ),
                        projection.Value,
                        viewport,
                        out nextScreenX,
                        out nextScreenY);
            }

            // Camera matrices are available only for the exact OMSI 2.3.004
            // memory profile, and openOMSI currently does not export them.
            // Keep Forza-style guidance usable by projecting the actual route
            // into a heading-relative road perspective when native projection
            // is unavailable or rejects a point.
            if (!projected)
            {
                projected =
                    TryProjectGroundGuidancePerspective(
                        deltaX,
                        deltaY,
                        projectionHeadingDegrees,
                        viewport,
                        out screenX,
                        out screenY) &&
                    TryProjectGroundGuidancePerspective(
                        nextDeltaX,
                        nextDeltaY,
                        projectionHeadingDegrees,
                        viewport,
                        out nextScreenX,
                        out nextScreenY);
            }

            if (!projected)
            {
                continue;
            }

            var dx = nextScreenX - screenX;
            var dy = nextScreenY - screenY;
            if (Math.Abs(dx) + Math.Abs(dy) < 1d)
            {
                continue;
            }

            var arrow = GetOrCreateGroundGuidanceArrow(used++);
            var angle = Math.Atan2(dy, dx) * 180d / Math.PI + 90d;
            var distanceScale = Math.Clamp(
                1.25d - accumulated / GroundGuidanceMaxDistanceMeters * 0.55d,
                0.70d,
                1.25d);
            var transforms = new TransformGroup();
            transforms.Children.Add(new ScaleTransform(distanceScale, distanceScale));
            transforms.Children.Add(new RotateTransform(angle));
            arrow.RenderTransform = transforms;
            arrow.Opacity = Math.Clamp(
                1d - accumulated /
                    (GroundGuidanceMaxDistanceMeters * 1.45d),
                0.32d,
                0.96d);
            arrow.Visibility = Visibility.Visible;
            Canvas.SetLeft(arrow, screenX - arrow.Width / 2d);
            Canvas.SetTop(arrow, screenY - arrow.Height / 2d);
            lastPlaced = accumulated;

            if (used >= 18)
            {
                break;
            }
        }

        for (var index = used;
             index < _groundRouteGuidanceArrows.Count;
             index++)
        {
            _groundRouteGuidanceArrows[index].Visibility =
                Visibility.Collapsed;
        }

        if (used == 0)
        {
            SetGroundRouteGuidanceStatus(
                $"sem pontos à frente • rota {_routeTracePoints.Count} pts • sentido {(routeDirection > 0 ? "+" : "-")}");
            canvas.Visibility = Visibility.Collapsed;
        }
        else
        {
            SetGroundRouteGuidanceStatus(
                $"{used} setas visíveis • {(hasFreshNativeProjection ? "3D nativo/fallback" : "perspectiva fallback")} • rota {_routeTracePoints.Count} pts");
        }
    }

    private void SetGroundRouteGuidanceStatus(string status)
    {
        if (string.Equals(
                _groundRouteGuidanceStatus,
                status,
                StringComparison.Ordinal))
        {
            return;
        }

        _groundRouteGuidanceStatus = status;
        UpdateInGameGroundGuidanceStatus(status);
    }

    private int ResolveGroundGuidanceRouteDirection(
        int nearestIndex,
        double currentWorldX,
        double currentWorldY,
        double tileSize,
        double headingDegrees)
    {
        if (_routeTracePoints.Count < 2)
        {
            return 1;
        }

        if (nearestIndex <= 0)
        {
            return 1;
        }

        if (nearestIndex >= _routeTracePoints.Count - 1)
        {
            return -1;
        }

        var radians = headingDegrees * Math.PI / 180d;
        var forwardX = Math.Sin(radians);
        var forwardY = Math.Cos(radians);

        double Score(int index)
        {
            var point = _routeTracePoints[index];
            var x = point.GridX * tileSize + point.TileX;
            var y = point.GridY * tileSize + point.TileY;
            var dx = x - currentWorldX;
            var dy = y - currentWorldY;
            return dx * forwardX + dy * forwardY;
        }

        var forwardScore = Score(nearestIndex + 1);
        var reverseScore = Score(nearestIndex - 1);

        if (!double.IsFinite(forwardScore) && !double.IsFinite(reverseScore))
        {
            return 1;
        }

        if (!double.IsFinite(forwardScore))
        {
            return -1;
        }

        if (!double.IsFinite(reverseScore))
        {
            return 1;
        }

        return reverseScore > forwardScore ? -1 : 1;
    }

    private double ResolveGroundGuidanceProjectionHeading(
        int nearestIndex,
        int routeDirection,
        double currentWorldX,
        double currentWorldY,
        double tileSize,
        double headingDegrees)
    {
        if (!double.IsFinite(headingDegrees))
        {
            return 0d;
        }

        var nextIndex = nearestIndex + routeDirection;
        if (nextIndex < 0 || nextIndex >= _routeTracePoints.Count)
        {
            return headingDegrees;
        }

        var next = _routeTracePoints[nextIndex];
        var dx =
            next.GridX * tileSize + next.TileX -
            currentWorldX;
        var dy =
            next.GridY * tileSize + next.TileY -
            currentWorldY;
        if (!double.IsFinite(dx) || !double.IsFinite(dy))
        {
            return headingDegrees;
        }

        var radians = headingDegrees * Math.PI / 180d;
        var forward =
            dx * Math.Sin(radians) +
            dy * Math.Cos(radians);

        return forward < -0.5d
            ? (headingDegrees + 180d) % 360d
            : headingDegrees;
    }

    private static bool TryProjectGroundGuidancePerspective(
        double deltaWorldX,
        double deltaWorldY,
        double headingDegrees,
        ClientViewport viewport,
        out double screenX,
        out double screenY)
    {
        screenX = 0d;
        screenY = 0d;

        if (!double.IsFinite(deltaWorldX) ||
            !double.IsFinite(deltaWorldY) ||
            !double.IsFinite(headingDegrees))
        {
            return false;
        }

        var radians = headingDegrees * Math.PI / 180d;
        var forward =
            deltaWorldX * Math.Sin(radians) +
            deltaWorldY * Math.Cos(radians);
        var right =
            deltaWorldX * Math.Cos(radians) -
            deltaWorldY * Math.Sin(radians);

        // Do not paint the route behind the driver's viewpoint.
        if (forward < 2d ||
            forward > GroundGuidanceMaxDistanceMeters * 1.20d)
        {
            return false;
        }

        var depth = Math.Clamp(
            forward / GroundGuidanceMaxDistanceMeters,
            0d,
            1d);
        var horizonY = viewport.Top + viewport.Height * 0.48d;
        var nearY = viewport.Top + viewport.Height * 0.91d;
        screenY =
            nearY +
            (horizonY - nearY) * Math.Sqrt(depth);

        // Perspective widens close to the bus and narrows toward the horizon.
        var focal = viewport.Width * 0.72d;
        screenX =
            viewport.Left +
            viewport.Width * 0.5d +
            right / Math.Max(10d, forward) * focal;

        var sideMargin = viewport.Width * 0.04d;
        return double.IsFinite(screenX) &&
               double.IsFinite(screenY) &&
               screenX >= viewport.Left - sideMargin &&
               screenX <= viewport.Right + sideMargin &&
               screenY >= viewport.Top &&
               screenY <= viewport.Bottom;
    }

    private int FindNearestGroundGuidanceRouteIndex(
        double worldX,
        double worldY,
        double tileSize)
    {
        var bestIndex = -1;
        var bestDistanceSquared =
            GroundGuidanceRouteSnapMeters *
            GroundGuidanceRouteSnapMeters;

        for (var index = 0; index < _routeTracePoints.Count; index++)
        {
            var point = _routeTracePoints[index];
            var x = point.GridX * tileSize + point.TileX;
            var y = point.GridY * tileSize + point.TileY;
            var dx = x - worldX;
            var dy = y - worldY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestIndex = index;
        }

        return bestIndex;
    }

    private Polygon GetOrCreateGroundGuidanceArrow(int index)
    {
        while (_groundRouteGuidanceArrows.Count <= index)
        {
            var arrow = new Polygon
            {
                Width = 28d,
                Height = 38d,
                Stretch = Stretch.Fill,
                Points = new PointCollection
                {
                    new(14d, 0d),
                    new(28d, 18d),
                    new(20d, 18d),
                    new(20d, 38d),
                    new(8d, 38d),
                    new(8d, 18d),
                    new(0d, 18d)
                },
                Fill = new SolidColorBrush(Color.FromArgb(220, 255, 145, 35)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 255, 240, 208)),
                StrokeThickness = 1.4d,
                RenderTransformOrigin = new Point(0.5d, 0.5d),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            _groundRouteGuidanceCanvas!.Children.Add(arrow);
            _groundRouteGuidanceArrows.Add(arrow);
        }

        return _groundRouteGuidanceArrows[index];
    }

    private void HideGroundRouteGuidance()
    {
        if (_groundRouteGuidanceCanvas is not null)
        {
            _groundRouteGuidanceCanvas.Visibility = Visibility.Collapsed;
        }

        foreach (var arrow in _groundRouteGuidanceArrows)
        {
            arrow.Visibility = Visibility.Collapsed;
        }
    }
}
