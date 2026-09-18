using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Driver;

internal sealed class DriverTripHistoryWindow : Window
{
    private readonly ListBox _trips = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _summary = new();
    private readonly TextBlock _details = new();
    private readonly IReadOnlyList<DriverTripHistoryEntry> _entries;

    public DriverTripHistoryWindow(Window owner)
    {
        Owner = owner;
        _entries = DriverTripHistoryStore.Load();
        Title = T("Histórico de viagens", "Trip history", "Historial de viajes", "Fahrtenverlauf", "Historique des trajets");
        Width = 920d;
        Height = 660d;
        MinWidth = 740d;
        MinHeight = 520d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        Icon = Application.Current?.MainWindow?.Icon;
        Content = BuildContent();
        Render();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 14d) };
        heading.Children.Add(new TextBlock
        {
            Text = T("Histórico de viagens", "Trip history", "Historial de viajes", "Fahrtenverlauf", "Historique des trajets"),
            FontSize = 24d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(218, 230, 238)
        });
        heading.Children.Add(new TextBlock
        {
            Text = T(
                "Registro local gerado somente a partir das sessões reais detectadas pela telemetria do OMSI.",
                "Local history generated only from real sessions detected by OMSI telemetry.",
                "Historial local generado solo a partir de sesiones reales detectadas por la telemetría de OMSI.",
                "Lokaler Verlauf, der nur aus echten, von der OMSI-Telemetrie erkannten Sitzungen erzeugt wird.",
                "Historique local généré uniquement à partir des sessions réelles détectées par la télémétrie OMSI."),
            Margin = new Thickness(0d, 5d, 0d, 0d),
            FontSize = 10.5d,
            Foreground = Brush(151, 171, 185),
            TextWrapping = TextWrapping.Wrap
        });
        _status.Margin = new Thickness(0d, 9d, 0d, 0d);
        _status.FontSize = 10d;
        _status.Foreground = Brush(113, 198, 255);
        heading.Children.Add(_status);
        root.Children.Add(heading);

        var summaryCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Padding = new Thickness(14d, 11d, 14d, 11d),
            Margin = new Thickness(0d, 0d, 0d, 14d),
            Child = _summary
        };
        _summary.FontSize = 10.5d;
        _summary.Foreground = Brush(151, 171, 185);
        _summary.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(summaryCard, 1);
        root.Children.Add(summaryCard);

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2d, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        _trips.Background = Brush(10, 19, 26);
        _trips.Foreground = Brush(218, 230, 238);
        _trips.BorderBrush = Brush(28, 42, 51);
        _trips.BorderThickness = new Thickness(1d);
        _trips.Padding = new Thickness(5d);
        _trips.DisplayMemberPath = nameof(TripListItem.DisplayText);
        _trips.SelectionChanged += (_, _) => RenderSelection();
        Grid.SetColumn(_trips, 0);
        content.Children.Add(_trips);

        var detailCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(18d),
            Child = _details
        };
        _details.FontSize = 11d;
        _details.Foreground = Brush(218, 230, 238);
        _details.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(detailCard, 2);
        content.Children.Add(detailCard);

        Grid.SetRow(content, 2);
        root.Children.Add(content);
        return root;
    }

    private void Render()
    {
        var items = _entries
            .Select(entry => new TripListItem(entry, FormatListItem(entry)))
            .ToArray();
        _trips.ItemsSource = items;
        _status.Text = string.Format(
            LocalizationService.CurrentCulture,
            T("{0:N0} viagem(ns) armazenada(s) localmente.", "{0:N0} trip(s) stored locally.", "{0:N0} viaje(s) guardado(s) localmente.", "{0:N0} Fahrt(en) lokal gespeichert.", "{0:N0} trajet(s) stocké(s) localement."),
            items.Length);
        _summary.Text = BuildSummary();

        if (items.Length == 0)
        {
            _details.Text = T(
                "Nenhuma viagem concluída foi registrada ainda. O histórico será preenchido ao encerrar uma sessão real do OMSI.",
                "No completed trip has been recorded yet. History will be populated when a real OMSI session ends.",
                "Todavía no se ha registrado ningún viaje finalizado. El historial se completará al terminar una sesión real de OMSI.",
                "Noch keine abgeschlossene Fahrt aufgezeichnet. Der Verlauf wird nach dem Ende einer echten OMSI-Sitzung ergänzt.",
                "Aucun trajet terminé n’a encore été enregistré. L’historique sera alimenté à la fin d’une session OMSI réelle.");
            return;
        }

        _trips.SelectedIndex = 0;
    }

    private string BuildSummary()
    {
        if (_entries.Count == 0)
        {
            return T(
                "Linhas e mapas aparecerão aqui somente depois de serem observados em viagens reais.",
                "Lines and maps will appear here only after being observed in real trips.",
                "Las líneas y los mapas aparecerán aquí solo después de observarse en viajes reales.",
                "Linien und Karten erscheinen hier erst, nachdem sie in echten Fahrten beobachtet wurden.",
                "Les lignes et cartes apparaîtront ici uniquement après avoir été observées lors de trajets réels.");
        }

        var culture = LocalizationService.CurrentCulture;
        var lines = DistinctObserved(_entries.Select(entry => entry.Line));
        var maps = DistinctObserved(_entries.Select(entry => entry.MapName));
        var totalDistance = _entries.Sum(entry => entry.DistanceKm);
        var totalDrivingSeconds = _entries.Sum(entry => entry.DrivingSeconds);

        return string.Join(Environment.NewLine,
            string.Format(
                culture,
                T(
                    "Resumo do histórico: {0:N1} km • {1} dirigindo • {2:N0} linha(s) • {3:N0} mapa(s)",
                    "History summary: {0:N1} km • {1} driving • {2:N0} line(s) • {3:N0} map(s)",
                    "Resumen del historial: {0:N1} km • {1} conduciendo • {2:N0} línea(s) • {3:N0} mapa(s)",
                    "Verlaufsübersicht: {0:N1} km • {1} Fahrzeit • {2:N0} Linie(n) • {3:N0} Karte(n)",
                    "Résumé de l’historique : {0:N1} km • {1} de conduite • {2:N0} ligne(s) • {3:N0} carte(s)"),
                totalDistance,
                FormatDuration(totalDrivingSeconds),
                lines.Count,
                maps.Count),
            $"{T("Linhas registradas", "Recorded lines", "Líneas registradas", "Erfasste Linien", "Lignes enregistrées")}: {FormatCatalog(lines)}",
            $"{T("Mapas registrados", "Recorded maps", "Mapas registrados", "Erfasste Karten", "Cartes enregistrées")}: {FormatCatalog(maps)}");
    }

    private void RenderSelection()
    {
        if (_trips.SelectedItem is not TripListItem item)
        {
            return;
        }

        var trip = item.Entry;
        var culture = LocalizationService.CurrentCulture;
        var duration = trip.EndedAtUtc - trip.StartedAtUtc;
        _details.Text = string.Join(Environment.NewLine,
            $"{T("Início", "Started", "Inicio", "Beginn", "Début")}: {trip.StartedAtUtc.ToLocalTime().ToString("g", culture)}",
            $"{T("Fim", "Ended", "Fin", "Ende", "Fin")}: {trip.EndedAtUtc.ToLocalTime().ToString("g", culture)}",
            $"{T("Sessão", "Session", "Sesión", "Sitzung", "Session")}: {FormatDuration(duration.TotalSeconds)}",
            $"{T("Tempo dirigindo", "Driving time", "Tiempo conduciendo", "Fahrzeit", "Temps de conduite")}: {FormatDuration(trip.DrivingSeconds)}",
            $"{T("Distância", "Distance", "Distancia", "Distanz", "Distance")}: {trip.DistanceKm.ToString("N1", culture)} km",
            $"{T("Maior velocidade", "Top speed", "Velocidad máxima", "Höchstgeschwindigkeit", "Vitesse maximale")}: {trip.HighestSpeedKph.ToString("N1", culture)} km/h",
            string.Empty,
            $"{T("Mapa", "Map", "Mapa", "Karte", "Carte")}: {Value(trip.MapName)}",
            $"{T("Linha", "Line", "Línea", "Linie", "Ligne")}: {Value(trip.Line)}",
            $"{T("Rota", "Route", "Ruta", "Route", "Itinéraire")}: {Value(trip.Route)}",
            $"{T("Veículo", "Vehicle", "Vehículo", "Fahrzeug", "Véhicule")}: {Value(trip.VehicleName)}");
    }

    private static IReadOnlyList<string> DistinctObserved(IEnumerable<string?> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!.Trim())
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    private static string FormatCatalog(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "—";
        }

        const int visibleLimit = 8;
        var visible = string.Join(", ", values.Take(visibleLimit));
        return values.Count > visibleLimit
            ? $"{visible}  +{values.Count - visibleLimit}"
            : visible;
    }

    private static string FormatListItem(DriverTripHistoryEntry trip)
    {
        var culture = LocalizationService.CurrentCulture;
        var date = trip.StartedAtUtc.ToLocalTime().ToString("g", culture);
        var operation = !string.IsNullOrWhiteSpace(trip.Line)
            ? string.IsNullOrWhiteSpace(trip.Route) ? trip.Line : $"{trip.Line} / {trip.Route}"
            : Value(trip.MapName);
        return $"{date}   •   {operation}   •   {trip.DistanceKm.ToString("N1", culture)} km   •   {FormatDuration(trip.DrivingSeconds)}";
    }

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        return duration.TotalHours >= 1d
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    private sealed record TripListItem(DriverTripHistoryEntry Entry, string DisplayText);
}
