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
            Background = new SolidColorBrush(Color.FromRgb(4, 9, 14)),
            ClipToBounds = true,
            Focusable = true
        };
        _fullMapCanvas.PreviewMouseWheel += (_, e) =>
        {
            AdjustFullMapZoom(e.Delta > 0 ? 0.15d : -0.15d);
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
            Background = new SolidColorBrush(Color.FromRgb(4, 9, 14)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(38, 68, 88)),
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
            Background = new SolidColorBrush(Color.FromArgb(248, 5, 14, 21)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(235, 57, 135, 174)),
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

        if (zoom <= 1.001d || !hasLocalPixel)
        {
            imageLeft = (width - imageWidth) / 2d;
            imageTop = (height - imageHeight) / 2d;
        }
        else
        {
            imageLeft = width / 2d - focusPixelX * scale;
            imageTop = height / 2d - focusPixelY * scale;
            imageLeft = ClampMapOffset(imageLeft, width, imageWidth);
            imageTop = ClampMapOffset(imageTop, height, imageHeight);
        }

        _fullMapCanvas.Children.Clear();

        var image = new Image
        {
            Source = bitmap,
            Width = imageWidth,
            Height = imageHeight,
            Stretch = Stretch.Fill,
            Opacity = Math.Clamp(settings.HudMapOpacity + 0.30d, 0.55d, 1d),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(image, imageLeft);
        Canvas.SetTop(image, imageTop);
        _fullMapCanvas.Children.Add(image);

        if (settings.MapShowRoute)
        {
            AddFullMapPolyline(
                _routeTracePoints,
                layout,
                bitmap,
                scale,
                imageLeft,
                imageTop,
                new SolidColorBrush(Color.FromRgb(255, 148, 34)),
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
                new SolidColorBrush(Color.FromRgb(87, 207, 255)),
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
                new SolidColorBrush(Color.FromRgb(255, 145, 35)),
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
                    new SolidColorBrush(Color.FromRgb(80, 177, 255)),
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
                    new SolidColorBrush(Color.FromRgb(102, 166, 208)),
                    "IA");
            }
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
