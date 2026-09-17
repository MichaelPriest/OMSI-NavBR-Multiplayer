using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Maps;

internal sealed class Navigation3DWindow : Window
{
    private readonly Func<VehicleTelemetry?> _localTelemetrySource;
    private readonly Func<OmsiMapInfo?> _activeMapSource;
    private readonly Viewport3D _viewport = new();
    private readonly PerspectiveCamera _camera = new();
    private readonly Model3DGroup _scene = new();
    private readonly ModelVisual3D _sceneVisual = new();
    private readonly Dictionary<string, ModelVisual3D> _remoteBuses =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _renderTimer;
    private readonly TextBlock _statusText;
    private readonly TextBlock _routeText;
    private readonly TextBlock _playersText;
    private readonly Button _followButton;
    private readonly Button _aerialButton;

    private ModelVisual3D? _localBus;
    private ModelVisual3D? _routeVisual;
    private OmsiMapLayout? _layout;
    private BitmapSource? _roadmapBitmap;
    private OmsiMapInfo? _activeMap;
    private string? _mapKey;
    private string? _routeKey;
    private double _planeWidth = 120d;
    private double _planeDepth = 80d;
    private double _cameraDistance = 34d;
    private bool _followVehicle = true;

    public Navigation3DWindow(
        Func<VehicleTelemetry?> localTelemetrySource,
        Func<OmsiMapInfo?> activeMapSource)
    {
        _localTelemetrySource = localTelemetrySource;
        _activeMapSource = activeMapSource;

        Title = T("Mapa 3D • NavBR", "3D Map • NavBR", "Mapa 3D • NavBR", "3D-Karte • NavBR", "Carte 3D • NavBR");
        Width = 1180d;
        Height = 760d;
        MinWidth = 860d;
        MinHeight = 560d;
        Background = new SolidColorBrush(Color.FromRgb(6, 12, 18));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _camera.FieldOfView = 52d;
        _camera.Position = new Point3D(0d, 72d, 62d);
        _camera.LookDirection = new Vector3D(0d, -56d, -62d);
        _camera.UpDirection = new Vector3D(0d, 1d, 0d);
        _viewport.Camera = _camera;
        _sceneVisual.Content = _scene;
        _viewport.Children.Add(_sceneVisual);

        _statusText = NewText(11d, FontWeights.SemiBold, Color.FromRgb(198, 213, 224));
        _routeText = NewText(11d, FontWeights.SemiBold, Color.FromRgb(255, 173, 73));
        _playersText = NewText(10.5d, FontWeights.Normal, Color.FromRgb(173, 190, 202));
        _playersText.TextWrapping = TextWrapping.Wrap;

        _followButton = NewButton(T("SEGUIR ÔNIBUS", "FOLLOW BUS", "SEGUIR AUTOBÚS", "BUS FOLGEN", "SUIVRE LE BUS"));
        _aerialButton = NewButton(T("VISÃO AÉREA", "AERIAL VIEW", "VISTA AÉREA", "DRAUFSICHT", "VUE AÉRIENNE"));
        _followButton.Click += (_, _) =>
        {
            _followVehicle = true;
            RefreshButtonState();
            RenderFrame();
        };
        _aerialButton.Click += (_, _) =>
        {
            _followVehicle = false;
            RefreshButtonState();
            RenderFrame();
        };
        RefreshButtonState();

        Content = BuildLayout();
        _viewport.MouseWheel += Viewport_MouseWheel;

        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _renderTimer.Tick += (_, _) => RenderFrame();
        Loaded += (_, _) =>
        {
            _renderTimer.Start();
            RenderFrame();
        };
        Closed += (_, _) => _renderTimer.Stop();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid
        {
            Margin = new Thickness(22d, 18d, 22d, 14d)
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = T("NAVEGAÇÃO 3D", "3D NAVIGATION", "NAVEGACIÓN 3D", "3D-NAVIGATION", "NAVIGATION 3D"),
            Foreground = Brushes.White,
            FontSize = 20d,
            FontWeight = FontWeights.Bold
        });
        title.Children.Add(new TextBlock
        {
            Text = T(
                "Mapa real do OMSI, rota ativa e ônibus da sessão online na mesma visão.",
                "Real OMSI map, active route and online-session buses in one view.",
                "Mapa real de OMSI, ruta activa y autobuses online en una sola vista.",
                "Reale OMSI-Karte, aktive Route und Online-Busse in einer Ansicht.",
                "Carte OMSI réelle, itinéraire actif et bus en ligne dans une seule vue."),
            Foreground = new SolidColorBrush(Color.FromRgb(137, 157, 171)),
            FontSize = 11d,
            Margin = new Thickness(0d, 4d, 0d, 0d)
        });
        Grid.SetColumn(title, 0);
        header.Children.Add(title);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        buttons.Children.Add(_followButton);
        buttons.Children.Add(_aerialButton);
        Grid.SetColumn(buttons, 1);
        header.Children.Add(buttons);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var viewportBorder = new Border
        {
            Margin = new Thickness(22d, 0d, 22d, 0d),
            Background = new SolidColorBrush(Color.FromRgb(4, 9, 14)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 49, 61)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(16d),
            ClipToBounds = true,
            Child = _viewport
        };
        Grid.SetRow(viewportBorder, 1);
        root.Children.Add(viewportBorder);

