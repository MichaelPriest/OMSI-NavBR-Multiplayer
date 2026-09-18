using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Ghost;

internal sealed class GhostReplayComparisonWindow : Window
{
    private readonly GhostReplayDocument _left;
    private readonly GhostReplayDocument _right;
    private readonly string _leftPath;
    private readonly string _rightPath;
    private readonly GhostReplayAnalytics _leftAnalytics;
    private readonly GhostReplayAnalytics _rightAnalytics;

    public GhostReplayComparisonWindow(
        GhostReplayDocument left,
        string leftPath,
        GhostReplayDocument right,
        string rightPath)
    {
        _left = left;
        _right = right;
        _leftPath = leftPath;
        _rightPath = rightPath;
        _leftAnalytics = GhostReplayAnalyticsCalculator.Analyze(left);
        _rightAnalytics = GhostReplayAnalyticsCalculator.Analyze(right);

        Title = "NavBR Ghost / Replay — Comparação";
        Width = 1080d;
        Height = 720d;
        MinWidth = 900d;
        MinHeight = 620d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        FontFamily = new FontFamily("Segoe UI");
        Icon = Application.Current?.MainWindow?.Icon;
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        heading.Children.Add(new TextBlock
        {
            Text = "Comparar replays",
            FontSize = 27d,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(218, 230, 238)
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Métricas calculadas somente a partir da telemetria gravada. Diferenças abaixo representam Replay B − Replay A.",
            Margin = new Thickness(0d, 6d, 0d, 0d),
            Foreground = Brush(151, 171, 185),
            FontSize = 11.5d,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var compatibility = BuildCompatibilityBanner();
        Grid.SetRow(compatibility, 1);
        root.Children.Add(compatibility);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0d, 16d, 0d, 0d)
        };
        var body = new StackPanel();
        var replays = new Grid();
        replays.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        replays.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        replays.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var leftCard = BuildReplayCard("REPLAY A", _left, _leftPath, _leftAnalytics);
        var rightCard = BuildReplayCard("REPLAY B", _right, _rightPath, _rightAnalytics);
        Grid.SetColumn(leftCard, 0);
        Grid.SetColumn(rightCard, 2);
        replays.Children.Add(leftCard);
        replays.Children.Add(rightCard);
        body.Children.Add(replays);

        var deltaCard = BuildDeltaCard();
        deltaCard.Margin = new Thickness(0d, 16d, 0d, 0d);
        body.Children.Add(deltaCard);

        scroller.Content = body;
        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);
        return root;
    }

    private Border BuildCompatibilityBanner()
    {
        var sameMap = SameValue(_left.Metadata.MapName, _right.Metadata.MapName);
        var sameVehicle = SameValue(_left.Metadata.VehicleName, _right.Metadata.VehicleName);
        var text = sameMap
            ? sameVehicle
                ? "Mesmo mapa e mesmo veículo identificados. A comparação é diretamente útil para análise da viagem."
                : "Mesmo mapa, mas veículos diferentes. Compare tempos e velocidades considerando a diferença de veículo."
            : "Os replays foram gravados em mapas diferentes. As métricas continuam reais, mas não representam viagens equivalentes.";

        return new Border
        {
            Background = sameMap ? Brush(11, 37, 31) : Brush(38, 31, 16),
            BorderBrush = sameMap ? Brush(56, 201, 140) : Brush(242, 184, 75),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Padding = new Thickness(14d),
            Child = new TextBlock
            {
                Text = text,
                Foreground = sameMap ? Brush(114, 224, 176) : Brush(242, 184, 75),
                FontSize = 11.5d,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Border BuildReplayCard(
        string label,
        GhostReplayDocument document,
        string path,
        GhostReplayAnalytics analytics)
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(113, 198, 255),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        body.Children.Add(new TextBlock
        {
            Text = document.Metadata.Name,
            Foreground = Brush(218, 230, 238),
            FontSize = 18d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 6d, 0d, 2d),
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = path
        });
        body.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(path),
            Foreground = Brush(151, 171, 185),
            FontSize = 10d,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = path
        });

        body.Children.Add(Metric("MAPA", Safe(document.Metadata.MapName)));
        body.Children.Add(Metric("VEÍCULO", Safe(document.Metadata.VehicleName)));
        body.Children.Add(Metric("DURAÇÃO", FormatDuration(analytics.DurationSeconds)));
        body.Children.Add(Metric("DISTÂNCIA EST.", FormatDistance(analytics.EstimatedDistanceKm)));
        body.Children.Add(Metric("VELOCIDADE MÉDIA", $"{analytics.AverageSpeedKph:0.0} km/h"));
        body.Children.Add(Metric("VELOCIDADE MÁXIMA", $"{analytics.MaximumSpeedKph:0.0} km/h"));
        body.Children.Add(Metric("FRAMES", document.Metadata.FrameCount.ToString("N0")));

        return new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(18d),
            Child = body
        };
    }

    private Border BuildDeltaCard()
    {
        var grid = new Grid();
        for (var column = 0; column < 4; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        }

        AddDelta(
            grid,
            0,
            "DURAÇÃO B − A",
            FormatSignedSeconds(_rightAnalytics.DurationSeconds - _leftAnalytics.DurationSeconds));
        AddDelta(
            grid,
            1,
            "DISTÂNCIA B − A",
            FormatSignedDistance(_rightAnalytics.EstimatedDistanceKm - _leftAnalytics.EstimatedDistanceKm));
        AddDelta(
            grid,
            2,
            "MÉDIA B − A",
            FormatSignedSpeed(_rightAnalytics.AverageSpeedKph - _leftAnalytics.AverageSpeedKph));
        AddDelta(
            grid,
            3,
            "MÁXIMA B − A",
            FormatSignedSpeed(_rightAnalytics.MaximumSpeedKph - _leftAnalytics.MaximumSpeedKph));

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "DIFERENÇAS",
            Foreground = Brush(151, 171, 185),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 12d)
        });
        body.Children.Add(grid);
        body.Children.Add(new TextBlock
        {
            Text = "A distância é estimada por integração temporal da velocidade registrada entre frames válidos. Nenhuma métrica é inferida de dados externos.",
            Foreground = Brush(151, 171, 185),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 14d, 0d, 0d)
        });

        return new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(18d),
            Child = body
        };
    }

    private static UIElement Metric(string label, string value)
    {
        var row = new Grid { Margin = new Thickness(0d, 12d, 0d, 0d) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(151, 171, 185),
            FontSize = 9.5d,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var valueText = new TextBlock
        {
            Text = value,
            Foreground = Brush(218, 230, 238),
            FontSize = 12d,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            ToolTip = value
        };
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);
        return row;
    }

    private static void AddDelta(Grid grid, int column, string label, string value)
    {
        var stack = new StackPanel { Margin = new Thickness(column == 0 ? 0d : 12d, 0d, 12d, 0d) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brush(151, 171, 185),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brush(218, 230, 238),
            FontSize = 17d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        });
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private static bool SameValue(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        return duration.TotalHours >= 1d
            ? duration.ToString("hh\\:mm\\:ss")
            : duration.ToString("mm\\:ss");
    }

    private static string FormatDistance(double distanceKm) =>
        distanceKm < 10d ? $"{distanceKm:0.00} km" : $"{distanceKm:0.0} km";

    private static string FormatSignedDistance(double value) =>
        $"{value:+0.00;-0.00;0.00} km";

    private static string FormatSignedSpeed(double value) =>
        $"{value:+0.0;-0.0;0.0} km/h";

    private static string FormatSignedSeconds(double value)
    {
        var sign = value > 0d ? "+" : value < 0d ? "−" : string.Empty;
        var duration = TimeSpan.FromSeconds(Math.Abs(value));
        var formatted = duration.TotalHours >= 1d
            ? duration.ToString("hh\\:mm\\:ss")
            : duration.ToString("mm\\:ss");
        return sign + formatted;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
