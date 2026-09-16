using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private readonly Dictionary<string, FrameworkElement> _busStopMarkers = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<OmsiBusStopPoint> _busStops = Array.Empty<OmsiBusStopPoint>();
    private IReadOnlySet<string> _activeRouteStopNames = new HashSet<string>(StringComparer.Ordinal);
    private IReadOnlyList<OmsiRouteTracePoint> _activeRouteStopTrace = Array.Empty<OmsiRouteTracePoint>();
    private DispatcherTimer? _busStopRenderTimer;
    private Button? _stopIconSettingsButton;
    private string? _busStopMapKey;
    private string? _busStopMarkerStyleKey;
    private string? _busStopRouteKey;
    private bool _busStopRouteActive;
    private BitmapSource? _customBusStopIcon;

    private void InitializeBusStopHud()
    {
        if (_busStopRenderTimer is not null)
        {
            return;
        }

        _busStopRenderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _busStopRenderTimer.Tick += BusStopRenderTimer_Tick;
        _busStopRenderTimer.Start();
        MultiplayerSettingsStore.SettingsSaved += BusStopSettingsSaved;
        Closed += BusStopWindow_Closed;
        InstallStopIconSettingsButton();
        RenderBusStops();
    }

    private void BusStopRenderTimer_Tick(object? sender, EventArgs e) => RenderBusStops();

    private void BusStopSettingsSaved(MultiplayerSettings settings)
    {
        var styleKey = BuildStopStyleKey(settings);
        _hudSettings = settings;
        UpdateStopIconSettingsButtonText();
        if (!string.Equals(styleKey, _busStopMarkerStyleKey, StringComparison.OrdinalIgnoreCase))
        {
            InvalidateBusStopMarkers();
        }
    }

    private void BusStopWindow_Closed(object? sender, EventArgs e)
    {
        if (_busStopRenderTimer is not null)
        {
            _busStopRenderTimer.Stop();
            _busStopRenderTimer.Tick -= BusStopRenderTimer_Tick;
            _busStopRenderTimer = null;
        }

        MultiplayerSettingsStore.SettingsSaved -= BusStopSettingsSaved;
        Closed -= BusStopWindow_Closed;

        if (_stopIconSettingsButton?.Parent is Panel parent)
        {
            parent.Children.Remove(_stopIconSettingsButton);
        }
        _stopIconSettingsButton = null;
    }

    private void InstallStopIconSettingsButton()
    {
        if (_stopIconSettingsButton is not null ||
            Application.Current.MainWindow is not NavBR.Client.MainWindow mainWindow ||
            mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton ||
            multiplayerButton.Parent is not Panel parent)
        {
            return;
        }

        _stopIconSettingsButton = new Button
        {
            MinWidth = 110,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _stopIconSettingsButton.Click += (_, _) => ShowStopIconSettingsMenu();
        var index = parent.Children.IndexOf(multiplayerButton);
        parent.Children.Insert(Math.Max(0, index), _stopIconSettingsButton);
        UpdateStopIconSettingsButtonText();
    }

    private void UpdateStopIconSettingsButtonText()
    {
        if (_stopIconSettingsButton is null)
        {
            return;
        }

        var style = _hudSettings.StopIconStyle?.ToLowerInvariant() switch
        {
            "dot" => StopIconText("Simples", "Simple", "Simple", "Einfach", "Simple"),
            "custom" => StopIconText("Personalizado", "Custom", "Personalizado", "Benutzer", "Personnalisé"),
            _ => "OMSI"
        };
        _stopIconSettingsButton.Content = $"{StopIconText("Paradas", "Stops", "Paradas", "Halte", "Arrêts")}: {style}";
    }

    private void ShowStopIconSettingsMenu()
    {
        if (_stopIconSettingsButton is null)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Items.Add(NewStopStyleMenuItem(
            StopIconText("Padrão OMSI", "OMSI default", "Predeterminado OMSI", "OMSI-Standard", "Standard OMSI"),
            "omsi"));
        menu.Items.Add(NewStopStyleMenuItem(
            StopIconText("Minimalista", "Minimal", "Minimalista", "Minimal", "Minimaliste"),
            "dot"));
        menu.Items.Add(new Separator());

        var custom = new MenuItem
        {
            Header = StopIconText(
                "Escolher imagem personalizada…",
                "Choose custom image…",
                "Elegir imagen personalizada…",
                "Eigenes Bild wählen…",
                "Choisir une image…")
        };
        custom.Click += (_, _) => ChooseCustomStopIcon();
        menu.Items.Add(custom);

        if (!string.IsNullOrWhiteSpace(_hudSettings.StopCustomIconPath))
        {
            var reuse = new MenuItem
            {
                Header = StopIconText(
                    "Usar imagem personalizada salva",
                    "Use saved custom image",
                    "Usar imagen guardada",
                    "Gespeichertes Bild verwenden",
                    "Utiliser l’image enregistrée"),
                IsCheckable = true,
                IsChecked = string.Equals(_hudSettings.StopIconStyle, "custom", StringComparison.OrdinalIgnoreCase)
            };
            reuse.Click += (_, _) => SaveStopIconSettings("custom", _hudSettings.StopCustomIconPath);
            menu.Items.Add(reuse);
        }

        _stopIconSettingsButton.ContextMenu = menu;
        menu.PlacementTarget = _stopIconSettingsButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private MenuItem NewStopStyleMenuItem(string header, string style)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = string.Equals(_hudSettings.StopIconStyle, style, StringComparison.OrdinalIgnoreCase)
        };
        item.Click += (_, _) => SaveStopIconSettings(style, _hudSettings.StopCustomIconPath);
        return item;
    }

    private void ChooseCustomStopIcon()
    {
        var dialog = new OpenFileDialog
        {
            Title = StopIconText(
                "Escolher ícone das paradas",
                "Choose stop icon",
                "Elegir icono de paradas",
                "Haltestellensymbol wählen",
                "Choisir l’icône des arrêts"),
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp|PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(Application.Current.MainWindow) == true)
        {
            SaveStopIconSettings("custom", dialog.FileName);
        }
    }

    private void SaveStopIconSettings(string style, string? customPath)
    {
        _hudSettings = _hudSettings with
        {
            StopIconStyle = style,
            StopCustomIconPath = customPath
        };
        MultiplayerSettingsStore.Save(_hudSettings);
        UpdateStopIconSettingsButtonText();
        InvalidateBusStopMarkers();
    }

    private void InvalidateBusStopMarkers()
    {
        foreach (var marker in _busStopMarkers.Values)
        {
            MiniMapCanvas.Children.Remove(marker);
        }

        _busStopMarkers.Clear();
        _busStopMarkerStyleKey = null;
        _customBusStopIcon = null;
        RenderBusStops();
    }

    private void RenderBusStops()
    {
        var map = _activeMap;
        var layout = _mapLayout;
        var bitmap = _mapBitmap;
        var telemetry = _localTelemetry;
        if (map is null ||
            layout is null ||
            bitmap is null ||
            telemetry is null ||
            telemetry.GridX is not int localGridX ||
            telemetry.GridY is not int localGridY ||
            telemetry.TileX is not double localTileX ||
            telemetry.TileY is not double localTileY ||
            !RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                localGridX,
                localGridY,
                localTileX,
                localTileY,
                out var localPixelX,
                out var localPixelY))
        {
            HideBusStopMarkers();
            return;
        }

        var mapKey = $"{map.DirectoryPath}|{map.GlobalConfigPath}";
        if (!string.Equals(mapKey, _busStopMapKey, StringComparison.OrdinalIgnoreCase))
        {
            _busStopMapKey = mapKey;
            _busStopRouteKey = null;
            _busStops = OmsiBusStopReader.TryRead(map);
            foreach (var marker in _busStopMarkers.Values)
            {
                MiniMapCanvas.Children.Remove(marker);
            }
            _busStopMarkers.Clear();
        }

        var styleKey = BuildStopStyleKey(_hudSettings);
        if (!string.Equals(styleKey, _busStopMarkerStyleKey, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var marker in _busStopMarkers.Values)
            {
                MiniMapCanvas.Children.Remove(marker);
            }
            _busStopMarkers.Clear();
            _customBusStopIcon = null;
            _busStopMarkerStyleKey = styleKey;
        }

        RefreshActiveRouteStopFilter(map, layout, telemetry);
        var stopsToRender = GetStopsForActiveRoute(layout, telemetry.NextStopName);

        const double canvasWidth = 296d;
        const double canvasHeight = 186d;
        const double sourceViewWidth = 900d;
        var scale = canvasWidth / sourceViewWidth;
        var zoom = Math.Max(0.65d, MiniMapContentScale.ScaleX);
        var nextStopKey = FindNearestNextStopKey(
            telemetry.NextStopName,
            layout,
            localGridX,
            localGridY,
            localTileX,
            localTileY,
            stopsToRender);
        var visibleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stop in stopsToRender)
        {
            if (!RoadmapTransform.TryToPixel(
                    layout,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    stop.GridX,
                    stop.GridY,
                    stop.TileX,
                    stop.TileY,
                    out var pixelX,
                    out var pixelY))
            {
                continue;
            }

            var x = canvasWidth / 2d + (pixelX - localPixelX) * scale;
            var y = canvasHeight / 2d + (pixelY - localPixelY) * scale;
            if (x < -45d || x > canvasWidth + 45d || y < -45d || y > canvasHeight + 45d)
            {
                continue;
            }

            var key = StopKey(stop);
            var isNext = string.Equals(key, nextStopKey, StringComparison.OrdinalIgnoreCase);
            var marker = GetOrCreateBusStopMarker(stop);
            UpdateBusStopMarkerVisual(marker, stop, isNext, telemetry.HeadingDegrees, zoom);
            Canvas.SetLeft(marker, x - marker.Width / 2d);
            Canvas.SetTop(marker, y - marker.Height / 2d);
            marker.Visibility = Visibility.Visible;
            visibleKeys.Add(key);
        }

        foreach (var pair in _busStopMarkers)
        {
            if (!visibleKeys.Contains(pair.Key))
            {
                pair.Value.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RefreshActiveRouteStopFilter(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        VehicleTelemetry telemetry)
    {
        var routeActive = HasActiveRoute(telemetry);
        var routeKey = routeActive
            ? $"{map.DirectoryPath}|{telemetry.Line}|{telemetry.Route}|{telemetry.DestinationName}"
            : $"{map.DirectoryPath}|<all-stops>";

        if (string.Equals(routeKey, _busStopRouteKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _busStopRouteKey = routeKey;
        _busStopRouteActive = routeActive;
        _activeRouteStopNames = new HashSet<string>(StringComparer.Ordinal);
        _activeRouteStopTrace = Array.Empty<OmsiRouteTracePoint>();

        if (!routeActive)
        {
            return;
        }

        var resolved = OmsiActiveRouteStopReader.TryRead(
            map,
            telemetry.Route,
            telemetry.Line,
            telemetry.DestinationName);
        _activeRouteStopNames = resolved.StopNames;

        var lookupTarget = !string.IsNullOrWhiteSpace(telemetry.Route)
            ? telemetry.Route
            : telemetry.DestinationName;
        _activeRouteStopTrace = OmsiRouteTraceReader.TryRead(
            map,
            layout,
            lookupTarget,
            telemetry.Line);
    }

    private IReadOnlyList<OmsiBusStopPoint> GetStopsForActiveRoute(
        OmsiMapLayout layout,
        string? nextStopName)
    {
        if (!_busStopRouteActive)
        {
            return _busStops;
        }

        var nextStopNormalized = NormalizeStopName(nextStopName ?? string.Empty);
        var selected = new List<OmsiBusStopPoint>();
        foreach (var stop in _busStops)
        {
            var normalizedName = NormalizeStopName(stop.Name);
            var belongsToTrip = normalizedName.Length > 0 &&
                                _activeRouteStopNames.Contains(normalizedName);
            var isNextStop = nextStopNormalized.Length > 0 &&
                             StopNamesMatch(normalizedName, nextStopNormalized);

            if (belongsToTrip || isNextStop)
            {
                selected.Add(stop);
                continue;
            }

            // Older/custom maps may use [station_typ2] or incomplete TTData
            // that cannot be resolved to names. In that case use the active
            // track geometry as a strict fallback instead of showing every stop.
            if (_activeRouteStopNames.Count == 0 && IsStopNearActiveRoute(stop, layout))
            {
                selected.Add(stop);
            }
        }

        return selected;
    }

    private bool IsStopNearActiveRoute(OmsiBusStopPoint stop, OmsiMapLayout layout)
    {
        if (_activeRouteStopTrace.Count < 2 || layout.TileSize is not double tileSize)
        {
            return false;
        }

        const double maxDistanceMeters = 70d;
        var maxDistanceSquared = maxDistanceMeters * maxDistanceMeters;
        var stopX = stop.GridX * tileSize + stop.TileX;
        var stopY = stop.GridY * tileSize + stop.TileY;

        for (var index = 1; index < _activeRouteStopTrace.Count; index++)
        {
            var previous = _activeRouteStopTrace[index - 1];
            var current = _activeRouteStopTrace[index];
            var ax = previous.GridX * tileSize + previous.TileX;
            var ay = previous.GridY * tileSize + previous.TileY;
            var bx = current.GridX * tileSize + current.TileX;
            var by = current.GridY * tileSize + current.TileY;
            if (DistanceToSegmentSquared(stopX, stopY, ax, ay, bx, by) <= maxDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static double DistanceToSegmentSquared(
        double px,
        double py,
        double ax,
        double ay,
        double bx,
        double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.000001d)
        {
            var pointDx = px - ax;
            var pointDy = py - ay;
            return pointDx * pointDx + pointDy * pointDy;
        }

        var projection = ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
        projection = Math.Clamp(projection, 0d, 1d);
        var nearestX = ax + projection * dx;
        var nearestY = ay + projection * dy;
        var nearestDx = px - nearestX;
        var nearestDy = py - nearestY;
        return nearestDx * nearestDx + nearestDy * nearestDy;
    }

    private static bool HasActiveRoute(VehicleTelemetry telemetry) =>
        !string.IsNullOrWhiteSpace(telemetry.Route) ||
        (!string.IsNullOrWhiteSpace(telemetry.Line) &&
         (!string.IsNullOrWhiteSpace(telemetry.DestinationName) ||
          !string.IsNullOrWhiteSpace(telemetry.NextStopName) ||
          telemetry.CurrentStopIndex.HasValue));

    private string? FindNearestNextStopKey(
        string? nextStopName,
        OmsiMapLayout layout,
        int localGridX,
        int localGridY,
        double localTileX,
        double localTileY,
        IReadOnlyList<OmsiBusStopPoint> candidates)
    {
        if (string.IsNullOrWhiteSpace(nextStopName) || layout.TileSize is not double tileSize)
        {
            return null;
        }

        var normalizedTarget = NormalizeStopName(nextStopName);
        if (normalizedTarget.Length == 0)
        {
            return null;
        }

        var localWorldX = localGridX * tileSize + localTileX;
        var localWorldY = localGridY * tileSize + localTileY;
        OmsiBusStopPoint? nearest = null;
        var nearestDistanceSquared = double.MaxValue;

        foreach (var stop in candidates)
        {
            var normalizedName = NormalizeStopName(stop.Name);
            if (!StopNamesMatch(normalizedName, normalizedTarget))
            {
                continue;
            }

            var worldX = stop.GridX * tileSize + stop.TileX;
            var worldY = stop.GridY * tileSize + stop.TileY;
            var dx = worldX - localWorldX;
            var dy = worldY - localWorldY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = stop;
            }
        }

        return nearest is null ? null : StopKey(nearest);
    }

    private FrameworkElement GetOrCreateBusStopMarker(OmsiBusStopPoint stop)
    {
        var key = StopKey(stop);
        if (_busStopMarkers.TryGetValue(key, out var marker))
        {
            return marker;
        }

        marker = BuildBusStopMarker();
        marker.ToolTip = stop.Name;
        Panel.SetZIndex(marker, 24);
        MiniMapCanvas.Children.Add(marker);
        _busStopMarkers[key] = marker;
        return marker;
    }

    private FrameworkElement BuildBusStopMarker()
    {
        const double size = 14d;
        if (string.Equals(_hudSettings.StopIconStyle, "custom", StringComparison.OrdinalIgnoreCase) &&
            TryLoadCustomBusStopIcon() is BitmapSource customIcon)
        {
            return new Image
            {
                Width = size,
                Height = size,
                Source = customIcon,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5d, 0.5d)
            };
        }

        if (string.Equals(_hudSettings.StopIconStyle, "dot", StringComparison.OrdinalIgnoreCase))
        {
            return new Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(23, 139, 67)),
                StrokeThickness = 1.8d,
                RenderTransformOrigin = new Point(0.5d, 0.5d)
            };
        }

        var grid = new Grid
        {
            Width = size,
            Height = size,
            RenderTransformOrigin = new Point(0.5d, 0.5d)
        };
        grid.Children.Add(new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromRgb(247, 211, 47)),
            Stroke = new SolidColorBrush(Color.FromRgb(24, 122, 55)),
            StrokeThickness = 2d
        });
        grid.Children.Add(new TextBlock
        {
            Text = "H",
            Foreground = new SolidColorBrush(Color.FromRgb(19, 102, 47)),
            FontSize = 8d,
            FontWeight = FontWeights.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -1, 0, 0)
        });
        return grid;
    }

    private static void UpdateBusStopMarkerVisual(
        FrameworkElement marker,
        OmsiBusStopPoint stop,
        bool isNext,
        double headingDegrees,
        double zoom)
    {
        marker.ToolTip = isNext ? $"Próxima parada: {stop.Name}" : stop.Name;
        Panel.SetZIndex(marker, isNext ? 31 : 24);
        marker.Effect = isNext
            ? new DropShadowEffect
            {
                Color = Colors.Orange,
                BlurRadius = 10d,
                ShadowDepth = 0d,
                Opacity = 0.95d
            }
            : null;

        if (marker is Ellipse dot)
        {
            dot.Fill = isNext ? Brushes.Orange : Brushes.White;
            dot.Stroke = isNext ? Brushes.White : new SolidColorBrush(Color.FromRgb(23, 139, 67));
            dot.StrokeThickness = isNext ? 2.2d : 1.8d;
        }

        var transforms = new TransformGroup();
        transforms.Children.Add(new RotateTransform(headingDegrees));
        var emphasis = isNext ? 1.45d : 1d;
        transforms.Children.Add(new ScaleTransform(emphasis / zoom, emphasis / zoom));
        marker.RenderTransform = transforms;
    }

    private BitmapSource? TryLoadCustomBusStopIcon()
    {
        if (_customBusStopIcon is not null)
        {
            return _customBusStopIcon;
        }

        var path = _hudSettings.StopCustomIconPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 96;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            _customBusStopIcon = image;
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void HideBusStopMarkers()
    {
        foreach (var marker in _busStopMarkers.Values)
        {
            marker.Visibility = Visibility.Collapsed;
        }
    }

    private static string BuildStopStyleKey(MultiplayerSettings settings) =>
        $"{settings.StopIconStyle}|{settings.StopCustomIconPath}";

    private static string StopKey(OmsiBusStopPoint stop) =>
        $"{stop.GridX}:{stop.GridY}:{stop.ObjectId}";

    private static string StopIconText(string pt, string en, string es, string de, string fr) =>
        NavBR.Client.Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static bool StopNamesMatch(string normalizedName, string normalizedTarget) =>
        normalizedName.Length > 0 &&
        normalizedTarget.Length > 0 &&
        (string.Equals(normalizedName, normalizedTarget, StringComparison.Ordinal) ||
         normalizedName.Contains(normalizedTarget, StringComparison.Ordinal) ||
         normalizedTarget.Contains(normalizedName, StringComparison.Ordinal));

    private static string NormalizeStopName(string value) =>
        new(value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
