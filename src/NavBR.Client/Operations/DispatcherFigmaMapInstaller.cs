using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Operations;

/// <summary>
/// Replaces the old illustrative CCO grid with the real OMSI roadmap. Every
/// rendered position comes from local/remote telemetry and every route comes
/// from installed OMSI route geometry; unavailable data stays unavailable.
/// </summary>
internal static class DispatcherFigmaMapInstaller
{
    private static readonly HashSet<DispatcherWindow> Installed = new();

    public static void Attach(
        DispatcherWindow window,
        Func<VehicleTelemetry?> telemetrySource,
        Func<OmsiMapInfo?> activeMapSource)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var schematic = FindSchematicGrid(window);
        if (schematic is null)
        {
            Installed.Remove(window);
            return;
        }

        var session = new MapSession(window, schematic, telemetrySource, activeMapSource);
        session.Start();
        window.Closed += (_, _) =>
        {
            session.Dispose();
            Installed.Remove(window);
        };
    }

    private static Grid? FindSchematicGrid(DependencyObject root)
    {
        var busText = Enumerate<TextBlock>(root)
            .FirstOrDefault(text => string.Equals(text.Text?.Trim(), "BUS", StringComparison.OrdinalIgnoreCase));
        if (busText is not null)
        {
            DependencyObject? current = busText;
            while (current is not null)
            {
                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
                if (current is Grid grid && grid.RowDefinitions.Count == 6 && grid.ColumnDefinitions.Count == 6)
                {
                    return grid;
                }
            }
        }

        return Enumerate<Grid>(root)
            .FirstOrDefault(grid => grid.RowDefinitions.Count == 6 && grid.ColumnDefinitions.Count == 6 && grid.MinHeight >= 240d);
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private sealed class MapSession : IDisposable
    {
        private readonly DispatcherWindow _window;
        private readonly Grid _host;
        private readonly Func<VehicleTelemetry?> _telemetrySource;
        private readonly Func<OmsiMapInfo?> _activeMapSource;
        private readonly DispatcherTimer _timer;
        private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both };
        private readonly Canvas _canvas = new();
        private readonly Image _roadmap = new() { Stretch = Stretch.Fill };
        private readonly Polyline _routeShadow = new()
        {
            Stroke = new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)),
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
        private readonly Polyline _route = new()
        {
            Stroke = Brush(61, 137, 196),
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };
        private readonly Grid _localMarker = CreateBusMarker(Brush(113, 198, 255), Brushes.White);
        private readonly TextBlock _status = new()
        {
            Foreground = Brush(218, 230, 238),
            FontSize = 9.5d,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        private readonly Dictionary<string, Grid> _remoteMarkers = new(StringComparer.OrdinalIgnoreCase);

        private OmsiMapInfo? _map;
        private OmsiMapLayout? _layout;
        private BitmapSource? _bitmap;
        private string? _mapKey;
        private string? _routeKey;

        public MapSession(
            DispatcherWindow window,
            Grid host,
            Func<VehicleTelemetry?> telemetrySource,
            Func<OmsiMapInfo?> activeMapSource)
        {
            _window = window;
            _host = host;
            _telemetrySource = telemetrySource;
            _activeMapSource = activeMapSource;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300d) };
            _timer.Tick += (_, _) => Refresh();

            BuildSurface();
        }

        public void Start()
        {
            Refresh();
            _timer.Start();
        }

        public void Dispose() => _timer.Stop();

        private void BuildSurface()
        {
            _host.Children.Clear();
            _host.RowDefinitions.Clear();
            _host.ColumnDefinitions.Clear();
            _host.Background = Brush(5, 14, 21);
            _host.ClipToBounds = true;

            _canvas.Children.Add(_roadmap);
            _canvas.Children.Add(_routeShadow);
            _canvas.Children.Add(_route);
            _canvas.Children.Add(_localMarker);
            _localMarker.Visibility = Visibility.Collapsed;
            _viewbox.Child = _canvas;
            _host.Children.Add(_viewbox);

            var statusCard = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10d),
                Padding = new Thickness(10d, 7d, 10d, 7d),
                Background = new SolidColorBrush(Color.FromArgb(225, 7, 18, 27)),
                BorderBrush = Brush(28, 42, 51),
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(8d),
                Child = _status
            };
            _host.Children.Add(statusCard);
        }

        private void Refresh()
        {
            var map = _activeMapSource();
            if (!EnsureMap(map))
            {
                SetUnavailable(map is null
                    ? T("MAPA REAL • aguardando mapa ativo do OMSI", "REAL MAP • waiting for the active OMSI map", "MAPA REAL • esperando el mapa activo de OMSI", "ECHTE KARTE • warte auf die aktive OMSI-Karte", "CARTE RÉELLE • attente de la carte OMSI active")
                    : T("MAPA REAL • roadmap/layout indisponível para o mapa ativo", "REAL MAP • roadmap/layout unavailable for the active map", "MAPA REAL • roadmap/layout no disponible para el mapa activo", "ECHTE KARTE • Roadmap/Layout für die aktive Karte nicht verfügbar", "CARTE RÉELLE • roadmap/layout indisponible pour la carte active"));
                return;
            }

            _viewbox.Visibility = Visibility.Visible;
            var telemetry = _telemetrySource();
            RefreshRoute(telemetry);
            RefreshLocalMarker(telemetry);
            var remotes = RefreshRemoteMarkers();

            var routeState = _route.Points.Count >= 2
                ? T("rota real", "real route", "ruta real", "echte Route", "itinéraire réel")
                : T("sem rota ativa", "no active route", "sin ruta activa", "keine aktive Route", "aucun itinéraire actif");
            _status.Text = $"{T("MAPA REAL", "REAL MAP", "MAPA REAL", "ECHTE KARTE", "CARTE RÉELLE")} • {_map?.DisplayName ?? _map?.FolderName ?? "—"} • {routeState} • {RemoteCount(remotes)}";
        }

        private bool EnsureMap(OmsiMapInfo? map)
        {
            if (map is null || string.IsNullOrWhiteSpace(map.RoadmapPath) || !File.Exists(map.RoadmapPath))
            {
                return false;
            }

            var key = $"{map.DirectoryPath}|{map.RoadmapPath}|{map.CompatibilityId}";
            if (string.Equals(key, _mapKey, StringComparison.OrdinalIgnoreCase))
            {
                return _layout is not null && _bitmap is not null;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(map.RoadmapPath!, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
                if (layout is null)
                {
                    return false;
                }

                _map = map;
                _layout = layout;
                _bitmap = bitmap;
                _mapKey = key;
                _routeKey = null;
                _route.Points.Clear();
                _routeShadow.Points.Clear();

                _canvas.Width = bitmap.PixelWidth;
                _canvas.Height = bitmap.PixelHeight;
                _roadmap.Width = bitmap.PixelWidth;
                _roadmap.Height = bitmap.PixelHeight;
                _roadmap.Source = bitmap;

                var routeStroke = Math.Clamp(bitmap.PixelWidth * 0.0032d, 5d, 16d);
                _route.StrokeThickness = routeStroke;
                _routeShadow.StrokeThickness = routeStroke + Math.Max(3d, routeStroke * 0.7d);

                ResizeMarker(_localMarker, bitmap.PixelWidth, local: true);
                foreach (var marker in _remoteMarkers.Values)
                {
                    _canvas.Children.Remove(marker);
                }
                _remoteMarkers.Clear();
                return true;
            }
            catch
            {
                _map = null;
                _layout = null;
                _bitmap = null;
                _mapKey = null;
                return false;
            }
        }

        private void RefreshRoute(VehicleTelemetry? telemetry)
        {
            if (_map is null || _layout is null || _bitmap is null || telemetry is null)
            {
                ClearRoute();
                return;
            }

            var target = !string.IsNullOrWhiteSpace(telemetry.Route)
                ? telemetry.Route
                : telemetry.DestinationName;
            var key = $"{_map.DirectoryPath}|{telemetry.Line}|{telemetry.Route}|{telemetry.DestinationName}";
            if (string.Equals(key, _routeKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _routeKey = key;
            var trace = OmsiRouteTraceReader.TryRead(_map, _layout, target, telemetry.Line);
            var points = new PointCollection(trace.Count);
            foreach (var point in trace)
            {
                if (RoadmapTransform.TryToPixel(
                    _layout,
                    _bitmap.PixelWidth,
                    _bitmap.PixelHeight,
                    point.GridX,
                    point.GridY,
                    point.TileX,
                    point.TileY,
                    out var x,
                    out var y))
                {
                    points.Add(new Point(x, y));
                }
            }

            if (points.Count < 2)
            {
                ClearRoute();
                return;
            }

            _route.Points = points;
            _routeShadow.Points = points.Clone();
        }

        private void ClearRoute()
        {
            _routeKey = null;
            _route.Points.Clear();
            _routeShadow.Points.Clear();
        }

        private void RefreshLocalMarker(VehicleTelemetry? telemetry)
        {
            if (!TryGetPixel(
                    telemetry?.MapName,
                    telemetry?.MapCompatibilityId,
                    telemetry?.GridX,
                    telemetry?.GridY,
                    telemetry?.TileX,
                    telemetry?.TileY,
                    out var x,
                    out var y))
            {
                _localMarker.Visibility = Visibility.Collapsed;
                return;
            }

            PositionMarker(_localMarker, x, y, telemetry?.HeadingDegrees ?? 0d);
            _localMarker.ToolTip = $"LOCAL • {telemetry?.VehicleName ?? T("Ônibus", "Bus", "Autobús", "Bus", "Bus")} • {telemetry?.SpeedKph ?? 0d:0.0} km/h";
            _localMarker.Visibility = Visibility.Visible;
        }

        private int RefreshRemoteMarkers()
        {
            var snapshot = DispatcherSessionFeed.Snapshot();
            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var count = 0;

            foreach (var driver in snapshot.RemoteDrivers)
            {
                if (DateTimeOffset.UtcNow - driver.ReceivedAtUtc > TimeSpan.FromSeconds(10d) ||
                    !TryGetPixel(
                        driver.MapName,
                        driver.MapCompatibilityId,
                        driver.GridX,
                        driver.GridY,
                        driver.TileX,
                        driver.TileY,
                        out var x,
                        out var y))
                {
                    continue;
                }

                if (!_remoteMarkers.TryGetValue(driver.PlayerId, out var marker))
                {
                    marker = CreateBusMarker(Brush(56, 201, 140), Brushes.White);
                    if (_bitmap is not null)
                    {
                        ResizeMarker(marker, _bitmap.PixelWidth, local: false);
                    }
                    _remoteMarkers[driver.PlayerId] = marker;
                    _canvas.Children.Add(marker);
                }

                PositionMarker(marker, x, y, driver.HeadingDegrees);
                marker.ToolTip = $"{driver.DisplayName} • {driver.VehicleName ?? T("Ônibus", "Bus", "Autobús", "Bus", "Bus")} • {driver.SpeedKph:0.0} km/h";
                marker.Visibility = Visibility.Visible;
                visible.Add(driver.PlayerId);
                count++;
            }

            foreach (var pair in _remoteMarkers)
            {
                if (!visible.Contains(pair.Key))
                {
                    pair.Value.Visibility = Visibility.Collapsed;
                }
            }

            return count;
        }

        private bool TryGetPixel(
            string? mapName,
            string? mapCompatibilityId,
            int? gridX,
            int? gridY,
            double? tileX,
            double? tileY,
            out double x,
            out double y)
        {
            x = 0d;
            y = 0d;
            if (_map is null || _layout is null || _bitmap is null ||
                gridX is not int gx || gridY is not int gy ||
                tileX is not double tx || tileY is not double ty)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mapCompatibilityId) &&
                !string.IsNullOrWhiteSpace(_map.CompatibilityId) &&
                !string.Equals(mapCompatibilityId, _map.CompatibilityId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mapName) && !MapNamesMatch(mapName, _map.DisplayName, _map.FolderName))
            {
                return false;
            }

            return RoadmapTransform.TryToPixel(
                _layout,
                _bitmap.PixelWidth,
                _bitmap.PixelHeight,
                gx,
                gy,
                tx,
                ty,
                out x,
                out y) &&
                x >= 0d && x <= _bitmap.PixelWidth &&
                y >= 0d && y <= _bitmap.PixelHeight;
        }

        private void SetUnavailable(string message)
        {
            _viewbox.Visibility = Visibility.Collapsed;
            _localMarker.Visibility = Visibility.Collapsed;
            foreach (var marker in _remoteMarkers.Values)
            {
                marker.Visibility = Visibility.Collapsed;
            }
            _status.Text = message;
        }

        private static void PositionMarker(Grid marker, double x, double y, double heading)
        {
            Canvas.SetLeft(marker, x - marker.Width / 2d);
            Canvas.SetTop(marker, y - marker.Height / 2d);
            if (marker.RenderTransform is RotateTransform rotation)
            {
                rotation.Angle = double.IsFinite(heading) ? heading : 0d;
            }
        }

        private static void ResizeMarker(Grid marker, int bitmapWidth, bool local)
        {
            var size = Math.Clamp(bitmapWidth * (local ? 0.018d : 0.014d), local ? 38d : 30d, local ? 100d : 82d);
            marker.Width = size;
            marker.Height = size;
        }

        private static bool MapNamesMatch(string value, string displayName, string folderName)
        {
            var target = Normalize(value);
            return target.Length > 0 &&
                   (target == Normalize(displayName) || target == Normalize(folderName));
        }

        private static string Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string RemoteCount(int count)
    {
        var value = count == 1
            ? T("{0} remoto visível", "{0} remote visible", "{0} remoto visible", "{0} Remote-Fahrzeug sichtbar", "{0} distant visible")
            : T("{0} remotos visíveis", "{0} remotes visible", "{0} remotos visibles", "{0} Remote-Fahrzeuge sichtbar", "{0} distants visibles");
        return string.Format(value, count);
    }

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static Grid CreateBusMarker(Brush fill, Brush stroke)
    {
        var marker = new Grid
        {
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            RenderTransform = new RotateTransform(),
            IsHitTestVisible = true
        };
        marker.Children.Add(new Ellipse
        {
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 2d
        });
        marker.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(0.50d, 0.10d),
                new(0.76d, 0.72d),
                new(0.50d, 0.58d),
                new(0.24d, 0.72d)
            },
            Stretch = Stretch.Fill,
            Fill = Brushes.White,
            Margin = new Thickness(8d)
        });
        return marker;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
