using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class NavigationPage : UserControl
{
    private readonly ObservableCollection<string> _upcomingStops = new();

    public NavigationPage()
    {
        InitializeComponent();
        UpcomingStopsList.ItemsSource = _upcomingStops;
    }

    public void ApplyState(JsonElement state)
    {
        var navigation = JsonState.Property(state, "navigation");
        var available = JsonState.Bool(navigation, "available");

        SubtitleText.Text = available
            ? $"{JsonState.String(navigation, "mapName") ?? "Mapa"} · navegação operacional ativa"
            : "Aguardando mapa, linha e rota válidos do OMSI.";

        ServiceText.Text =
            $"Linha {JsonState.String(navigation, "line") ?? "—"} / Rota {JsonState.String(navigation, "route") ?? "—"}";
        DestinationText.Text =
            $"Destino: {JsonState.String(navigation, "destinationName") ?? "—"}";

        NextStopText.Text =
            JsonState.String(navigation, "nextStopName") ?? "—";
        ManeuverText.Text =
            JsonState.String(navigation, "maneuver") ?? "—";

        var distanceToManeuver = JsonState.Double(navigation, "distanceToManeuverMeters");
        var distanceToNextStop = JsonState.Double(navigation, "distanceToNextStopMeters");
        DistanceText.Text = FormatDistance(distanceToManeuver ?? distanceToNextStop);

        var etaSeconds = JsonState.Double(navigation, "etaToNextStopSeconds");
        EtaText.Text = FormatDuration(etaSeconds);

        var progress = Math.Clamp(
            JsonState.Double(navigation, "routeProgressPercent") ?? 0d,
            0d,
            100d);
        RouteProgressBar.Value = progress;
        RouteProgressText.Text =
            $"{progress:0.0}% · restante {FormatDistance(JsonState.Double(navigation, "distanceRemainingMeters"))}";

        var isOnRoute = JsonState.Bool(navigation, "isOnRoute");
        RouteStateText.Text = !available
            ? "SEM ROTA"
            : isOnRoute
                ? "NA ROTA"
                : "FORA DA ROTA";

        _upcomingStops.Clear();
        var sequence = JsonState.Property(navigation, "stopSequence");
        foreach (var stop in JsonState.Array(sequence, "upcomingStops"))
        {
            if (stop.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(stop.GetString()))
            {
                _upcomingStops.Add(stop.GetString()!);
            }
        }

        var diagnostic = JsonState.Property(navigation, "routeDiagnostic");
        RouteDiagnosticText.Text = JsonState.IsObject(diagnostic)
            ? $"Modo: {JsonState.String(diagnostic, "mode") ?? "—"}\n" +
              $"Track: {JsonState.String(diagnostic, "trackName") ?? "—"}\n" +
              $"Entradas: {JsonState.Int(diagnostic, "entryCount") ?? 0}\n" +
              $"Pontos: {JsonState.Int(diagnostic, "pointCount") ?? 0}\n" +
              $"Reentrada: {(JsonState.Bool(navigation, "rejoinAvailable") ? FormatDistance(JsonState.Double(navigation, "rejoinDistanceMeters")) : "não necessária/indisponível")}"
            : "Nenhum diagnóstico de rota disponível.";
    }

    private static string FormatDistance(double? meters)
    {
        if (meters is null || !double.IsFinite(meters.Value))
        {
            return "—";
        }

        return meters.Value >= 1000d
            ? $"{meters.Value / 1000d:0.0} km"
            : $"{meters.Value:0} m";
    }

    private static string FormatDuration(double? seconds)
    {
        if (seconds is null || !double.IsFinite(seconds.Value) || seconds < 0)
        {
            return "—";
        }

        var duration = TimeSpan.FromSeconds(seconds.Value);
        return duration.TotalHours >= 1d
            ? $"{(int)duration.TotalHours}h {duration.Minutes:00}min"
            : $"{Math.Max(0, (int)Math.Ceiling(duration.TotalMinutes))} min";
    }
}
