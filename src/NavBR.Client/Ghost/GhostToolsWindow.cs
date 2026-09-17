using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NavBR.Client.Localization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Ghost;

internal sealed class GhostToolsWindow : Window
{
    private readonly Func<VehicleTelemetry?> _telemetrySource;
    private readonly GhostRecorder _recorder = new();
    private readonly GhostReplayPlayer _player = new();
    private readonly DispatcherTimer _recordTimer;
    private readonly TextBlock _status = new();
    private readonly TextBlock _details = new();
    private readonly Button _recordButton;
    private readonly Button _stopButton;
    private readonly Button _playButton;

    public GhostToolsWindow(Func<VehicleTelemetry?> telemetrySource)
    {
        _telemetrySource = telemetrySource;
        Title = Text("Title");
        Width = 760d;
        Height = 520d;
        MinWidth = 660d;
        MinHeight = 440d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        Icon = Application.Current?.MainWindow?.Icon;

        _recordButton = CreateButton(Text("Record"), Record_Click, true);
        _stopButton = CreateButton(Text("StopSave"), Stop_Click, false);
        _playButton = CreateButton(Text("Play"), Play_Click, false);

        Content = BuildContent();

        _recordTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _recordTimer.Tick += (_, _) => CaptureFrame();
        Closed += (_, _) =>
        {
            _recordTimer.Stop();
            _recorder.Cancel();
            _player.Stop();
        };

        RefreshUi();
    }

