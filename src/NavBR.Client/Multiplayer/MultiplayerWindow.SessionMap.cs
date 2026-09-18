using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Canvas? _liveSessionCanvas;
    private TextBlock? _liveSessionHint;
    private DispatcherTimer? _liveSessionTimer;
    private bool _liveSessionInstalled;

    [ModuleInitializer]
    internal static void InitializeLiveSessionMapBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(LiveSessionWindowLoaded));
    }

    private static void LiveSessionWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallLiveSessionView),
            DispatcherPriority.ContextIdle);
    }

    private void InstallLiveSessionView()
    {
        if (_liveSessionInstalled)
        {
            return;
        }

        _liveSessionCanvas = LiveSessionCanvas;
        _liveSessionHint = LiveSessionHintText;

        if (_liveSessionCanvas is null)
        {
            return;
        }

        _liveSessionInstalled = true;
        _liveSessionCanvas.Children.Clear();
        _liveSessionCanvas.ClipToBounds = true;
        _liveSessionCanvas.Background = Brushes.Transparent;

        _liveSessionHint.Text = LiveSessionText(
            "Posições exibidas somente quando existe telemetria real e compatível. Sem dados suficientes, nenhum ônibus ou personagem é inventado.",
            "Positions are shown only when real, compatible telemetry is available. No buses or characters are invented when data is insufficient.",
            "Las posiciones se muestran solo con telemetría real y compatible. No se inventan autobuses ni personajes cuando faltan datos.",
            "Positionen werden nur mit echten, kompatiblen Telemetriedaten angezeigt. Bei fehlenden Daten werden keine Busse oder Charaktere erfunden.",
            "Les positions ne sont affichées qu’avec une télémétrie réelle et compatible. Aucun bus ou personnage n’est inventé si les données manquent.");

        _liveSessionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500d)
        };
        _liveSessionTimer.Tick += LiveSessionTimer_Tick;
        _liveSessionTimer.Start();

        _liveSessionCanvas.SizeChanged += LiveSessionCanvas_SizeChanged;
        Closed += LiveSessionWindowClosed;
        RenderLiveSessionView();
    }

    private void LiveSessionTimer_Tick(object? sender, EventArgs e) => RenderLiveSessionView();

    private void LiveSessionCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderLiveSessionView();

    private void LiveSessionWindowClosed(object? sender, EventArgs e)
    {
        if (_liveSessionTimer is not null)
        {
            _liveSessionTimer.Stop();
            _liveSessionTimer.Tick -= LiveSessionTimer_Tick;
            _liveSessionTimer = null;
        }

        if (_liveSessionCanvas is not null)
        {
            _liveSessionCanvas.SizeChanged -= LiveSessionCanvas_SizeChanged;
        }
        Closed -= LiveSessionWindowClosed;
    }

    private void RenderLiveSessionView()
    {
        var canvas = _liveSessionCanvas;
        if (canvas is null)
        {
            return;
        }

        canvas.Children.Clear();

        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width < 80d || height < 70d)
        {
            return;
        }

        DrawSessionGrid(canvas, width, height);

        var local = _telemetrySource();
        var activeMap = _activeMapSource();
        var points = new List<LiveSessionPoint>();
        var now = DateTimeOffset.UtcNow;

        if (_localRoleplayCharacter is { IsActive: true } localRoleplay &&
            now - localRoleplay.Timestamp <= TimeSpan.FromSeconds(3d) &&
            IsRoleplaySessionCompatible(local, activeMap?.CompatibilityId, localRoleplay))
        {
            points.Add(CreateRoleplaySessionPoint(
                LiveSessionText("Você", "You", "Tú", "Sie", "Vous"),
                localRoleplay,
                isLocal: true));
        }
        else if (local is not null &&
                 local.IsInGame &&
                 TryGetSessionCoordinates(local, out var localX, out var localY))
        {
            points.Add(CreateVehicleSessionPoint(
                LiveSessionText("Você", "You", "Tú", "Sie", "Vous"),
                local,
                localX,
                localY,
                isLocal: true));
        }

        foreach (var item in _remoteTelemetry)
        {
            if (_remoteRoleplayCharacters.TryGetValue(item.Key, out var rpFrame) &&
                rpFrame.Character.IsActive &&
                now - rpFrame.Character.Timestamp <= TimeSpan.FromSeconds(3d) &&
                IsRoleplaySessionCompatible(local, activeMap?.CompatibilityId, rpFrame.Character))
            {
                var rpName = _players.TryGetValue(item.Key, out var rpPlayer)
                    ? rpPlayer.DisplayName
                    : rpFrame.Player.DisplayName;
                points.Add(CreateRoleplaySessionPoint(
                    rpName,
                    rpFrame.Character,
                    isLocal: false));
                continue;
            }

            var telemetry = item.Value;
            if (!telemetry.IsInGame ||
                now - telemetry.Timestamp > TimeSpan.FromSeconds(3d) ||
                !IsSessionTelemetryCompatible(local, activeMap?.CompatibilityId, item.Key, telemetry) ||
                !TryGetSessionCoordinates(telemetry, out var x, out var y))
            {
                continue;
            }

            var displayName = _players.TryGetValue(item.Key, out var player)
                ? player.DisplayName
                : item.Key;
            points.Add(CreateVehicleSessionPoint(
                displayName,
                telemetry,
                x,
                y,
                isLocal: false));
        }

        foreach (var item in _remoteRoleplayCharacters)
        {
            if (_remoteTelemetry.ContainsKey(item.Key))
            {
                continue;
            }

            var frame = item.Value;
            if (!frame.Character.IsActive ||
                now - frame.Character.Timestamp > TimeSpan.FromSeconds(3d) ||
                !IsRoleplaySessionCompatible(local, activeMap?.CompatibilityId, frame.Character))
            {
                continue;
            }

            var displayName = _players.TryGetValue(item.Key, out var player)
                ? player.DisplayName
                : frame.Player.DisplayName;
            points.Add(CreateRoleplaySessionPoint(
                displayName,
                frame.Character,
                isLocal: false));
        }

        if (points.Count == 0)
        {
            DrawSessionEmptyState(canvas, width, height);
            UpdateLiveSessionHint(local, 0);
            return;
        }

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var spanX = Math.Max(60d, maxX - minX);
        var spanY = Math.Max(60d, maxY - minY);
        var pad = 32d;
        var usableWidth = Math.Max(20d, width - pad * 2d);
        var usableHeight = Math.Max(20d, height - pad * 2d);
        var scale = Math.Min(usableWidth / spanX, usableHeight / spanY);
        scale = Math.Min(scale, 2.2d);

        var centerX = (minX + maxX) / 2d;
        var centerY = (minY + maxY) / 2d;

        foreach (var point in points.OrderBy(point => point.IsLocal ? 1 : 0))
        {
            var px = width / 2d + (point.X - centerX) * scale;
            var py = height / 2d - (point.Y - centerY) * scale;
            DrawSessionPoint(canvas, point, px, py);
        }

        UpdateLiveSessionHint(local, points.Count(point => !point.IsLocal));
    }

    private bool IsSessionTelemetryCompatible(
        VehicleTelemetry? local,
        string? activeMapCompatibilityId,
        string playerId,
        VehicleTelemetry remote)
    {
        if (local is not null &&
            !string.IsNullOrWhiteSpace(local.MapName) &&
            !string.IsNullOrWhiteSpace(remote.MapName) &&
            !string.Equals(local.MapName, remote.MapName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeMapCompatibilityId) ||
            !_players.TryGetValue(playerId, out var player) ||
            string.IsNullOrWhiteSpace(player.MapCompatibilityId))
        {
            return true;
        }

        return string.Equals(
            activeMapCompatibilityId,
            player.MapCompatibilityId,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSessionCoordinates(VehicleTelemetry telemetry, out double x, out double y)
    {
        if (telemetry.LocalX is double localX &&
            telemetry.LocalY is double localY &&
            double.IsFinite(localX) &&
            double.IsFinite(localY))
        {
            x = localX;
            y = localY;
            return true;
        }

        x = telemetry.X;
        y = telemetry.Y;
        return double.IsFinite(x) && double.IsFinite(y);
    }

    private static bool IsRoleplaySessionCompatible(
        VehicleTelemetry? local,
        string? activeMapCompatibilityId,
        RoleplayCharacterState roleplay)
    {
        if (local is not null &&
            !string.IsNullOrWhiteSpace(local.MapName) &&
            !string.IsNullOrWhiteSpace(roleplay.MapName) &&
            !string.Equals(local.MapName, roleplay.MapName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(activeMapCompatibilityId) ||
            string.IsNullOrWhiteSpace(roleplay.MapCompatibilityId))
        {
            return true;
        }

        return string.Equals(
            activeMapCompatibilityId,
            roleplay.MapCompatibilityId,
            StringComparison.OrdinalIgnoreCase);
    }

    private static LiveSessionPoint CreateVehicleSessionPoint(
        string displayName,
        VehicleTelemetry telemetry,
        double x,
        double y,
        bool isLocal)
    {
        var line = string.IsNullOrWhiteSpace(telemetry.Line)
            ? string.Empty
            : telemetry.Line.Trim();
        var detail = string.IsNullOrWhiteSpace(line)
            ? $"{telemetry.SpeedKph:F0} km/h"
            : $"{line} • {telemetry.SpeedKph:F0} km/h";

        return new LiveSessionPoint(
            displayName,
            x,
            y,
            telemetry.HeadingDegrees,
            detail,
            isLocal,
            IsRoleplay: false);
    }

    private static LiveSessionPoint CreateRoleplaySessionPoint(
        string displayName,
        RoleplayCharacterState roleplay,
        bool isLocal)
    {
        var activity = roleplay.Activity switch
        {
            RoleplayCharacterActivity.Running => LiveSessionText(
                "RP • correndo",
                "RP • running",
                "RP • corriendo",
                "RP • läuft",
                "RP • course"),
            RoleplayCharacterActivity.Walking => LiveSessionText(
                "RP • a pé",
                "RP • on foot",
                "RP • a pie",
                "RP • zu Fuß",
                "RP • à pied"),
            _ => LiveSessionText(
                "RP • parado",
                "RP • idle",
                "RP • quieto",
                "RP • steht",
                "RP • immobile")
        };

        return new LiveSessionPoint(
            displayName,
            roleplay.LocalX,
            roleplay.LocalY,
            roleplay.HeadingDegrees,
            $"{activity} • {roleplay.SpeedMps:F1} m/s",
            isLocal,
            IsRoleplay: true);
    }

    private static void DrawSessionGrid(Canvas canvas, double width, double height)
    {
        var gridBrush = new SolidColorBrush(Color.FromArgb(42, 92, 143, 178));
        for (var i = 1; i < 4; i++)
        {
            var x = width * i / 4d;
            canvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0d,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 1d
            });
        }

        for (var i = 1; i < 3; i++)
        {
            var y = height * i / 3d;
            canvas.Children.Add(new Line
            {
                X1 = 0d,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1d
            });
        }
    }

    private static void DrawSessionPoint(Canvas canvas, LiveSessionPoint point, double x, double y)
    {
        const double size = 30d;
        var markerColor = point.IsLocal
            ? Color.FromRgb(56, 201, 140)
            : point.IsRoleplay
                ? Color.FromRgb(186, 132, 255)
                : Color.FromRgb(113, 198, 255);

        var marker = new Grid
        {
            Width = size,
            Height = size,
            RenderTransformOrigin = new Point(0.5d, 0.5d),
            ToolTip = $"{point.DisplayName} • {point.Detail}"
        };

        marker.Children.Add(new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromArgb(215, 0, 0, 0)),
            Stroke = Brushes.White,
            StrokeThickness = 2d
        });
        marker.Children.Add(new Ellipse
        {
            Width = 22d,
            Height = 22d,
            Fill = new SolidColorBrush(markerColor)
        });

        if (point.IsRoleplay)
        {
            marker.Children.Add(new TextBlock
            {
                Text = "♙",
                Foreground = Brushes.White,
                FontSize = 16d,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            var pointer = new Polygon
            {
                Points = new PointCollection
                {
                    new(15d, 3d),
                    new(21d, 23d),
                    new(15d, 19d),
                    new(9d, 23d)
                },
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(24, 34, 42)),
                StrokeThickness = 1d,
                RenderTransformOrigin = new Point(0.5d, 0.5d),
                RenderTransform = new RotateTransform(point.HeadingDegrees)
            };
            marker.Children.Add(pointer);
        }

        Canvas.SetLeft(marker, x - size / 2d);
        Canvas.SetTop(marker, y - size / 2d);
        Panel.SetZIndex(marker, 10);
        canvas.Children.Add(marker);

        var nameText = new TextBlock
        {
            Text = point.DisplayName,
            Foreground = Brushes.White,
            FontSize = 9.5d,
            FontWeight = point.IsLocal ? FontWeights.Bold : FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120d,
            TextAlignment = TextAlignment.Center
        };
        var namePlate = new Border
        {
            Padding = new Thickness(6d, 2d, 6d, 2d),
            Background = new SolidColorBrush(Color.FromArgb(210, 4, 15, 23)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(185, markerColor.R, markerColor.G, markerColor.B)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(5d),
            Child = nameText
        };
        namePlate.Measure(new Size(130d, 40d));
        Canvas.SetLeft(
            namePlate,
            Math.Clamp(
                x - namePlate.DesiredSize.Width / 2d,
                2d,
                Math.Max(2d, canvas.ActualWidth - namePlate.DesiredSize.Width - 2d)));
        Canvas.SetTop(
            namePlate,
            Math.Max(2d, y - size / 2d - namePlate.DesiredSize.Height - 5d));
        Panel.SetZIndex(namePlate, 12);
        canvas.Children.Add(namePlate);

        var detail = new TextBlock
        {
            Text = point.Detail,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 205, 215)),
            FontSize = 8.5d,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(165, 4, 15, 23)),
            Padding = new Thickness(4d, 1d, 4d, 1d)
        };
        detail.Measure(new Size(150d, 32d));
        Canvas.SetLeft(
            detail,
            Math.Clamp(
                x - detail.DesiredSize.Width / 2d,
                2d,
                Math.Max(2d, canvas.ActualWidth - detail.DesiredSize.Width - 2d)));
        Canvas.SetTop(
            detail,
            Math.Min(
                canvas.ActualHeight - detail.DesiredSize.Height - 2d,
                y + size / 2d + 4d));
        Panel.SetZIndex(detail, 11);
        canvas.Children.Add(detail);
    }

    private static void DrawSessionEmptyState(Canvas canvas, double width, double height)
    {
        var stack = new StackPanel
        {
            Width = Math.Min(330d, Math.Max(190d, width - 40d))
        };
        stack.Children.Add(new TextBlock
        {
            Text = "◎",
            Foreground = new SolidColorBrush(Color.FromRgb(83, 153, 199)),
            FontSize = 27d,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = LiveSessionText(
                "Aguardando telemetria compatível",
                "Waiting for compatible telemetry",
                "Esperando telemetría compatible",
                "Warte auf kompatible Telemetrie",
                "En attente d’une télémétrie compatible"),
            Foreground = new SolidColorBrush(Color.FromRgb(181, 204, 219)),
            FontSize = 11d,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        });

        Canvas.SetLeft(stack, Math.Max(0d, (width - stack.Width) / 2d));
        Canvas.SetTop(stack, Math.Max(0d, height / 2d - 38d));
        canvas.Children.Add(stack);
    }

    private void UpdateLiveSessionHint(VehicleTelemetry? local, int remoteCount)
    {
        if (_liveSessionHint is null)
        {
            return;
        }

        if (local is null || !local.IsInGame)
        {
            _liveSessionHint.Text = LiveSessionText(
                "Abra uma viagem no OMSI para posicionar seu ônibus. Motoristas remotos só aparecem com mapa e telemetria compatíveis.",
                "Start a trip in OMSI to position your bus. Remote drivers appear only with compatible map and telemetry.",
                "Inicia un viaje en OMSI para posicionar tu autobús. Los conductores remotos solo aparecen con mapa y telemetría compatibles.",
                "Starten Sie eine Fahrt in OMSI, um Ihren Bus zu positionieren. Andere Fahrer erscheinen nur bei kompatibler Karte und Telemetrie.",
                "Démarrez un trajet dans OMSI pour positionner votre bus. Les conducteurs distants n’apparaissent qu’avec une carte et une télémétrie compatibles.");
            return;
        }

        var map = string.IsNullOrWhiteSpace(local.MapName) ? "OMSI" : local.MapName.Trim();
        _liveSessionHint.Text = remoteCount switch
        {
            0 => LiveSessionText(
                $"{map} • seu ônibus está sendo acompanhado • nenhum motorista remoto compatível agora",
                $"{map} • your bus is being tracked • no compatible remote drivers right now",
                $"{map} • tu autobús está siendo seguido • no hay conductores remotos compatibles ahora",
                $"{map} • Ihr Bus wird verfolgt • derzeit keine kompatiblen entfernten Fahrer",
                $"{map} • votre bus est suivi • aucun conducteur distant compatible actuellement"),
            1 => LiveSessionText(
                $"{map} • 1 motorista remoto com telemetria compatível",
                $"{map} • 1 remote driver with compatible telemetry",
                $"{map} • 1 conductor remoto con telemetría compatible",
                $"{map} • 1 entfernter Fahrer mit kompatibler Telemetrie",
                $"{map} • 1 conducteur distant avec télémétrie compatible"),
            _ => LiveSessionText(
                $"{map} • {remoteCount} motoristas remotos com telemetria compatível",
                $"{map} • {remoteCount} remote drivers with compatible telemetry",
                $"{map} • {remoteCount} conductores remotos con telemetría compatible",
                $"{map} • {remoteCount} entfernte Fahrer mit kompatibler Telemetrie",
                $"{map} • {remoteCount} conducteurs distants avec télémétrie compatible")
        };
    }

    private static IEnumerable<T> FindLiveSessionChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindLiveSessionChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string LiveSessionText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private sealed record LiveSessionPoint(
        string DisplayName,
        double X,
        double Y,
        double HeadingDegrees,
        string Detail,
        bool IsLocal,
        bool IsRoleplay);
}
