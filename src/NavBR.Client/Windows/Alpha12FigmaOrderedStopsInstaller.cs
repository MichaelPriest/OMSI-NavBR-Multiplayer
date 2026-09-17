using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

/// <summary>
/// Adds the ordered stop sequence from the active OMSI .ttp to the Figma route
/// rail. The list starts at the telemetry-reported next stop only when that
/// stop can be matched safely to the trip sequence.
/// </summary>
internal static class Alpha12FigmaOrderedStopsInstaller
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

        var separator = new Border
        {
            Height = 1d,
            Background = Brush(28, 42, 51),
            Margin = new Thickness(0d, 20d, 0d, 16d)
        };
        body.Children.Add(separator);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(Text(
            T("PRÓXIMAS PARADAS", "UPCOMING STOPS", "PRÓXIMAS PARADAS", "NÄCHSTE HALTESTELLEN", "PROCHAINS ARRÊTS"),
            10d,
            Brush(151, 171, 185),
            FontWeights.Bold));
        var summary = Text("—", 10d, Brush(113, 198, 255), FontWeights.SemiBold);
        Grid.SetColumn(summary, 1);
        header.Children.Add(summary);
        body.Children.Add(header);

        var list = new StackPanel { Margin = new Thickness(0d, 10d, 0d, 0d) };
        body.Children.Add(list);

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
                var snapshot = await Task.Run(window.GetOrderedRouteStopsForAlpha12);
                list.Children.Clear();

                if (!snapshot.RouteResolved)
                {
                    summary.Text = "—";
                    list.Children.Add(MutedLine(T(
                        "Sequência de paradas indisponível para esta viagem.",
                        "Stop sequence is unavailable for this trip.",
                        "La secuencia de paradas no está disponible para este viaje.",
                        "Haltestellenfolge für diese Fahrt nicht verfügbar.",
                        "La séquence des arrêts est indisponible pour ce trajet.")));
                    return;
                }

                if (!snapshot.NextStopIndex.HasValue || snapshot.UpcomingStops.Count == 0)
                {
                    summary.Text = string.Format(
                        T("{0} paradas", "{0} stops", "{0} paradas", "{0} Haltestellen", "{0} arrêts"),
                        snapshot.TotalStops);
                    list.Children.Add(MutedLine(T(
                        "Viagem identificada; aguardando posição segura na sequência.",
                        "Trip identified; waiting for a safe position in the sequence.",
                        "Viaje identificado; esperando una posición segura en la secuencia.",
                        "Fahrt erkannt; warte auf eine sichere Position in der Folge.",
                        "Trajet identifié ; attente d’une position fiable dans la séquence.")));
                    return;
                }

                summary.Text = string.Format(
                    T("{0}/{1}", "{0}/{1}", "{0}/{1}", "{0}/{1}", "{0}/{1}"),
                    snapshot.NextStopIndex.Value + 1,
                    snapshot.TotalStops);

                for (var offset = 0; offset < snapshot.UpcomingStops.Count; offset++)
                {
                    var absoluteIndex = snapshot.NextStopIndex.Value + offset;
                    list.Children.Add(StopRow(
                        absoluteIndex + 1,
                        snapshot.UpcomingStops[offset],
                        isNext: offset == 0));
                }
            }
            catch
            {
                summary.Text = "—";
                list.Children.Clear();
                list.Children.Add(MutedLine(T(
                    "Não foi possível atualizar a sequência agora.",
                    "The stop sequence could not be refreshed right now.",
                    "No se pudo actualizar la secuencia ahora.",
                    "Die Haltestellenfolge konnte gerade nicht aktualisiert werden.",
                    "Impossible d’actualiser la séquence pour le moment.")));
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
            Installed.Remove(window);
        };
    }

    private static Border StopRow(int number, string stopName, bool isNext)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var index = Text(
            isNext ? "→" : number.ToString(),
            isNext ? 15d : 10d,
            isNext ? Brush(113, 198, 255) : Brush(96, 113, 125),
            FontWeights.Bold);
        index.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(index);

        var name = Text(
            stopName,
            11.5d,
            isNext ? Brushes.White : Brush(190, 205, 215),
            isNext ? FontWeights.SemiBold : FontWeights.Normal);
        name.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 6d),
            Padding = new Thickness(10d, 8d, 10d, 8d),
            Background = isNext ? Brush(16, 38, 56) : Brush(13, 26, 36),
            BorderBrush = isNext ? Brush(61, 137, 196) : Brush(28, 42, 51),
            BorderThickness = new Thickness(isNext ? 1d : 0d),
            CornerRadius = new CornerRadius(8d),
            Child = grid
        };
    }

    private static TextBlock MutedLine(string value) =>
        Text(value, 10.5d, Brush(151, 171, 185), FontWeights.Normal);

    private static TextBlock Text(string value, double size, Brush foreground, FontWeight weight) => new()
    {
        Text = value,
        FontSize = size,
        Foreground = foreground,
        FontWeight = weight,
        TextWrapping = TextWrapping.Wrap
    };

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
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

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
