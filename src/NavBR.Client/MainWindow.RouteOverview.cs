using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;

namespace NavBR.Client;

public partial class MainWindow
{
    private ToggleButton? _routeOverviewButton;
    private Polyline? _mainRouteShadow;
    private Polyline? _mainRoutePolyline;
    private string? _mainRouteCacheKey;
    private IReadOnlyList<OmsiRouteTracePoint> _mainRouteTrace = Array.Empty<OmsiRouteTracePoint>();

    internal void InitializeRouteOverviewFeature()
    {
        if (_routeOverviewButton is not null ||
            FitMapButton.Parent is not Panel controls)
        {
            return;
        }

        _routeOverviewButton = new ToggleButton
        {
            MinWidth = 108,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        _routeOverviewButton.Checked += RouteOverviewButton_Changed;
        _routeOverviewButton.Unchecked += RouteOverviewButton_Changed;

        var followIndex = controls.Children.IndexOf(FollowButton);
        controls.Children.Insert(followIndex >= 0 ? followIndex : controls.Children.Count, _routeOverviewButton);

        _mainRouteShadow = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromArgb(215, 0, 0, 0)),
            StrokeThickness = 9d,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _mainRoutePolyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(255, 157, 36)),
            StrokeThickness = 5d,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        var vehicleIndex = RoadmapCanvas.Children.IndexOf(VehicleMarker);
        var insertIndex = vehicleIndex >= 0 ? vehicleIndex : RoadmapCanvas.Children.Count;
        RoadmapCanvas.Children.Insert(insertIndex, _mainRouteShadow);
        RoadmapCanvas.Children.Insert(insertIndex + 1, _mainRoutePolyline);

        FollowButton.Checked += FollowButton_CheckedForRouteOverview;
        LanguageComboBox.SelectionChanged += RouteOverviewLanguageChanged;
        _telemetryTimer.Tick += RouteOverviewTelemetryTick;
        Closed += RouteOverviewWindowClosed;

