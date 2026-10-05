using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const double GroundGuidanceMaxDistanceMeters = 150d;
    private const double GroundGuidanceMinSpacingMeters = 9d;
    private Canvas? _groundRouteGuidanceCanvas;
    private readonly List<Polygon> _groundRouteGuidanceArrows = [];

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

        if (canvas is null ||
            !_hudSettings.GroundRouteGuidanceEnabled ||
            telemetry?.IsInGame != true ||
            map is null ||
            layout?.TileSize is not double tileSize ||
            !double.IsFinite(tileSize) ||
            tileSize <= 0d ||
            projection is null ||
            DateTimeOffset.UtcNow - projection.Value.CapturedAtUtc > TimeSpan.FromSeconds(2d) ||
            telemetry.LocalX is not double localX ||
            telemetry.LocalY is not double localY ||
            telemetry.LocalZ is not double localZ ||
            !double.IsFinite(localX) ||
            !double.IsFinite(localY) ||
            !double.IsFinite(localZ) ||
            _routeTracePoints.Count < 2 ||
            !TryGetGpsDisplayAnchor(
                telemetry,
                map,
                out var gridX,
                out var gridY,
                out var tileX,
                out var tileY) ||
            !TryGetOmsiClientViewport(out var viewport))
        {
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
             index < _routeTracePoints.Count - 1 && accumulated <= GroundGuidanceMaxDistanceMeters;
             index++)
        {
            var point = _routeTracePoints[index];
            var next = _routeTracePoints[index + 1];

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

            var aheadX = localX + (worldX - currentWorldX);
            var aheadZ = localZ + (worldY - currentWorldY);
            var directionX = localX + (nextWorldX - currentWorldX);
            var directionZ = localZ + (nextWorldY - currentWorldY);

            // Route geometry is exact in the horizontal OMSI lane plane. The
            // timetable trace does not carry per-point elevation yet, so nearby
            // arrows use the current vehicle ground height instead of inventing
            // terrain data. This keeps them tied to the real lane geometry while
            // remaining read-only and safe on OMSI 2.
            var height = localY + 0.08d;
            if (!TryProjectToViewport(
                    new Vector3((float)aheadX, (float)height, (float)aheadZ),
                    projection.Value,
                    viewport,
                    out var screenX,
                    out var screenY) ||
                !TryProjectToViewport(
                    new Vector3((float)directionX, (float)height, (float)directionZ),
                    projection.Value,
                    viewport,
                    out var nextScreenX,
                    out var nextScreenY))
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
            arrow.RenderTransform = new RotateTransform(angle);
            arrow.Opacity = Math.Clamp(
                1d - accumulated / (GroundGuidanceMaxDistanceMeters * 1.45d),
                0.28d,
                0.92d);
            arrow.Visibility = Visibility.Visible;
            Canvas.SetLeft(arrow, screenX - arrow.Width / 2d);
            Canvas.SetTop(arrow, screenY - arrow.Height / 2d);
            lastPlaced = accumulated;

            if (used >= 18)
            {
                break;
            }
        }

        for (var index = used; index < _groundRouteGuidanceArrows.Count; index++)
        {
            _groundRouteGuidanceArrows[index].Visibility = Visibility.Collapsed;
        }

        if (used == 0)
        {
            canvas.Visibility = Visibility.Collapsed;
        }
    }

    private int FindNearestGroundGuidanceRouteIndex(
        double worldX,
        double worldY,
        double tileSize)
    {
        var bestIndex = -1;
        var bestDistanceSquared = 45d * 45d;

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
