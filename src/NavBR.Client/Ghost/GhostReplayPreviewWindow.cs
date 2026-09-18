using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NavBR.Client.Ghost;

internal sealed class GhostReplayPreviewWindow : Window
{
    private const double CanvasWidth = 1000d;
    private const double CanvasHeight = 620d;
    private const double Padding = 42d;

    private readonly GhostReplayDocument _document;
    private readonly string _sourcePath;

    public GhostReplayPreviewWindow(GhostReplayDocument document, string sourcePath)
    {
        _document = document;
        _sourcePath = sourcePath;

        Title = "NavBR Ghost / Replay — Pré-visualização";
        Width = 1120d;
        Height = 860d;
        MinWidth = 860d;
        MinHeight = 660d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        FontFamily = new FontFamily("Segoe UI");
        Icon = Application.Current?.MainWindow?.Icon;
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new Grid { Margin = new Thickness(0d, 0d, 0d, 16d) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingText = new StackPanel();
        headingText.Children.Add(new TextBlock
        {
            Text = _document.Metadata.Name,
            FontSize = 24d,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(218, 230, 238),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        headingText.Children.Add(new TextBlock
        {
            Text = "Pré-visualização local do trajeto gravado • somente leitura • sem comandos para o OMSI",
            Margin = new Thickness(0d, 5d, 16d, 0d),
            Foreground = Brush(151, 171, 185),
            FontSize = 11.5d
        });
        heading.Children.Add(headingText);
        var localPlayback = new Button
        {
            Content = "Reproduzir no mapa",
            Height = 38d,
            MinWidth = 152d,
            Padding = new Thickness(14d, 6d, 14d, 6d),
            Background = Brush(61, 137, 196),
            Foreground = Brushes.White,
            BorderBrush = Brush(113, 198, 255),
            BorderThickness = new Thickness(1d),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        localPlayback.Click += OpenLocalPlayback_Click;
        Grid.SetColumn(localPlayback, 1);
        heading.Children.Add(localPlayback);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var metadata = BuildMetadataCard();
        Grid.SetRow(metadata, 1);
        root.Children.Add(metadata);

        var routeCard = new Border
        {
            Margin = new Thickness(0d, 16d, 0d, 0d),
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(14d)
        };

        var container = new Grid();
        var canvas = new Canvas
        {
            Width = CanvasWidth,
            Height = CanvasHeight,
            Background = Brush(7, 18, 28),
            ClipToBounds = true
        };
        RenderRoute(canvas);
        container.Children.Add(new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            Child = canvas
        });
        routeCard.Child = container;
        Grid.SetRow(routeCard, 2);
        root.Children.Add(routeCard);

        return root;
    }

    private void OpenLocalPlayback_Click(object sender, RoutedEventArgs e)
    {
        var playback = new GhostReplayMapPlaybackWindow(_document)
        {
            Owner = this
        };
        playback.ShowDialog();
    }

    private Border BuildMetadataCard()
    {
        var grid = new Grid();
        for (var row = 0; row < 2; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (var column = 0; column < 4; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        }

        var analytics = GhostReplayAnalyticsCalculator.Analyze(_document);
        var map = string.IsNullOrWhiteSpace(_document.Metadata.MapName) ? "—" : _document.Metadata.MapName;
        var vehicle = string.IsNullOrWhiteSpace(_document.Metadata.VehicleName) ? "—" : _document.Metadata.VehicleName;
        var duration = TimeSpan.FromSeconds(Math.Max(0d, analytics.DurationSeconds));
        var line = _document.Frames
            .Select(frame => frame.Telemetry.Line)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "—";

        AddMetric(grid, 0, 0, "MAPA", map);
        AddMetric(grid, 0, 1, "VEÍCULO", vehicle);
        AddMetric(grid, 0, 2, "DURAÇÃO", FormatDuration(duration));
        AddMetric(grid, 0, 3, "FRAMES", _document.Metadata.FrameCount.ToString("N0"));
        AddMetric(grid, 1, 0, "DISTÂNCIA EST.", FormatDistance(analytics.EstimatedDistanceKm));
        AddMetric(grid, 1, 1, "VEL. MÉDIA", $"{analytics.AverageSpeedKph:0.0} km/h");
        AddMetric(grid, 1, 2, "VEL. MÁXIMA", $"{analytics.MaximumSpeedKph:0.0} km/h");
        AddMetric(grid, 1, 3, "LINHA", line);

        return new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(16d),
            ToolTip = $"{_sourcePath}\nDistância estimada pela integração da velocidade registrada entre os frames válidos.",
            Child = grid
        };
    }

    private static void AddMetric(Grid grid, int row, int column, string label, string value)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(
                column == 0 ? 0d : 12d,
                row == 0 ? 0d : 14d,
                12d,
                0d)
        };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(151, 171, 185),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Margin = new Thickness(0d, 5d, 0d, 0d),
            Foreground = Brush(218, 230, 238),
            FontSize = 13d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = value
        });
        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private void RenderRoute(Canvas canvas)
    {
        var frames = _document.Frames
            .Where(frame =>
                double.IsFinite(frame.Telemetry.X) &&
                double.IsFinite(frame.Telemetry.Z))
            .ToArray();

        if (frames.Length < 2)
        {
            AddEmptyMessage(canvas, "Replay sem coordenadas suficientes para desenhar o trajeto.");
            return;
        }

        var minX = frames.Min(frame => frame.Telemetry.X);
        var maxX = frames.Max(frame => frame.Telemetry.X);
        var minZ = frames.Min(frame => frame.Telemetry.Z);
        var maxZ = frames.Max(frame => frame.Telemetry.Z);
        var spanX = Math.Max(1d, maxX - minX);
        var spanZ = Math.Max(1d, maxZ - minZ);

        var drawableWidth = CanvasWidth - (Padding * 2d);
        var drawableHeight = CanvasHeight - (Padding * 2d);
        var scale = Math.Min(drawableWidth / spanX, drawableHeight / spanZ);
        var routeWidth = spanX * scale;
        var routeHeight = spanZ * scale;
        var offsetX = (CanvasWidth - routeWidth) / 2d;
        var offsetY = (CanvasHeight - routeHeight) / 2d;

        var sampleStep = Math.Max(1, frames.Length / 4000);
        var points = new PointCollection();
        for (var index = 0; index < frames.Length; index += sampleStep)
        {
            points.Add(ToCanvas(frames[index].Telemetry.X, frames[index].Telemetry.Z));
        }
        if ((frames.Length - 1) % sampleStep != 0)
        {
            points.Add(ToCanvas(frames[^1].Telemetry.X, frames[^1].Telemetry.Z));
        }

        canvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = Brush(113, 198, 255),
            StrokeThickness = 5d,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });

        AddMarker(canvas, ToCanvas(frames[0].Telemetry.X, frames[0].Telemetry.Z), "INÍCIO", Brush(56, 201, 140));
        AddMarker(canvas, ToCanvas(frames[^1].Telemetry.X, frames[^1].Telemetry.Z), "FIM", Brush(239, 91, 100));

        var note = new TextBlock
        {
            Text = "Trajeto relativo pelas coordenadas absolutas registradas pelo OMSI",
            Foreground = Brush(151, 171, 185),
            FontSize = 10d
        };
        Canvas.SetLeft(note, 16d);
        Canvas.SetBottom(note, 12d);
        canvas.Children.Add(note);
        return;

        Point ToCanvas(double x, double z)
        {
            var px = offsetX + ((x - minX) * scale);
            var py = CanvasHeight - (offsetY + ((z - minZ) * scale));
            return new Point(px, py);
        }
    }

    private static void AddMarker(Canvas canvas, Point point, string label, Brush brush)
    {
        const double size = 16d;
        var marker = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 2d
        };
        Canvas.SetLeft(marker, point.X - (size / 2d));
        Canvas.SetTop(marker, point.Y - (size / 2d));
        canvas.Children.Add(marker);

        var text = new TextBlock
        {
            Text = label,
            Foreground = brush,
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(text, point.X + 10d);
        Canvas.SetTop(text, Math.Max(4d, point.Y - 18d));
        canvas.Children.Add(text);
    }

    private static void AddEmptyMessage(Canvas canvas, string message)
    {
        var text = new TextBlock
        {
            Text = message,
            Foreground = Brush(151, 171, 185),
            FontSize = 15d,
            TextWrapping = TextWrapping.Wrap,
            Width = 520d,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(text, (CanvasWidth - text.Width) / 2d);
        Canvas.SetTop(text, (CanvasHeight / 2d) - 20d);
        canvas.Children.Add(text);
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1d
            ? duration.ToString("hh\\:mm\\:ss")
            : duration.ToString("mm\\:ss");

    private static string FormatDistance(double distanceKm) =>
        distanceKm < 10d
            ? $"{distanceKm:0.00} km"
            : $"{distanceKm:0.0} km";

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