        var footer = new Grid
        {
            Margin = new Thickness(22d, 12d, 22d, 18d)
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(_routeText);
        left.Children.Add(_statusText);
        Grid.SetColumn(left, 0);
        footer.Children.Add(left);

        var right = new Border
        {
            Padding = new Thickness(12d, 9d, 12d, 9d),
            Background = new SolidColorBrush(Color.FromRgb(11, 20, 27)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(31, 48, 59)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Child = _playersText
        };
        Grid.SetColumn(right, 1);
        footer.Children.Add(right);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private void RenderFrame()
    {
        var map = _activeMapSource();
        var local = _localTelemetrySource();
        if (map is null || local is null)
        {
            _statusText.Text = T(
                "Aguardando mapa e telemetria do OMSI…",
                "Waiting for OMSI map and telemetry…",
                "Esperando mapa y telemetría de OMSI…",
                "Warte auf OMSI-Karte und Telemetrie…",
                "En attente de la carte et de la télémétrie OMSI…");
            _routeText.Text = string.Empty;
            return;
        }

        if (!EnsureMap(map))
        {
            _statusText.Text = T(
                "O mapa ativo ainda não possui roadmap/layout utilizável para o 3D.",
                "The active map does not yet have a usable roadmap/layout for 3D.",
                "El mapa activo aún no tiene roadmap/layout utilizable para 3D.",
                "Die aktive Karte hat noch keine nutzbare Roadmap/Layout für 3D.",
                "La carte active ne possède pas encore de roadmap/layout utilisable en 3D.");
            return;
        }

        EnsureRoute(map, local);
        var localPosition = UpdateLocalBus(local);
        var remotes = UpdateRemoteBuses(map);
        UpdateNavigationText(local);
        UpdateCamera(local, localPosition);
        UpdatePlayersText(remotes);
    }

    private bool EnsureMap(OmsiMapInfo map)
    {
        var key = $"{map.DirectoryPath}|{map.RoadmapPath}|{map.CompatibilityId}";
        if (string.Equals(key, _mapKey, StringComparison.OrdinalIgnoreCase))
        {
            return _layout is not null && _roadmapBitmap is not null;
        }

        _mapKey = key;
        _routeKey = null;
        _activeMap = map;
        _layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        _roadmapBitmap = TryLoadRoadmap(map.RoadmapPath);
        _remoteBuses.Clear();
        _localBus = null;
        _routeVisual = null;
        _viewport.Children.Clear();
        _scene.Children.Clear();
        _sceneVisual.Content = _scene;
        _viewport.Children.Add(_sceneVisual);

        if (_layout is null || _roadmapBitmap is null)
        {
            return false;
        }

        _planeWidth = 120d;
        _planeDepth = _planeWidth * _roadmapBitmap.PixelHeight / Math.Max(1d, _roadmapBitmap.PixelWidth);

        _scene.Children.Add(new AmbientLight(Color.FromRgb(155, 166, 178)));
        _scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.45d, -1d, -0.25d)));
        _scene.Children.Add(CreateMapPlane(_roadmapBitmap, _planeWidth, _planeDepth));
        return true;
    }

    private static BitmapSource? TryLoadRoadmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void EnsureRoute(OmsiMapInfo map, VehicleTelemetry telemetry)
    {
        var key = $"{map.DirectoryPath}|{telemetry.Line}|{telemetry.Route}|{telemetry.DestinationName}";
        if (string.Equals(key, _routeKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _routeKey = key;
        if (_routeVisual is not null)
        {
            _viewport.Children.Remove(_routeVisual);
            _routeVisual = null;
        }

        if (_layout is null || _roadmapBitmap is null)
        {
            return;
        }

        var target = !string.IsNullOrWhiteSpace(telemetry.Route)
            ? telemetry.Route
            : telemetry.DestinationName;
        var trace = OmsiRouteTraceReader.TryRead(map, _layout, target, telemetry.Line);
        if (trace.Count < 2)
        {
            return;
        }

        var points = new List<Point>(Math.Min(trace.Count, 500));
        var step = Math.Max(1, trace.Count / 450);
        for (var index = 0; index < trace.Count; index += step)
        {
            if (TryToScene(trace[index], out var x, out var z))
            {
                points.Add(new Point(x, z));
            }
        }
        if (trace.Count > 0 && TryToScene(trace[^1], out var lastX, out var lastZ))
        {
            points.Add(new Point(lastX, lastZ));
        }

        if (points.Count < 2)
        {
            return;
        }

        var routeGroup = new Model3DGroup();
        var routeBrush = new SolidColorBrush(Color.FromRgb(255, 155, 36));
        for (var index = 1; index < points.Count; index++)
        {
            var a = points[index - 1];
            var b = points[index];
            var dx = b.X - a.X;
            var dz = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dz * dz);
            if (length <= 0.02d)
            {
                continue;
            }

            var model = CreateBox(0.18d, 0.055d, length, routeBrush);
            var transforms = new Transform3DGroup();
            transforms.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0d, 1d, 0d), Math.Atan2(dx, dz) * 180d / Math.PI)));
            transforms.Children.Add(new TranslateTransform3D(
                (a.X + b.X) / 2d,
                0.10d,
                (a.Y + b.Y) / 2d));
            model.Transform = transforms;
            routeGroup.Children.Add(model);
        }

        _routeVisual = new ModelVisual3D { Content = routeGroup };
        _viewport.Children.Add(_routeVisual);
    }

    private Point? UpdateLocalBus(VehicleTelemetry telemetry)
    {
        if (!TryToScene(telemetry, out var x, out var z))
        {
            if (_localBus is not null)
            {
                _localBus.Content = null;
            }
            return null;
        }

        if (_localBus is null)
        {
            _localBus = CreateBusVisual(
                new SolidColorBrush(Color.FromRgb(255, 145, 35)),
                new SolidColorBrush(Color.FromRgb(255, 218, 170)));
            _viewport.Children.Add(_localBus);
        }
        else if (_localBus.Content is null)
        {
            _localBus.Content = CreateBusModel(
                new SolidColorBrush(Color.FromRgb(255, 145, 35)),
                new SolidColorBrush(Color.FromRgb(255, 218, 170)));
        }

        SetBusTransform(_localBus, x, z, telemetry.HeadingDegrees);
        return new Point(x, z);
    }

    private IReadOnlyList<Navigation3DRemoteVehicle> UpdateRemoteBuses(OmsiMapInfo localMap)
    {
        var snapshot = Navigation3DSessionFeed.Snapshot();
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var remote in snapshot)
        {
            var frame = remote.Frame;
            if (!IsCompatibleWithLocalMap(localMap, frame) ||
                !frame.Telemetry.IsInGame ||
                !TryToScene(frame.Telemetry, out var x, out var z))
            {
                continue;
            }

            visible.Add(frame.Player.PlayerId);
            if (!_remoteBuses.TryGetValue(frame.Player.PlayerId, out var visual))
            {
                visual = CreateBusVisual(
                    new SolidColorBrush(Color.FromRgb(44, 146, 255)),
                    new SolidColorBrush(Color.FromRgb(185, 224, 255)));
                _remoteBuses[frame.Player.PlayerId] = visual;
                _viewport.Children.Add(visual);
            }

            SetBusTransform(visual, x, z, frame.Telemetry.HeadingDegrees);
        }

        foreach (var playerId in _remoteBuses.Keys.Where(id => !visible.Contains(id)).ToArray())
        {
            _viewport.Children.Remove(_remoteBuses[playerId]);
            _remoteBuses.Remove(playerId);
        }

        return snapshot
            .Where(item => visible.Contains(item.Frame.Player.PlayerId))
            .ToArray();
    }

    private void UpdateNavigationText(VehicleTelemetry telemetry)
    {
        if (_layout is null || _activeMap is null)
        {
            return;
        }

        var target = !string.IsNullOrWhiteSpace(telemetry.Route)
            ? telemetry.Route
            : telemetry.DestinationName;
        var trace = OmsiRouteTraceReader.TryRead(_activeMap, _layout, target, telemetry.Line);
        var stops = OmsiBusStopReader.TryRead(_activeMap);
        var nav = NavBRNavigationEngine.Evaluate(telemetry, _layout, trace, stops);

        if (!nav.RouteAvailable)
        {
            _routeText.Text = T("ROTA NÃO RESOLVIDA", "ROUTE NOT RESOLVED", "RUTA NO RESUELTA", "ROUTE NICHT ERKANNT", "ITINÉRAIRE NON RÉSOLU");
            _statusText.Text = $"{_activeMap.DisplayName} • {telemetry.SpeedKph:0} km/h";
            return;
        }

        var nextStop = string.IsNullOrWhiteSpace(nav.NextStopName)
            ? string.Empty
            : $" • {nav.NextStopName}{(nav.DistanceToNextStopMeters is double stopDistance ? $" {FormatDistance(stopDistance)}" : string.Empty)}";
        _routeText.Text = $"{telemetry.Line ?? "—"} • {telemetry.DestinationName ?? telemetry.Route ?? "—"}{nextStop}";
        _statusText.Text = nav.IsOnRoute
            ? $"{nav.RouteProgressPercent:0}% • {FormatDistance(nav.DistanceRemainingMeters)} • {telemetry.SpeedKph:0} km/h"
            : $"{T("FORA DA ROTA", "OFF ROUTE", "FUERA DE RUTA", "ROUTE VERLASSEN", "HORS ITINÉRAIRE")} • {FormatDistance(nav.OffRouteDistanceMeters)}";
    }

    private void UpdateCamera(VehicleTelemetry telemetry, Point? localPosition)
    {
        if (!_followVehicle || localPosition is not Point position)
        {
            var height = Math.Max(72d, Math.Max(_planeWidth, _planeDepth) * 0.72d);
            _camera.Position = new Point3D(0d, height, 0.01d);
            _camera.LookDirection = new Vector3D(0d, -height, 0d);
            _camera.UpDirection = new Vector3D(0d, 0d, -1d);
            return;
        }

        var heading = telemetry.HeadingDegrees * Math.PI / 180d;
        var forwardX = Math.Sin(heading);
        var forwardZ = -Math.Cos(heading);
        var distance = Math.Clamp(_cameraDistance, 16d, 72d);
        var cameraX = position.X - forwardX * distance;
        var cameraZ = position.Y - forwardZ * distance;
        var cameraY = Math.Max(13d, distance * 0.58d);

        _camera.Position = new Point3D(cameraX, cameraY, cameraZ);
        _camera.LookDirection = new Vector3D(
            position.X - cameraX,
            0.7d - cameraY,
            position.Y - cameraZ);
        _camera.UpDirection = new Vector3D(0d, 1d, 0d);
    }

    private void UpdatePlayersText(IReadOnlyList<Navigation3DRemoteVehicle> remotes)
    {
        if (remotes.Count == 0)
        {
            _playersText.Text = T(
                "ONLINE • nenhum ônibus remoto compatível visível",
                "ONLINE • no compatible remote bus visible",
                "ONLINE • ningún autobús remoto compatible visible",
                "ONLINE • kein kompatibler Remote-Bus sichtbar",
                "EN LIGNE • aucun bus distant compatible visible");
            return;
        }

        var rows = remotes
            .Take(6)
            .Select(item => $"● {item.Frame.Player.DisplayName} • {item.Frame.Telemetry.Line ?? "—"} • {item.Frame.Telemetry.SpeedKph:0} km/h")
            .ToList();
        if (remotes.Count > rows.Count)
        {
            rows.Add($"+{remotes.Count - rows.Count}");
        }
        _playersText.Text = string.Join("   ", rows);
    }

    private bool IsCompatibleWithLocalMap(OmsiMapInfo localMap, NavBR.Shared.Multiplayer.PlayerTelemetryFrame frame)
    {
        var remoteMap = frame.Telemetry.MapName ?? frame.Player.MapName;
        if (!string.IsNullOrWhiteSpace(remoteMap) &&
            NormalizeMap(remoteMap) != NormalizeMap(localMap.DisplayName) &&
            NormalizeMap(remoteMap) != NormalizeMap(localMap.FolderName))
        {
            return false;
        }

        var remoteCompatibility = frame.Telemetry.MapCompatibilityId ?? frame.Player.MapCompatibilityId;
        return string.IsNullOrWhiteSpace(localMap.CompatibilityId) ||
               string.IsNullOrWhiteSpace(remoteCompatibility) ||
               string.Equals(localMap.CompatibilityId, remoteCompatibility, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryToScene(VehicleTelemetry telemetry, out double x, out double z)
    {
        x = 0d;
        z = 0d;
        return telemetry.GridX is int gridX &&
               telemetry.GridY is int gridY &&
               telemetry.TileX is double tileX &&
               telemetry.TileY is double tileY &&
               TryToScene(gridX, gridY, tileX, tileY, out x, out z);
    }

    private bool TryToScene(OmsiRouteTracePoint point, out double x, out double z) =>
        TryToScene(point.GridX, point.GridY, point.TileX, point.TileY, out x, out z);

    private bool TryToScene(
        int gridX,
        int gridY,
        double tileX,
        double tileY,
        out double x,
        out double z)
    {
        x = 0d;
        z = 0d;
        if (_layout is null || _roadmapBitmap is null ||
            !RoadmapTransform.TryToPixel(
                _layout,
                _roadmapBitmap.PixelWidth,
                _roadmapBitmap.PixelHeight,
                gridX,
                gridY,
                tileX,
                tileY,
                out var pixelX,
                out var pixelY))
        {
            return false;
        }

        x = (pixelX / _roadmapBitmap.PixelWidth - 0.5d) * _planeWidth;
        z = (pixelY / _roadmapBitmap.PixelHeight - 0.5d) * _planeDepth;
        return double.IsFinite(x) && double.IsFinite(z);
    }

    private static GeometryModel3D CreateMapPlane(BitmapSource bitmap, double width, double depth)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-width / 2d, 0d, -depth / 2d),
                new(width / 2d, 0d, -depth / 2d),
                new(width / 2d, 0d, depth / 2d),
                new(-width / 2d, 0d, depth / 2d)
            },
            TextureCoordinates = new PointCollection
            {
                new(0d, 0d), new(1d, 0d), new(1d, 1d), new(0d, 1d)
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        var material = new DiffuseMaterial(new ImageBrush(bitmap) { Stretch = Stretch.Fill });
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static ModelVisual3D CreateBusVisual(Brush body, Brush accent) =>
        new() { Content = CreateBusModel(body, accent) };

    private static Model3DGroup CreateBusModel(Brush body, Brush accent)
    {
        var group = new Model3DGroup();
        var main = CreateBox(1.8d, 0.85d, 4.6d, body);
        main.Transform = new TranslateTransform3D(0d, 0.62d, 0d);
        group.Children.Add(main);

        var roof = CreateBox(1.68d, 0.32d, 4.1d, accent);
        roof.Transform = new TranslateTransform3D(0d, 1.18d, 0.05d);
        group.Children.Add(roof);

        var front = CreateBox(1.55d, 0.52d, 0.12d, new SolidColorBrush(Color.FromRgb(28, 42, 54)));
        front.Transform = new TranslateTransform3D(0d, 0.86d, -2.33d);
        group.Children.Add(front);

        var rear = CreateBox(1.45d, 0.36d, 0.10d, new SolidColorBrush(Color.FromRgb(123, 20, 24)));
        rear.Transform = new TranslateTransform3D(0d, 0.64d, 2.34d);
        group.Children.Add(rear);

        var wheelBrush = new SolidColorBrush(Color.FromRgb(20, 24, 28));
        foreach (var wheel in new[]
        {
            new Vector3D(-0.92d, 0.32d, -1.35d),
            new Vector3D(0.92d, 0.32d, -1.35d),
            new Vector3D(-0.92d, 0.32d, 1.45d),
            new Vector3D(0.92d, 0.32d, 1.45d)
        })
        {
            var model = CreateBox(0.18d, 0.48d, 0.66d, wheelBrush);
            model.Transform = new TranslateTransform3D(wheel.X, wheel.Y, wheel.Z);
            group.Children.Add(model);
        }
        return group;
    }

    private static GeometryModel3D CreateBox(double width, double height, double depth, Brush brush)
    {
        var x = width / 2d;
        var y = height / 2d;
        var z = depth / 2d;
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-x, -y, -z), new(x, -y, -z), new(x, y, -z), new(-x, y, -z),
                new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z)
            },
            TriangleIndices = new Int32Collection
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                3,7,6, 3,6,2,
                0,4,7, 0,7,3,
                1,2,6, 1,6,5
            }
        };
        var material = new DiffuseMaterial(brush);
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static void SetBusTransform(ModelVisual3D bus, double x, double z, double headingDegrees)
    {
        var transforms = new Transform3DGroup();
        transforms.Children.Add(new RotateTransform3D(
            new AxisAngleRotation3D(new Vector3D(0d, 1d, 0d), headingDegrees)));
        transforms.Children.Add(new TranslateTransform3D(x, 0.14d, z));
        bus.Transform = transforms;
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_followVehicle)
        {
            return;
        }

        _cameraDistance = Math.Clamp(
            _cameraDistance * (e.Delta > 0 ? 0.88d : 1.14d),
            16d,
            72d);
        RenderFrame();
        e.Handled = true;
    }

    private void RefreshButtonState()
    {
        _followButton.Opacity = _followVehicle ? 1d : 0.58d;
        _aerialButton.Opacity = _followVehicle ? 0.58d : 1d;
    }

    private static TextBlock NewText(double size, FontWeight weight, Color color) => new()
    {
        Foreground = new SolidColorBrush(color),
        FontSize = size,
        FontWeight = weight,
        Margin = new Thickness(0d, 2d, 12d, 2d)
    };

    private static Button NewButton(string text) => new()
    {
        Content = text,
        Margin = new Thickness(8d, 0d, 0d, 0d),
        Padding = new Thickness(12d, 7d, 12d, 7d),
        MinWidth = 108d
    };

    private static string FormatDistance(double meters)
    {
        meters = Math.Max(0d, meters);
        return meters >= 1000d ? $"{meters / 1000d:0.0} km" : $"{Math.Round(meters / 10d) * 10d:0} m";
    }

    private static string NormalizeMap(string value) => new(
        value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
