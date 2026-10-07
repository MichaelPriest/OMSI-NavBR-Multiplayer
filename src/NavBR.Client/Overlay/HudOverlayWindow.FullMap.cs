using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private Border? _fullMapOverlay;
    private Canvas? _fullMapCanvas;
    private TextBlock? _fullMapTitle;
    private TextBlock? _fullMapZoomText;
    private bool _fullMapOpen;
    private bool _fullMapFollowLocal = true;
    private bool _fullMapDragging;
    private Point _fullMapDragStart;
    private double _fullMapPanStartX;
    private double _fullMapPanStartY;
    private double _fullMapPanX;
    private double _fullMapPanY;

    internal void InitializeFullMapOverlay()
    {
        if (_fullMapOverlay is not null)
        {
            return;
        }

        var header = new Grid
        {
            Margin = new Thickness(12d, 10d, 12d, 8d)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel();
        _fullMapTitle = new TextBlock
        {
            Text = "MAPA COMPLETO • NavBR",
            Foreground = Brushes.White,
            FontSize = 17d,
            FontWeight = FontWeights.Bold
        };
        _fullMapZoomText = new TextBlock
        {
            Text = "100%",
            Foreground = new SolidColorBrush(Color.FromRgb(156, 180, 196)),
            FontSize = 10d,
            Margin = new Thickness(0d, 3d, 0d, 0d)
        };
        heading.Children.Add(_fullMapTitle);
        heading.Children.Add(_fullMapZoomText);
        Grid.SetColumn(heading, 0);
        header.Children.Add(heading);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        buttons.Children.Add(BuildInGameButton(
            "SEGUIR",
            new SolidColorBrush(Color.FromRgb(22, 92, 112)),
            () =>
            {
                _fullMapFollowLocal = true;
                _fullMapPanX = 0d;
                _fullMapPanY = 0d;
                RenderFullMapOverlay();
            }));
        buttons.Children.Add(BuildInGameButton(
            "VISÃO GERAL",
            new SolidColorBrush(Color.FromRgb(38, 66, 86)),
            () =>
            {
                _fullMapFollowLocal = false;
                _fullMapPanX = 0d;
                _fullMapPanY = 0d;
                var settings = MultiplayerSettingsStore.Load();
                if (Math.Abs(settings.InGameFullMapZoom - 1d) > 0.001d)
                {
                    var updated = settings with { InGameFullMapZoom = 1d };
                    MultiplayerSettingsStore.Save(updated);
                    _hudSettings = updated;
                }
                RenderFullMapOverlay();
            }));
        buttons.Children.Add(BuildInGameButton(
            "−",
            new SolidColorBrush(Color.FromRgb(38, 66, 86)),
            () => AdjustFullMapZoom(-0.20d)));
        buttons.Children.Add(BuildInGameButton(
            "+",
            new SolidColorBrush(Color.FromRgb(38, 66, 86)),
            () => AdjustFullMapZoom(0.20d)));
        buttons.Children.Add(BuildInGameButton(
            "×",
            new SolidColorBrush(Color.FromRgb(72, 55, 61)),
            () => CloseFullMapOverlay()));
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);

        _fullMapCanvas = new Canvas
        {
            Background = new SolidColorBrush(Color.FromArgb(210, 10, 10, 10)),
            ClipToBounds = true,
            Focusable = true
        };
        _fullMapCanvas.PreviewMouseWheel += (_, e) =>
        {
            AdjustFullMapZoom(e.Delta > 0 ? 0.15d : -0.15d);
            e.Handled = true;
        };
        _fullMapCanvas.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _fullMapDragging = true;
            _fullMapFollowLocal = false;
            _fullMapDragStart = e.GetPosition(_fullMapCanvas);
            _fullMapPanStartX = _fullMapPanX;
            _fullMapPanStartY = _fullMapPanY;
            _fullMapCanvas.CaptureMouse();
            e.Handled = true;
        };
        _fullMapCanvas.PreviewMouseMove += (_, e) =>
        {
            if (!_fullMapDragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var position = e.GetPosition(_fullMapCanvas);
            _fullMapPanX =
                _fullMapPanStartX + position.X - _fullMapDragStart.X;
            _fullMapPanY =
                _fullMapPanStartY + position.Y - _fullMapDragStart.Y;
            RenderFullMapOverlay();
            e.Handled = true;
        };
        _fullMapCanvas.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!_fullMapDragging)
            {
                return;
            }

            _fullMapDragging = false;
            _fullMapCanvas.ReleaseMouseCapture();
            e.Handled = true;
        };
        _fullMapCanvas.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseFullMapOverlay();
                e.Handled = true;
            }
        };

        var mapFrame = new Border
        {
            Margin = new Thickness(12d, 0d, 12d, 12d),
            Background = new SolidColorBrush(Color.FromArgb(205, 10, 10, 10)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 178, 178, 178)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            ClipToBounds = true,
            Child = _fullMapCanvas
        };

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(mapFrame, 1);
        body.Children.Add(header);
        body.Children.Add(mapFrame);

        _fullMapOverlay = new Border
        {
            Margin = new Thickness(30d),
            Padding = new Thickness(4d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromArgb(225, 8, 8, 8)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(175, 178, 178, 178)),
            BorderThickness = new Thickness(1.5d),
            CornerRadius = new CornerRadius(17d),
            Child = body,
            Visibility = Visibility.Collapsed,
            Focusable = true
        };
        _fullMapOverlay.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseFullMapOverlay();
                e.Handled = true;
            }
        };

        Panel.SetZIndex(_fullMapOverlay, 1240);
        OverlayRoot.Children.Add(_fullMapOverlay);
    }

    internal void OpenFullMapOverlay()
    {
        InitializeFullMapOverlay();
        if (_fullMapOverlay is null || !IsOmsiForeground())
        {
            return;
        }

        if (_inGamePanelOpen)
        {
            CloseInGamePanel(restoreFocus: false);
        }

        if (_chatInteractive)
        {
            CloseChatInput();
        }

        _fullMapOpen = true;
        _fullMapOverlay.Visibility = Visibility.Visible;
        SetInteractive(true);
        RenderFullMapOverlay();
        _ = Dispatcher.BeginInvoke(() =>
        {
            RenderFullMapOverlay();
            _fullMapCanvas?.Focus();
        });
    }

    internal void CloseFullMapOverlay(bool restoreFocus = true)
    {
        if (!_fullMapOpen)
        {
            return;
        }

        _fullMapOpen = false;
        if (_fullMapOverlay is not null)
        {
            _fullMapOverlay.Visibility = Visibility.Collapsed;
        }

        if (!_inGamePanelOpen &&
            !_chatInteractive &&
            !_hudLayoutEditMode)
        {
            SetInteractive(false);
        }

        if (restoreFocus)
        {
            _ = Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                RestoreOmsiFocus);
        }
    }

    internal void AdjustFullMapZoom(double delta)
    {
        var settings = MultiplayerSettingsStore.Load();
        var zoom = Math.Clamp(settings.InGameFullMapZoom + delta, 0.75d, 4d);
        if (Math.Abs(zoom - settings.InGameFullMapZoom) < 0.001d)
        {
            return;
        }

        var updated = settings with
        {
            InGameFullMapEnabled = true,
            InGameFullMapZoom = zoom
        };
        MultiplayerSettingsStore.Save(updated);
        _hudSettings = updated;
        RenderFullMapOverlay();
    }

    internal void RenderFullMapOverlay()
    {
        if (!_fullMapOpen ||
            _fullMapOverlay is null ||
            _fullMapCanvas is null)
        {
            return;
        }

        var bitmap = _mapBitmap;
        var layout = _mapLayout;
        var map = _activeMap;
        var telemetry = _localTelemetry;
        if (bitmap is null ||
            layout is null ||
            map is null ||
            telemetry?.IsInGame != true)
        {
            _fullMapCanvas.Children.Clear();
            if (_fullMapTitle is not null)
            {
                _fullMapTitle.Text = "MAPA COMPLETO • aguardando mapa";
            }
            return;
        }

        var width = _fullMapCanvas.ActualWidth;
        var height = _fullMapCanvas.ActualHeight;
        if (width < 100d || height < 100d)
        {
            return;
        }

        var settings = _hudSettings;
        var zoom = Math.Clamp(settings.InGameFullMapZoom, 0.75d, 4d);
        if (_fullMapZoomText is not null)
        {
            _fullMapZoomText.Text = $"{zoom * 100d:F0}% • roda do mouse = zoom";
        }
        if (_fullMapTitle is not null)
        {
            _fullMapTitle.Text = $"MAPA COMPLETO • {map.DisplayName}";
        }

        var fit = Math.Min(
            width / Math.Max(1d, bitmap.PixelWidth),
            height / Math.Max(1d, bitmap.PixelHeight));
        var scale = Math.Max(0.0001d, fit * zoom);

        var focusPixelX = bitmap.PixelWidth / 2d;
        var focusPixelY = bitmap.PixelHeight / 2d;
        var hasLocalPixel =
            TryGetGpsDisplayAnchor(
                telemetry,
                map,
                out var localGridX,
                out var localGridY,
                out var localTileX,
                out var localTileY) &&
            RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                localGridX,
                localGridY,
                localTileX,
                localTileY,
                out focusPixelX,
                out focusPixelY);

        var imageWidth = bitmap.PixelWidth * scale;
        var imageHeight = bitmap.PixelHeight * scale;
        double imageLeft;
        double imageTop;

        if (_fullMapFollowLocal &&
            zoom > 1.001d &&
            hasLocalPixel)
        {
            imageLeft = width / 2d - focusPixelX * scale;
            imageTop = height / 2d - focusPixelY * scale;
        }
        else
        {
            imageLeft = (width - imageWidth) / 2d + _fullMapPanX;
            imageTop = (height - imageHeight) / 2d + _fullMapPanY;
        }

        imageLeft = ClampMapOffset(imageLeft, width, imageWidth);
        imageTop = ClampMapOffset(imageTop, height, imageHeight);

        _fullMapCanvas.Children.Clear();

        var image = new Image
        {
            Source = bitmap,
            Width = imageWidth,
            Height = imageHeight,
            Stretch = Stretch.Fill,
            Opacity = Math.Clamp(settings.HudMapOpacity, 0.10d, 1d),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(image, imageLeft);
        Canvas.SetTop(image, imageTop);
        _fullMapCanvas.Children.Add(image);

        if (settings.MapShowCongestion &&
            hasLocalPixel &&
            layout.TileSize is double congestionTileSize &&
            layout.WorldWidth is double congestionWorldWidth &&
            layout.WorldHeight is double congestionWorldHeight &&
            double.IsFinite(telemetry.X) &&
            double.IsFinite(telemetry.Z))
        {
            RenderFullMapCongestion(
                layout,
                bitmap,
                scale,
                imageLeft,
                imageTop,
                telemetry,
                localGridX,
                localGridY,
                localTileX,
                localTileY,
                congestionTileSize,
                congestionWorldWidth,
                congestionWorldHeight);
        }

        if (settings.MapShowRoute)
        {
            AddFullMapPolyline(
                _routeTracePoints,
                layout,
                bitmap,
                scale,
                imageLeft,
                imageTop,
                new SolidColorBrush(Color.FromRgb(214, 48, 40)),
                4.5d);
        }

        OmsiRouteRejoinPath? rejoinPath = null;
        if (settings.MapShowRejoin &&
            _routeTracePoints.Count >= 2 &&
            hasLocalPixel)
        {
            var navigationTelemetry = telemetry with
            {
                GridX = localGridX,
                GridY = localGridY,
                TileX = localTileX,
                TileY = localTileY
            };
            var navigation = NavBRNavigationEngine.Evaluate(
                navigationTelemetry,
                layout,
                _routeTracePoints,
                _busStops);
            if (!navigation.IsOnRoute && navigation.RouteAvailable)
            {
                rejoinPath = _hudRouteRejoinPathfinder.TryFind(
                    map,
                    layout,
                    navigationTelemetry,
                    _routeTracePoints);
            }
        }

        if (settings.MapShowRejoin && rejoinPath is not null)
        {
            var points = rejoinPath.Points
                .Select(point => new OmsiRouteTracePoint(
                    (int)Math.Floor(point.X / layout.TileSize!.Value),
                    (int)Math.Floor(point.Y / layout.TileSize!.Value),
                    point.X - Math.Floor(point.X / layout.TileSize.Value) * layout.TileSize.Value,
                    point.Y - Math.Floor(point.Y / layout.TileSize.Value) * layout.TileSize.Value))
                .ToArray();
            AddFullMapPolyline(
                points,
                layout,
                bitmap,
                scale,
                imageLeft,
                imageTop,
                new SolidColorBrush(Color.FromRgb(90, 160, 240)),
                3.2d);
        }

        if (settings.MapShowStops)
        {
            foreach (var stop in _busStops.Take(400))
            {
                if (!RoadmapTransform.TryToPixel(
                        layout,
                        bitmap.PixelWidth,
                        bitmap.PixelHeight,
                        stop.GridX,
                        stop.GridY,
                        stop.TileX,
                        stop.TileY,
                        out var x,
                        out var y))
                {
                    continue;
                }

                AddFullMapDot(
                    imageLeft + x * scale,
                    imageTop + y * scale,
                    5d,
                    new SolidColorBrush(Color.FromRgb(245, 229, 161)),
                    stop.Name);
            }
        }

        if (hasLocalPixel)
        {
            AddFullMapArrow(
                imageLeft + focusPixelX * scale,
                imageTop + focusPixelY * scale,
                telemetry.HeadingDegrees,
                18d,
                new SolidColorBrush(Color.FromRgb(235, 235, 235)),
                "Você");
        }

        if (settings.MapShowPlayers)
        {
            foreach (var frame in _smoothedHudFrames.Values)
            {
                var remote = frame.Telemetry;
                if (remote.GridX is not int gridX ||
                    remote.GridY is not int gridY ||
                    remote.TileX is not double tileX ||
                    remote.TileY is not double tileY ||
                    !RoadmapTransform.TryToPixel(
                        layout,
                        bitmap.PixelWidth,
                        bitmap.PixelHeight,
                        gridX,
                        gridY,
                        tileX,
                        tileY,
                        out var x,
                        out var y))
                {
                    continue;
                }

                AddFullMapArrow(
                    imageLeft + x * scale,
                    imageTop + y * scale,
                    remote.HeadingDegrees,
                    14d,
                    new SolidColorBrush(Color.FromRgb(190, 96, 255)),
                    frame.Player.DisplayName);
            }
        }

        if (settings.MapShowTraffic &&
            hasLocalPixel &&
            layout.TileSize is double tileSize &&
            layout.WorldWidth is double worldWidth &&
            layout.WorldHeight is double worldHeight &&
            double.IsFinite(telemetry.X) &&
            double.IsFinite(telemetry.Z))
        {
            var localWorldX =
                (localGridX - layout.MinGridX) * tileSize + localTileX;
            var localWorldY =
                (localGridY - layout.MinGridY) * tileSize + localTileY;

            foreach (var traffic in _localRoadTraffic.Take(48))
            {
                if (!double.IsFinite(traffic.X) ||
                    !double.IsFinite(traffic.Z))
                {
                    continue;
                }

                var trafficWorldX =
                    localWorldX + (traffic.X - telemetry.X);
                var trafficWorldY =
                    localWorldY + (traffic.Z - telemetry.Z);
                var pixelX =
                    trafficWorldX * bitmap.PixelWidth / worldWidth;
                var pixelY =
                    bitmap.PixelHeight -
                    trafficWorldY * bitmap.PixelHeight / worldHeight;

                if (!double.IsFinite(pixelX) || !double.IsFinite(pixelY))
                {
                    continue;
                }

                AddFullMapArrow(
                    imageLeft + pixelX * scale,
                    imageTop + pixelY * scale,
                    TrafficQuaternionToHeadingDegrees(
                        traffic.RotationX,
                        traffic.RotationY,
                        traffic.RotationZ,
                        traffic.RotationW),
                    9d,
                    new SolidColorBrush(Color.FromRgb(70, 140, 255)),
                    "IA");
            }
        }
    }

    private void RenderFullMapCongestion(
        OmsiMapLayout layout,
        BitmapImage bitmap,
        double scale,
        double imageLeft,
        double imageTop,
        NavBR.Shared.Telemetry.VehicleTelemetry telemetry,
        int localGridX,
        int localGridY,
        double localTileX,
        double localTileY,
        double tileSize,
        double worldWidth,
        double worldHeight)
    {
        if (_fullMapCanvas is null ||
            _localRoadTraffic.Count < 3)
        {
            return;
        }

        var localWorldX =
            (localGridX - layout.MinGridX) * tileSize + localTileX;
        var localWorldY =
            (localGridY - layout.MinGridY) * tileSize + localTileY;

        var candidates = _localRoadTraffic
            .Where(item =>
                double.IsFinite(item.X) &&
                double.IsFinite(item.Z) &&
                double.IsFinite(item.SpeedKph) &&
                Math.Abs(item.SpeedKph) <= 18d)
            .Select(item => new
            {
                WorldX = localWorldX + (item.X - telemetry.X),
                WorldY = localWorldY + (item.Z - telemetry.Z),
                Speed = Math.Abs(item.SpeedKph)
            })
            .Where(item =>
                double.IsFinite(item.WorldX) &&
                double.IsFinite(item.WorldY))
            .Take(80)
            .ToArray();

        if (candidates.Length < 3)
        {
            return;
        }

        var consumed = new bool[candidates.Length];
        const double clusterRadiusMeters = 45d;
        var radiusSquared =
            clusterRadiusMeters * clusterRadiusMeters;

        for (var index = 0; index < candidates.Length; index++)
        {
            if (consumed[index])
            {
                continue;
            }

            var seed = candidates[index];
            var members = new List<int>();
            for (var other = index; other < candidates.Length; other++)
            {
                if (consumed[other])
                {
                    continue;
                }

                var dx = candidates[other].WorldX - seed.WorldX;
                var dy = candidates[other].WorldY - seed.WorldY;
                if (dx * dx + dy * dy <= radiusSquared)
                {
                    members.Add(other);
                }
            }

            // Match openOMSI's intent: one car stopped at a light is not a jam.
            if (members.Count < 3)
            {
                continue;
            }

            foreach (var member in members)
            {
                consumed[member] = true;
            }

            var centreWorldX =
                members.Average(member => candidates[member].WorldX);
            var centreWorldY =
                members.Average(member => candidates[member].WorldY);
            var averageSpeed =
                members.Average(member => candidates[member].Speed);

            var pixelX =
                centreWorldX * bitmap.PixelWidth / worldWidth;
            var pixelY =
                bitmap.PixelHeight -
                centreWorldY * bitmap.PixelHeight / worldHeight;
            if (!double.IsFinite(pixelX) || !double.IsFinite(pixelY))
            {
                continue;
            }

            var severity =
                Math.Clamp(1d - averageSpeed / 18d, 0d, 1d);
            var diameter =
                Math.Clamp(28d + members.Count * 5d, 38d, 86d);
            var fill = severity >= 0.60d
                ? Color.FromArgb(92, 215, 62, 55)
                : Color.FromArgb(78, 232, 165, 48);
            var stroke = severity >= 0.60d
                ? Color.FromArgb(185, 255, 108, 93)
                : Color.FromArgb(170, 255, 203, 94);

            var marker = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(stroke),
                StrokeThickness = 1.5d,
                IsHitTestVisible = false,
                ToolTip =
                    $"Tráfego lento • {members.Count} veículos • {averageSpeed:F0} km/h"
            };

            Canvas.SetLeft(
                marker,
                imageLeft + pixelX * scale - diameter / 2d);
            Canvas.SetTop(
                marker,
                imageTop + pixelY * scale - diameter / 2d);
            _fullMapCanvas.Children.Add(marker);
        }
    }

    private void AddFullMapPolyline(
        IReadOnlyList<OmsiRouteTracePoint> points,
        OmsiMapLayout layout,
        BitmapImage bitmap,
        double scale,
        double imageLeft,
        double imageTop,
        Brush stroke,
        double thickness)
    {
        if (_fullMapCanvas is null || points.Count < 2)
        {
            return;
        }

        var collection = new PointCollection();
        var step = Math.Max(1, points.Count / 1800);
        for (var index = 0; index < points.Count; index += step)
        {
            var point = points[index];
            if (RoadmapTransform.TryToPixel(
                    layout,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    point.GridX,
                    point.GridY,
                    point.TileX,
                    point.TileY,
                    out var x,
                    out var y))
            {
                collection.Add(new Point(
                    imageLeft + x * scale,
                    imageTop + y * scale));
            }
        }

        if (collection.Count < 2)
        {
            return;
        }

        _fullMapCanvas.Children.Add(new Polyline
        {
            Points = collection,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        });
    }

    private void AddFullMapDot(
        double x,
        double y,
        double diameter,
        Brush fill,
        string? tooltip)
    {
        if (_fullMapCanvas is null)
        {
            return;
        }

        var dot = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 0.7d,
            ToolTip = tooltip,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, x - diameter / 2d);
        Canvas.SetTop(dot, y - diameter / 2d);
        _fullMapCanvas.Children.Add(dot);
    }

    private void AddFullMapArrow(
        double x,
        double y,
        double headingDegrees,
        double size,
        Brush fill,
        string tooltip)
    {
        if (_fullMapCanvas is null)
        {
            return;
        }

        var marker = new Polygon
        {
            Width = size,
            Height = size * 1.2d,
            Stretch = Stretch.Fill,
            Points = new PointCollection
            {
                new(size / 2d, 0d),
                new(size, size * 1.2d),
                new(size / 2d, size * 0.88d),
                new(0d, size * 1.2d)
            },
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 1d,
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            RenderTransform = new RotateTransform(headingDegrees),
            ToolTip = tooltip,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(marker, x - size / 2d);
        Canvas.SetTop(marker, y - size * 0.6d);
        _fullMapCanvas.Children.Add(marker);
    }

    private static double ClampMapOffset(
        double offset,
        double viewportSize,
        double contentSize)
    {
        if (contentSize <= viewportSize)
        {
            return (viewportSize - contentSize) / 2d;
        }

        return Math.Clamp(offset, viewportSize - contentSize, 0d);
    }
}
