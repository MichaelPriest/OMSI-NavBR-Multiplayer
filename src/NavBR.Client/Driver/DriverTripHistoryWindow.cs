using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Driver;

internal sealed class DriverTripHistoryWindow : Window
{
    private readonly ListBox _trips = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _details = new();
    private readonly IReadOnlyList<DriverTripHistoryEntry> _entries;

    public DriverTripHistoryWindow(Window owner)
    {
        Owner = owner;
        _entries = DriverTripHistoryStore.Load();
        Title = T("Histórico de viagens", "Trip history", "Historial de viajes", "Fahrtenverlauf", "Historique des trajets");
        Width = 900d;
        Height = 620d;
        MinWidth = 720d;
        MinHeight = 500d;
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
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 16d) };
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

        Grid.SetRow(content, 1);
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
