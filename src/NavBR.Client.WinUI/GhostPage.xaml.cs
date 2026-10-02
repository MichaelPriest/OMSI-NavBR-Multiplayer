using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class GhostPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<GhostReplayRow> _library = new();
    private bool _applyingState;

    public GhostPage()
    {
        InitializeComponent();
        GhostLibraryList.ItemsSource = _library;
    }

    public void ApplyState(JsonElement state)
    {
        var ghost = JsonState.Property(state, "ghost");
        if (!JsonState.IsObject(ghost))
        {
            RecordingStateText.Text = "INDISPONÍVEL";
            PlaybackStateText.Text = "INDISPONÍVEL";
            StatusText.Text = "Runtime Host não retornou o módulo Ghost.";
            return;
        }

        var recording = JsonState.Bool(ghost, "recording");
        var playing = JsonState.Bool(ghost, "playing");
        RecordingStateText.Text = recording ? "GRAVANDO" : "PARADA";
        PlaybackStateText.Text = playing ? "REPRODUZINDO" : "PARADA";
        FrameCountText.Text =
            JsonState.Int(ghost, "frameCount")?.ToString()
            ?? JsonState.Double(ghost, "frameCount")?.ToString("0")
            ?? "0";

        GhostDirectoryText.Text =
            $"Pasta: {JsonState.String(ghost, "ghostDirectory") ?? "—"}";

        var currentSelectedFile = (GhostLibraryList.SelectedItem as GhostReplayRow)?.FileName;
        GhostReplayRow? selectedRow = null;

        _applyingState = true;
        try
        {
            _library.Clear();
            foreach (var item in JsonState.Array(ghost, "library"))
            {
                var fileName = JsonState.String(item, "fileName") ?? string.Empty;
                var name = JsonState.String(item, "name") ?? fileName;
                var map = JsonState.String(item, "mapName") ?? "mapa —";
                var vehicle = JsonState.String(item, "vehicleName") ?? "veículo —";
                var duration = JsonState.Double(item, "durationSeconds") ?? 0d;
                var distance = JsonState.Double(item, "estimatedDistanceKm") ?? 0d;
                var averageSpeed = JsonState.Double(item, "averageSpeedKph") ?? 0d;
                var recordedAt = JsonState.String(item, "recordedAtUtc");
                var recordedAtText =
                    DateTimeOffset.TryParse(recordedAt, out var parsed)
                        ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                        : "—";

                var row = new GhostReplayRow(
                    fileName,
                    name,
                    $"{map} · {vehicle}",
                    recordedAtText,
                    $"{duration / 60d:0.0} min · {distance:0.00} km · {averageSpeed:0.0} km/h");

                _library.Add(row);

                if (JsonState.Bool(item, "selected") ||
                    (!string.IsNullOrWhiteSpace(currentSelectedFile) &&
                     string.Equals(
                         currentSelectedFile,
                         fileName,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    selectedRow = row;
                }
            }

            GhostLibraryList.SelectedItem = selectedRow;
        }
        finally
        {
            _applyingState = false;
        }

        var invalid = JsonState.Int(ghost, "libraryInvalidCount") ?? 0;
        LibraryCountText.Text = invalid > 0
            ? $"{_library.Count} (+{invalid} inválidos)"
            : _library.Count.ToString();

        var selected = JsonState.Property(ghost, "selected");
        if (JsonState.IsObject(selected))
        {
            var analytics = JsonState.Property(selected, "analytics");
            var duration = JsonState.Double(selected, "durationSeconds")
                ?? JsonState.Double(analytics, "durationSeconds")
                ?? 0d;
            var distance = JsonState.Double(analytics, "estimatedDistanceKm") ?? 0d;
            var average = JsonState.Double(analytics, "averageSpeedKph") ?? 0d;
            var maximum = JsonState.Double(analytics, "maximumSpeedKph") ?? 0d;
            SelectedReplayText.Text =
                $"{JsonState.String(selected, "name") ?? "Replay"}\n" +
                $"Mapa: {JsonState.String(selected, "mapName") ?? "—"}\n" +
                $"Veículo: {JsonState.String(selected, "vehicleName") ?? "—"}\n" +
                $"Linha: {JsonState.String(selected, "line") ?? "—"}\n" +
                $"Duração: {duration / 60d:0.0} min · Distância: {distance:0.00} km\n" +
                $"Velocidade média/máxima: {average:0.0} / {maximum:0.0} km/h";
        }
        else
        {
            SelectedReplayText.Text = "Nenhum replay selecionado.";
        }

        var status = JsonState.String(ghost, "status");
        var error = JsonState.String(ghost, "error");
        StatusText.Text = error is null
            ? $"Estado: {status ?? "pronto"}"
            : $"Estado: {status ?? "erro"} · {error}";

        StartRecordingButton.IsEnabled = !recording && !playing;
        StopRecordingButton.IsEnabled = recording;
        CancelRecordingButton.IsEnabled = recording;
        SelectFileButton.IsEnabled = !recording && !playing;
        ImportButton.IsEnabled = !recording && !playing;
        GhostLibraryList.IsEnabled = !recording && !playing;
        PlayButton.IsEnabled =
            !recording &&
            !playing &&
            JsonState.IsObject(selected);
        StopPlaybackButton.IsEnabled = playing;
    }

    private async Task RunAsync(
        string command,
        object? payload = null,
        string successMessage = "Ghost atualizado.")
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

    private async void StartRecording_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "startGhostRecording",
            new { name = RecordingNameTextBox.Text },
            "Gravação Ghost iniciada.");

    private async void StopRecording_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "stopGhostRecording",
            successMessage: "Replay Ghost salvo.");

    private async void CancelRecording_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "cancelGhostRecording",
            successMessage: "Gravação cancelada.");

    private async void SelectFile_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "selectGhostFile",
            successMessage: "Replay carregado.");

    private async void Import_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "importGhostReplay",
            successMessage: "Replay importado.");

    private async void RefreshLibrary_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "refreshGhostLibrary",
            successMessage: "Biblioteca atualizada.");

    private async void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("openGhostFolder");

    private async void GhostLibraryList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingState ||
            GhostLibraryList.SelectedItem is not GhostReplayRow replay)
        {
            return;
        }

        await RunAsync(
            "selectGhostLibraryItem",
            new { fileName = replay.FileName },
            "Replay selecionado.");
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        var playbackSpeed =
            double.IsFinite(PlaybackSpeedNumberBox.Value)
                ? PlaybackSpeedNumberBox.Value
                : 1d;

        await RunAsync(
            "playGhost",
            new
            {
                playbackSpeed,
                loop = LoopToggle.IsOn
            },
            "Reprodução Ghost iniciada.");
    }

    private async void StopPlayback_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "stopGhostPlayback",
            successMessage: "Parada do Ghost solicitada.");
}

public sealed record GhostReplayRow(
    string FileName,
    string Name,
    string Context,
    string RecordedAtText,
    string Stats);