    internal static string MenuText() => Text("Menu");
    internal static string MenuToolTip() => Text("MenuTip");

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        heading.Children.Add(new TextBlock
        {
            Text = Text("Heading"),
            FontSize = 26d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(218, 230, 238)
        });
        heading.Children.Add(new TextBlock
        {
            Text = Text("Subtitle"),
            Margin = new Thickness(0d, 6d, 0d, 0d),
            Foreground = Brush(151, 171, 185),
            FontSize = 11.5d,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var warning = new Border
        {
            Background = Brush(22, 26, 22),
            BorderBrush = Brush(90, 75, 38),
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(14d),
            CornerRadius = new CornerRadius(10d),
            Margin = new Thickness(0d, 0d, 0d, 16d),
            Child = new TextBlock
            {
                Text = Text("Warning"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(242, 184, 75),
                FontSize = 10.8d
            }
        };
        Grid.SetRow(warning, 1);
        root.Children.Add(warning);

        var actions = new WrapPanel { Margin = new Thickness(0d, 0d, 0d, 16d) };
        actions.Children.Add(_recordButton);
        actions.Children.Add(_stopButton);
        actions.Children.Add(_playButton);
        actions.Children.Add(CreateButton(Text("OpenFolder"), OpenFolder_Click, false));
        Grid.SetRow(actions, 2);
        root.Children.Add(actions);

        var statusCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(18d),
            Child = BuildStatusContent()
        };
        Grid.SetRow(statusCard, 3);
        root.Children.Add(statusCard);

        return root;
    }

    private UIElement BuildStatusContent()
    {
        var content = new StackPanel();

        _status.FontSize = 15d;
        _status.FontWeight = FontWeights.SemiBold;
        _status.Foreground = Brush(218, 230, 238);
        content.Children.Add(_status);

        _details.Margin = new Thickness(0d, 10d, 0d, 0d);
        _details.Foreground = Brush(151, 171, 185);
        _details.TextWrapping = TextWrapping.Wrap;
        _details.FontFamily = new FontFamily("Consolas");
        _details.FontSize = 10.5d;
        content.Children.Add(_details);

        return content;
    }

    private static Button CreateButton(string text, RoutedEventHandler handler, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 138d,
            Height = 38d,
            Margin = new Thickness(0d, 0d, 8d, 8d),
            Padding = new Thickness(13d, 5d, 13d, 5d),
            Background = primary ? Brush(61, 137, 196) : Brush(13, 26, 36),
            Foreground = Brush(218, 230, 238),
            BorderBrush = primary ? Brush(113, 198, 255) : Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            FontSize = 11.5d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += handler;
        return button;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        var telemetry = _telemetrySource();
        if (telemetry is null || !telemetry.IsInGame)
        {
            SetStatus(Text("NeedOmsi"), false);
            return;
        }

        _player.Stop();
        _recorder.Start();
        _recordTimer.Start();
        CaptureFrame();
        RefreshUi();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (!_recorder.IsRecording)
        {
            return;
        }

        _recordTimer.Stop();
        try
        {
            var path = await _recorder.StopAndSaveAsync();
            SetStatus(Text("Saved"), true);
            _details.Text = path;
        }
        catch (Exception ex)
        {
            _recorder.Cancel();
            SetStatus(Text("SaveFailed"), false);
            _details.Text = ex.Message;
        }
        finally
        {
            RefreshUi(keepDetails: true);
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Text("ChooseFile"),
            Filter = $"NavBR Ghost (*{GhostReplayFormat.Extension})|*{GhostReplayFormat.Extension}|{Text("AllFiles")} (*.*)|*.*",
            InitialDirectory = Directory.Exists(GhostRecorder.GetGhostDirectory())
                ? GhostRecorder.GetGhostDirectory()
                : null
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _recordTimer.Stop();
        if (_recorder.IsRecording)
        {
            _recorder.Cancel();
        }

        try
        {
            var document = await _player.LoadAsync(dialog.FileName);
            var metadata = document.Metadata;
            _details.Text = string.Format(
                LocalizationService.CurrentCulture,
                Text("ReplayInfo"),
                metadata.Name,
                Empty(metadata.MapName),
                Empty(metadata.VehicleName),
                TimeSpan.FromSeconds(Math.Max(0d, metadata.DurationSeconds)).ToString(@"hh\:mm\:ss"),
                metadata.FrameCount,
                dialog.FileName);

            SetStatus(Text("Starting"), null);
            RefreshUi(keepDetails: true);
            await _player.PlayAsync(dialog.FileName);
            SetStatus(Text("Completed"), true);
        }
        catch (OperationCanceledException)
        {
            SetStatus(Text("Interrupted"), null);
        }
        catch (Exception ex)
        {
            SetStatus(Text("Unavailable"), false);
            _details.Text = $"{dialog.FileName}{Environment.NewLine}{Environment.NewLine}{ex.Message}";
        }
        finally
        {
            RefreshUi(keepDetails: true);
        }
    }

    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var directory = GhostRecorder.GetGhostDirectory();
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directory}\"",
            UseShellExecute = true
        });
    }

    private void CaptureFrame()
    {
        _recorder.Record(_telemetrySource());
        if (_recorder.IsRecording)
        {
            _status.Text = string.Format(Text("Recording"), _recorder.FrameCount);
            _status.Foreground = Brush(56, 201, 140);
            _details.Text = Text("RecordingNote");
        }
    }

    private void SetStatus(string text, bool? success)
    {
        _status.Text = text;
        _status.Foreground = success switch
        {
            true => Brush(56, 201, 140),
            false => Brush(239, 91, 100),
            _ => Brush(218, 230, 238)
        };
    }

    private void RefreshUi(bool keepDetails = false)
    {
        _recordButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _stopButton.IsEnabled = _recorder.IsRecording;
        _playButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;

        if (_recorder.IsRecording)
        {
            return;
        }

        if (!_player.IsPlaying && string.IsNullOrWhiteSpace(_status.Text))
        {
            SetStatus(Text("Ready"), null);
        }

        if (!keepDetails && string.IsNullOrWhiteSpace(_details.Text))
        {
            _details.Text = string.Format(Text("Folder"), GhostRecorder.GetGhostDirectory());
        }
    }

    private static string Text(string key)
    {
        var table = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("Title", "Ghost / Replay — NavBR Alpha.12"),
        ("Menu", "Ghost / Replay"),
        ("MenuTip", "Record real OMSI telemetry and replay a Ghost Bus through the experimental bridge."),
        ("Heading", "Ghost / Replay"),
        ("Subtitle", "Record a real trip from your bus and replay it later using NavBR's versioned Ghost format."),
        ("Warning", "Recording is read-only and safe. Physical Ghost 3D playback only runs when the experimental OMSI bridge accepts spawn/transform commands; otherwise NavBR stops with a diagnostic and does not force writes."),
        ("Record", "● Record trip"), ("StopSave", "■ Stop and save"), ("Play", "▶ Play Ghost 3D"), ("OpenFolder", "Open Ghost folder"),
        ("NeedOmsi", "Load a map and a bus in OMSI before recording."), ("Saved", "Ghost saved."), ("SaveFailed", "Could not save the Ghost."),
        ("ChooseFile", "Choose a NavBR Ghost"), ("AllFiles", "All files"), ("Starting", "Starting Ghost 3D…"), ("Completed", "Ghost completed."),
        ("Interrupted", "Ghost interrupted."), ("Unavailable", "Ghost 3D is unavailable in this configuration."), ("Ready", "Ready to record."),
        ("Recording", "Recording… {0:N0} frames"), ("RecordingNote", "Recording uses only local NavBR telemetry; no value is written to OMSI."),
        ("Folder", "Folder: {0}"), ("ReplayInfo", "Name: {0}\nMap: {1}\nVehicle: {2}\nDuration: {3}\nFrames: {4:N0}\nFile: {5}"));

    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Ghost / Replay — NavBR Alpha.12"),
        ("Menu", "Ghost / Replay"),
        ("MenuTip", "Grave telemetria real do OMSI e reproduza um Ghost Bus pelo bridge experimental."),
        ("Heading", "Ghost / Replay"),
        ("Subtitle", "Grave uma viagem real do seu ônibus e reproduza depois usando o formato versionado de Ghost do NavBR."),
        ("Warning", "A gravação é somente leitura e segura. A reprodução física Ghost 3D só roda quando o bridge experimental do OMSI aceita comandos de spawn/transform; caso contrário, o NavBR interrompe com diagnóstico e não força escrita."),
        ("Record", "● Gravar viagem"), ("StopSave", "■ Parar e salvar"), ("Play", "▶ Reproduzir Ghost 3D"), ("OpenFolder", "Abrir pasta de Ghosts"),
        ("NeedOmsi", "Carregue um mapa e um ônibus no OMSI antes de gravar."), ("Saved", "Ghost salvo."), ("SaveFailed", "Não foi possível salvar o Ghost."),
        ("ChooseFile", "Escolha um Ghost NavBR"), ("AllFiles", "Todos os arquivos"), ("Starting", "Iniciando Ghost 3D…"), ("Completed", "Ghost concluído."),
        ("Interrupted", "Ghost interrompido."), ("Unavailable", "Ghost 3D indisponível nesta configuração."), ("Ready", "Pronto para gravar."),
        ("Recording", "Gravando… {0:N0} frames"), ("RecordingNote", "A gravação usa somente a telemetria local do NavBR; nenhum valor é escrito no OMSI."),
        ("Folder", "Pasta: {0}"), ("ReplayInfo", "Nome: {0}\nMapa: {1}\nVeículo: {2}\nDuração: {3}\nFrames: {4:N0}\nArquivo: {5}"));

    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Ghost / Replay — NavBR Alpha.12"), ("Menu", "Ghost / Replay"),
        ("MenuTip", "Graba telemetría real de OMSI y reproduce un Ghost Bus mediante el bridge experimental."),
        ("Heading", "Ghost / Replay"), ("Subtitle", "Graba un viaje real de tu autobús y reprodúcelo después con el formato Ghost versionado de NavBR."),
        ("Warning", "La grabación es de solo lectura y segura. La reproducción física Ghost 3D solo funciona cuando el bridge experimental de OMSI acepta comandos spawn/transform; de lo contrario NavBR se detiene con un diagnóstico y no fuerza escrituras."),
        ("Record", "● Grabar viaje"), ("StopSave", "■ Detener y guardar"), ("Play", "▶ Reproducir Ghost 3D"), ("OpenFolder", "Abrir carpeta Ghost"),
        ("NeedOmsi", "Carga un mapa y un autobús en OMSI antes de grabar."), ("Saved", "Ghost guardado."), ("SaveFailed", "No se pudo guardar el Ghost."),
        ("ChooseFile", "Elige un Ghost NavBR"), ("AllFiles", "Todos los archivos"), ("Starting", "Iniciando Ghost 3D…"), ("Completed", "Ghost finalizado."),
        ("Interrupted", "Ghost interrumpido."), ("Unavailable", "Ghost 3D no está disponible en esta configuración."), ("Ready", "Listo para grabar."),
        ("Recording", "Grabando… {0:N0} frames"), ("RecordingNote", "La grabación usa solo telemetría local de NavBR; no se escribe ningún valor en OMSI."),
        ("Folder", "Carpeta: {0}"), ("ReplayInfo", "Nombre: {0}\nMapa: {1}\nVehículo: {2}\nDuración: {3}\nFrames: {4:N0}\nArchivo: {5}"));

    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Ghost / Replay — NavBR Alpha.12"), ("Menu", "Ghost / Replay"),
        ("MenuTip", "Echte OMSI-Telemetrie aufzeichnen und einen Ghost Bus über die experimentelle Bridge wiedergeben."),
        ("Heading", "Ghost / Replay"), ("Subtitle", "Eine echte Busfahrt aufzeichnen und später im versionierten NavBR-Ghost-Format wiedergeben."),
        ("Warning", "Die Aufzeichnung ist schreibgeschützt und sicher. Die physische Ghost-3D-Wiedergabe läuft nur, wenn die experimentelle OMSI-Bridge Spawn-/Transform-Befehle akzeptiert; andernfalls stoppt NavBR mit einer Diagnose und erzwingt keine Schreibzugriffe."),
        ("Record", "● Fahrt aufnehmen"), ("StopSave", "■ Stoppen und speichern"), ("Play", "▶ Ghost 3D abspielen"), ("OpenFolder", "Ghost-Ordner öffnen"),
        ("NeedOmsi", "Vor der Aufnahme Karte und Bus in OMSI laden."), ("Saved", "Ghost gespeichert."), ("SaveFailed", "Ghost konnte nicht gespeichert werden."),
        ("ChooseFile", "NavBR Ghost auswählen"), ("AllFiles", "Alle Dateien"), ("Starting", "Ghost 3D wird gestartet…"), ("Completed", "Ghost abgeschlossen."),
        ("Interrupted", "Ghost unterbrochen."), ("Unavailable", "Ghost 3D ist in dieser Konfiguration nicht verfügbar."), ("Ready", "Aufnahmebereit."),
        ("Recording", "Aufnahme… {0:N0} Frames"), ("RecordingNote", "Die Aufnahme verwendet nur lokale NavBR-Telemetrie; es werden keine Werte in OMSI geschrieben."),
        ("Folder", "Ordner: {0}"), ("ReplayInfo", "Name: {0}\nKarte: {1}\nFahrzeug: {2}\nDauer: {3}\nFrames: {4:N0}\nDatei: {5}"));

    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Ghost / Replay — NavBR Alpha.12"), ("Menu", "Ghost / Replay"),
        ("MenuTip", "Enregistrer la télémétrie OMSI réelle et relire un Ghost Bus via le bridge expérimental."),
        ("Heading", "Ghost / Replay"), ("Subtitle", "Enregistrez un trajet réel de votre bus puis relisez-le avec le format Ghost versionné de NavBR."),
        ("Warning", "L’enregistrement est en lecture seule et sûr. La lecture physique Ghost 3D ne fonctionne que si le bridge OMSI expérimental accepte les commandes spawn/transform ; sinon NavBR s’arrête avec un diagnostic et ne force aucune écriture."),
        ("Record", "● Enregistrer le trajet"), ("StopSave", "■ Arrêter et enregistrer"), ("Play", "▶ Lire Ghost 3D"), ("OpenFolder", "Ouvrir le dossier Ghost"),
        ("NeedOmsi", "Chargez une carte et un bus dans OMSI avant d’enregistrer."), ("Saved", "Ghost enregistré."), ("SaveFailed", "Impossible d’enregistrer le Ghost."),
        ("ChooseFile", "Choisir un Ghost NavBR"), ("AllFiles", "Tous les fichiers"), ("Starting", "Démarrage du Ghost 3D…"), ("Completed", "Ghost terminé."),
        ("Interrupted", "Ghost interrompu."), ("Unavailable", "Ghost 3D indisponible dans cette configuration."), ("Ready", "Prêt à enregistrer."),
        ("Recording", "Enregistrement… {0:N0} images"), ("RecordingNote", "L’enregistrement utilise uniquement la télémétrie locale NavBR ; aucune valeur n’est écrite dans OMSI."),
        ("Folder", "Dossier : {0}"), ("ReplayInfo", "Nom : {0}\nCarte : {1}\nVéhicule : {2}\nDurée : {3}\nImages : {4:N0}\nFichier : {5}"));

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
