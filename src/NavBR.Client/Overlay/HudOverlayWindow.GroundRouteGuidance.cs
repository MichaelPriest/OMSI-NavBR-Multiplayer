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

        // Forza-style guidance must stay locked to real OMSI world geometry.
        // Never fall back to a screen-space fake perspective: if the native
        // camera projection is unavailable/stale, hide the arrows instead of
        // drawing floating guidance over the HUD.
        if (!hasFreshNativeProjection)
        {
            SetGroundRouteGuidanceStatus("projeção 3D nativa indisponível");
            HideGroundRouteGuidance();
            return;
        }

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

            var aheadX = localX + deltaX;
            var aheadZ = localZ + deltaY;
            var directionX = localX + nextDeltaX;
            var directionZ = localZ + nextDeltaY;

            var arrowGroundY = localY;
            if (OmsiSplineGroundHeightResolver.TryResolve(
                    map,
                    telemetry,
                    aheadX,
                    aheadZ,
                    localY,
                    out var resolvedArrowGroundY))
            {
                arrowGroundY = resolvedArrowGroundY;
            }

            var directionGroundY = arrowGroundY;
            if (OmsiSplineGroundHeightResolver.TryResolve(
                    map,
                    telemetry,
                    directionX,
                    directionZ,
                    arrowGroundY,
                    out var resolvedDirectionGroundY))
            {
                directionGroundY = resolvedDirectionGroundY;
            }

            // Keep the arrow a few centimetres above the physical spline to
            // avoid z-fighting while remaining visually attached to the road.
            const double GroundLiftMeters = 0.045d;
            double screenX = 0d;
            double screenY = 0d;
            double nextScreenX = 0d;
            double nextScreenY = 0d;
            var projected =
                TryProjectToViewport(
                    new Vector3(
                        (float)aheadX,
                        (float)(arrowGroundY + GroundLiftMeters),
                        (float)aheadZ),
                    projection!.Value,
                    viewport,
                    out var screenX,
                    out var screenY) &&
                TryProjectToViewport(
                    new Vector3(
                        (float)directionX,
                        (float)(directionGroundY + GroundLiftMeters),
                        (float)directionZ),
                    projection.Value,
                    viewport,
                    out var nextScreenX,
                    out var nextScreenY);

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
                $"{used} setas na via • spline + projeção 3D nativa • rota {_routeTracePoints.Count} pts");
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
