using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class DriverPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<DriverTripRow> _trips = new();

    public DriverPage()
    {
        InitializeComponent();
        TripHistoryList.ItemsSource = _trips;
    }

    public void ApplyState(JsonElement state)
    {
        var driver = JsonState.Property(state, "driver");
        if (!JsonState.IsObject(driver))
        {
            LastOperationText.Text = "Perfil do motorista indisponível no Runtime Host.";
            return;
        }

        var profile = JsonState.Property(driver, "profile");
        var totalDrivingSeconds = JsonState.Double(profile, "totalDrivingSeconds") ?? 0d;
        var totalDistanceKm = JsonState.Double(profile, "totalDistanceKm") ?? 0d;
        var trips = JsonState.Int(profile, "trips") ?? 0;
        var averageSpeed = JsonState.Double(profile, "averageMovingSpeedKph") ?? 0d;
        var highestSpeed = JsonState.Double(profile, "highestSpeedKph") ?? 0d;

        DrivingTimeText.Text = $"{totalDrivingSeconds / 3600d:0.0} h";
        DistanceText.Text = $"{totalDistanceKm:0.0} km";
        TripsText.Text = trips.ToString();
        AverageSpeedText.Text = $"{averageSpeed:0.0} km/h";
        HighestSpeedText.Text = $"{highestSpeed:0.0} km/h";

        if (DisplayNameTextBox.FocusState == FocusState.Unfocused)
        {
            DisplayNameTextBox.Text =
                JsonState.String(profile, "displayName")
                ?? DisplayNameTextBox.Text;
        }

        if (CompanyNameTextBox.FocusState == FocusState.Unfocused)
        {
            CompanyNameTextBox.Text =
                JsonState.String(profile, "companyName")
                ?? string.Empty;
        }

        var lastDrivenAt = JsonState.String(profile, "lastDrivenAt");
        var lastDrivenText =
            DateTimeOffset.TryParse(lastDrivenAt, out var parsedLast)
                ? parsedLast.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : "—";
        LastOperationText.Text =
            $"Última operação: {lastDrivenText} · " +
            $"{JsonState.String(profile, "lastMap") ?? "mapa —"} · " +
            $"linha {JsonState.String(profile, "lastLine") ?? "—"} / " +
            $"rota {JsonState.String(profile, "lastRoute") ?? "—"}";

        _trips.Clear();
        foreach (var trip in JsonState.Array(driver, "tripHistory"))
        {
            var started = JsonState.String(trip, "startedAtUtc");
            var dateText =
                DateTimeOffset.TryParse(started, out var parsed)
                    ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                    : "—";
            var seconds = JsonState.Double(trip, "drivingSeconds") ?? 0d;
            var distance = JsonState.Double(trip, "distanceKm") ?? 0d;
            var maxSpeed = JsonState.Double(trip, "highestSpeedKph") ?? 0d;
            var map = JsonState.String(trip, "mapName") ?? "mapa —";
            var line = JsonState.String(trip, "line") ?? "—";
            var route = JsonState.String(trip, "route") ?? "—";
            var vehicle = JsonState.String(trip, "vehicleName") ?? "veículo —";

            _trips.Add(new DriverTripRow(
                dateText,
                $"{map} · {line}/{route}",
                vehicle,
                $"{seconds / 60d:0} min · {distance:0.00} km",
                $"máx. {maxSpeed:0.0} km/h"));
        }

        HistorySummaryText.Text = _trips.Count == 0
            ? "Nenhuma viagem registrada."
            : $"{_trips.Count} viagem{(_trips.Count == 1 ? string.Empty : "s")} no histórico local.";

        var transfer = JsonState.Property(driver, "profileTransfer");
        var pending = JsonState.Property(transfer, "pending");
        var hasPending = JsonState.IsObject(pending);
        ApplyImportButton.IsEnabled = hasPending;
        CancelImportButton.IsEnabled = hasPending;

        if (hasPending)
        {
            PendingImportText.Text =
                $"Importação pronta: {JsonState.String(pending, "displayName") ?? "Motorista"} · " +
                $"{JsonState.String(pending, "companyName") ?? "sem empresa"} · " +
                $"{JsonState.Int(pending, "tripCount") ?? 0} viagens · " +
                $"histórico {(JsonState.Bool(pending, "includesTripHistory") ? "incluído" : "não incluído")}.";
        }
        else
        {
            PendingImportText.Text =
                JsonState.String(transfer, "notice")
                ?? "Nenhuma importação pendente.";
        }
    }

    private async Task RunAsync(
        string command,
        object? payload = null,
        string successMessage = "Perfil atualizado.")
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = successMessage;
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
        }
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "saveDriverProfile",
            new
            {
                displayName = DisplayNameTextBox.Text,
                companyName = CompanyNameTextBox.Text
            },
            "Perfil do motorista salvo.");

    private async void ExportProfile_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "exportDriverProfile",
            successMessage: "Exportação concluída ou cancelada.");

    private async void SelectImport_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "selectDriverProfileImport",
            successMessage: "Arquivo de importação analisado.");

    private async void ApplyImport_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "applyDriverProfileImport",
            successMessage: "Perfil importado.");

    private async void CancelImport_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "cancelDriverProfileImport",
            successMessage: "Importação cancelada.");
}

public sealed record DriverTripRow(
    string DateText,
    string Service,
    string Vehicle,
    string DurationDistance,
    string HighestSpeed);
