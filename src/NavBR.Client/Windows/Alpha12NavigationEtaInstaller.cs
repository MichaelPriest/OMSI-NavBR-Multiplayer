using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;

namespace NavBR.Client.Windows;

/// <summary>
/// Adds ETA values to the Alpha.12 route rail. ETAs are learned only from real
/// progress along the resolved OMSI route; until enough recent samples exist the
/// UI remains empty instead of inventing an arrival time.
/// </summary>
internal static class Alpha12NavigationEtaInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var body = Enumerate<StackPanel>(window)
            .FirstOrDefault(panel =>
                panel.Children.OfType<TextBlock>().FirstOrDefault()?.Text == "ROTA ATIVA");
        if (body is null)
        {
            Installed.Remove(window);
            return;
        }

        var nextStopEta = Value();
        var routeEndEta = Value();
        var row = TwoColumn(
            DataBlock(T("ETA próxima parada", "Next stop ETA", "ETA próxima parada", "ETA nächste Haltestelle", "ETA prochain arrêt"), nextStopEta),
            DataBlock(T("ETA terminal", "Terminal ETA", "ETA terminal", "ETA Endhaltestelle", "ETA terminus"), routeEndEta));
        row.Tag = "alpha12-navigation-eta";
        row.Margin = new Thickness(0d, 12d, 0d, 0d);
        body.Children.Add(row);

        var note = Text(
            T(
                "Estimativa adaptativa baseada somente no progresso real da rota; sem amostras suficientes ou recentes, o NavBR mostra —.",
                "Adaptive estimate based only on real route progress; without enough recent samples NavBR shows —.",
                "Estimación adaptativa basada solo en el progreso real de la ruta; sin muestras recientes suficientes NavBR muestra —.",
                "Adaptive Schätzung nur aus echtem Routenfortschritt; ohne genügend aktuelle Messwerte zeigt NavBR —.",
                "Estimation adaptative fondée uniquement sur la progression réelle ; sans échantillons récents suffisants, NavBR affiche —."),
            9.5d,
            Muted(),
            FontWeights.Normal);
        note.Margin = new Thickness(0d, 7d, 0d, 0d);
        body.Children.Add(note);

        var estimator = new NavBRNavigationEtaEstimator();
        var refreshing = false;

        async Task RefreshAsync()
        {
            if (refreshing || !window.IsLoaded)
            {
                return;
            }

            refreshing = true;
            try
            {
                var snapshot = await Task.Run(window.GetNavigationSnapshotForAlpha12);
                var estimate = estimator.Observe(snapshot, DateTimeOffset.UtcNow);
                nextStopEta.Text = FormatEta(estimate.ToNextStop);
                routeEndEta.Text = FormatEta(estimate.ToRouteEnd);
            }
            catch
            {
                estimator.Reset();
                nextStopEta.Text = "—";
                routeEndEta.Text = "—";
            }
            finally
            {
                refreshing = false;
            }
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1d) };
        timer.Tick += async (_, _) => await RefreshAsync();
        timer.Start();
        _ = RefreshAsync();

        window.Closed += (_, _) =>
        {
            timer.Stop();
            estimator.Reset();
            Installed.Remove(window);
        };
    }

    private static string FormatEta(TimeSpan? eta)
    {
        if (eta is not TimeSpan duration || duration < TimeSpan.Zero || duration > TimeSpan.FromHours(8d))
        {
            return "—";
        }

        var arrival = DateTimeOffset.Now.Add(duration).ToString("HH:mm", LocalizationService.CurrentCulture);
        var totalMinutes = Math.Max(0d, duration.TotalMinutes);
        string remaining;
        if (totalMinutes < 1d)
        {
            remaining = T("< 1 min", "< 1 min", "< 1 min", "< 1 Min.", "< 1 min");
        }
        else if (totalMinutes < 60d)
        {
            remaining = string.Format(
                LocalizationService.CurrentCulture,
                T("{0:0} min", "{0:0} min", "{0:0} min", "{0:0} Min.", "{0:0} min"),
                totalMinutes);
        }
        else
        {
            remaining = string.Format(
                LocalizationService.CurrentCulture,
                T("{0} h {1:00}", "{0} h {1:00}", "{0} h {1:00}", "{0} Std. {1:00}", "{0} h {1:00}"),
                (int)duration.TotalHours,
                duration.Minutes);
        }

        return $"{arrival} • {remaining}";
    }

    private static Border DataBlock(string label, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(Text(label, 9.5d, Muted(), FontWeights.SemiBold));
        value.Margin = new Thickness(0d, 5d, 0d, 0d);
        stack.Children.Add(value);
        return new Border
        {
            Padding = new Thickness(12d),
            Background = Brush(13, 26, 36),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = stack
        };
    }

    private static Grid TwoColumn(UIElement left, UIElement right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static TextBlock Value() => Text("—", 13d, White(), FontWeights.SemiBold);

    private static TextBlock Text(string value, double size, Brush color, FontWeight weight) => new()
    {
        Text = value,
        FontSize = size,
        Foreground = color,
        FontWeight = weight,
        TextWrapping = TextWrapping.Wrap
    };

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

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

    private static SolidColorBrush White() => Brush(218, 230, 238);
    private static SolidColorBrush Muted() => Brush(151, 171, 185);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