        UpdateRouteOverviewButtonText();
        RefreshRouteOverviewLayer(forceFit: false);
    }

    private void RouteOverviewTelemetryTick(object? sender, EventArgs e) =>
        RefreshRouteOverviewLayer(forceFit: false);

    private void RouteOverviewLanguageChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateRouteOverviewButtonText();

    private void RouteOverviewWindowClosed(object? sender, EventArgs e)
    {
        FollowButton.Checked -= FollowButton_CheckedForRouteOverview;
        LanguageComboBox.SelectionChanged -= RouteOverviewLanguageChanged;
        _telemetryTimer.Tick -= RouteOverviewTelemetryTick;
        Closed -= RouteOverviewWindowClosed;
    }

    private void FollowButton_CheckedForRouteOverview(object sender, RoutedEventArgs e)
    {
        if (_routeOverviewButton?.IsChecked == true)
        {
            _routeOverviewButton.IsChecked = false;
        }
    }

    private void RouteOverviewButton_Changed(object sender, RoutedEventArgs e)
    {
        if (_routeOverviewButton?.IsChecked == true)
        {
            FollowButton.IsChecked = false;
            RefreshRouteOverviewLayer(forceFit: true);
        }
        else if (_lastVehiclePixelX.HasValue && _lastVehiclePixelY.HasValue)
        {
            FollowButton.IsChecked = true;
            CenterOnVehicle();
        }
    }

    private void UpdateRouteOverviewButtonText()
    {
        if (_routeOverviewButton is null)
        {
            return;
        }

        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        _routeOverviewButton.Content = language switch
        {
            "pt" => "Rota completa",
            "es" => "Ruta completa",
            "de" => "Ganze Route",
            "fr" => "Itinéraire complet",
            _ => "Full route"
        };
        _routeOverviewButton.ToolTip = language switch
        {
            "pt" => "Ajusta o mapa para mostrar toda a rota ativa.",
            "es" => "Ajusta el mapa para mostrar toda la ruta activa.",
            "de" => "Passt die Karte an, um die gesamte aktive Route zu zeigen.",
            "fr" => "Ajuste la carte pour afficher tout l’itinéraire actif.",
            _ => "Fits the map to show the entire active route."
        };
    }

    private void RefreshRouteOverviewLayer(bool forceFit)
    {
        var button = _routeOverviewButton;
        var routeLine = _mainRoutePolyline;
        var routeShadow = _mainRouteShadow;
        var telemetry = _lastTelemetry;
        var layout = _loadedRoadmapLayout;
        var bitmap = _loadedRoadmapBitmap;

        if (button is null || routeLine is null || routeShadow is null)
        {
            return;
        }

        if (telemetry is null ||
            layout is null ||
            bitmap is null ||
            string.IsNullOrWhiteSpace(telemetry.MapName))
        {
            HideMainRouteOverview();
            return;
        }

        var map = FindActiveMap(telemetry.MapName);
        if (map is null || !string.Equals(map.RoadmapPath, _loadedRoadmapPath, StringComparison.OrdinalIgnoreCase))
        {
            HideMainRouteOverview();
            return;
        }

        var lookupTarget = !string.IsNullOrWhiteSpace(telemetry.Route)
            ? telemetry.Route
            : telemetry.DestinationName;
        var cacheKey = $"{map.DirectoryPath}|{telemetry.Line}|{telemetry.Route}|{telemetry.DestinationName}";
        var routeChanged = !string.Equals(cacheKey, _mainRouteCacheKey, StringComparison.OrdinalIgnoreCase);
        if (routeChanged)
        {
            _mainRouteCacheKey = cacheKey;
            _mainRouteTrace = OmsiRouteTraceReader.TryRead(
                map,
                layout,
                lookupTarget,
                telemetry.Line);
        }

        var points = new PointCollection(_mainRouteTrace.Count);
        foreach (var routePoint in _mainRouteTrace)
        {
            if (!RoadmapTransform.TryToPixel(
                    layout,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    routePoint.GridX,
                    routePoint.GridY,
                    routePoint.TileX,
                    routePoint.TileY,
                    out var pixelX,
                    out var pixelY))
            {
                continue;
            }

            points.Add(new Point(pixelX, pixelY));
        }

        if (points.Count < 2)
        {
            HideMainRouteOverview();
            return;
        }

        button.IsEnabled = true;
        routeLine.Points = points;
        routeShadow.Points = points.Clone();
        routeLine.Visibility = Visibility.Visible;
        routeShadow.Visibility = Visibility.Visible;
        UpdateMainRouteStrokeForZoom();

        if (button.IsChecked == true && (forceFit || routeChanged))
        {
            FitActiveRouteToViewport(points);
        }
    }

    private void HideMainRouteOverview()
    {
        if (_mainRoutePolyline is not null)
        {
            _mainRoutePolyline.Visibility = Visibility.Collapsed;
        }
        if (_mainRouteShadow is not null)
        {
            _mainRouteShadow.Visibility = Visibility.Collapsed;
        }
        if (_routeOverviewButton is not null)
        {
            _routeOverviewButton.IsEnabled = false;
            if (_routeOverviewButton.IsChecked == true)
            {
                _routeOverviewButton.IsChecked = false;
            }
        }
    }

    private void FitActiveRouteToViewport(PointCollection points)
    {
        if (points.Count < 2 || _loadedRoadmapBitmap is null)
        {
            return;
        }

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var routeWidth = Math.Max(80d, maxX - minX);
        var routeHeight = Math.Max(80d, maxY - minY);
        var centerX = (minX + maxX) / 2d;
        var centerY = (minY + maxY) / 2d;

        var viewportWidth = RoadmapScrollViewer.ViewportWidth;
        var viewportHeight = RoadmapScrollViewer.ViewportHeight;
        if (viewportWidth <= 1d)
        {
            viewportWidth = Math.Max(1d, RoadmapScrollViewer.ActualWidth - 24d);
        }
        if (viewportHeight <= 1d)
        {
            viewportHeight = Math.Max(1d, RoadmapScrollViewer.ActualHeight - 24d);
        }

        const double paddingFactor = 1.18d;
        var zoom = Math.Min(
            viewportWidth / (routeWidth * paddingFactor),
            viewportHeight / (routeHeight * paddingFactor));
        ApplyRoadmapZoom(Math.Clamp(zoom, MinimumRoadmapZoom, MaximumRoadmapZoom));
        _roadmapZoomInitialized = true;
        UpdateMainRouteStrokeForZoom();

        _ = Dispatcher.BeginInvoke(() =>
        {
            var horizontalOffset = centerX * _roadmapZoom - RoadmapScrollViewer.ViewportWidth / 2d;
            var verticalOffset = centerY * _roadmapZoom - RoadmapScrollViewer.ViewportHeight / 2d;
            RoadmapScrollViewer.ScrollToHorizontalOffset(Math.Max(0d, horizontalOffset));
            RoadmapScrollViewer.ScrollToVerticalOffset(Math.Max(0d, verticalOffset));
        }, DispatcherPriority.Loaded);
    }

    private void UpdateMainRouteStrokeForZoom()
    {
        var safeZoom = Math.Max(MinimumRoadmapZoom, _roadmapZoom);
        if (_mainRoutePolyline is not null)
        {
            _mainRoutePolyline.StrokeThickness = 5d / safeZoom;
        }
        if (_mainRouteShadow is not null)
        {
            _mainRouteShadow.StrokeThickness = 9d / safeZoom;
        }
    }
}
