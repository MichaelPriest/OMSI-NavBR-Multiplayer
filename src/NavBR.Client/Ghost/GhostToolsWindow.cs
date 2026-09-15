using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
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
        Title = "NavBR Ghost 3D — Alpha.11";
        Width = 690;
        Height = 430;
        MinWidth = 600;
        MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(7, 15, 24);
        Foreground = Brushes.White;
        Icon = Application.Current?.MainWindow?.Icon;

        _recordButton = CreateButton("● Gravar viagem", Record_Click, true);
        _stopButton = CreateButton("■ Parar e salvar", Stop_Click, false);
        _playButton = CreateButton("▶ Reproduzir Ghost 3D", Play_Click, false);

        Content = BuildContent();

        _recordTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
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

    private UIElement BuildContent()
    {
        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = "Ghost Bus 3D",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = "Grave uma viagem do seu ônibus e use a mesma ponte que será usada pelo multiplayer físico para reproduzi-la como veículo remoto.",
            Margin = new Thickness(0, 6, 0, 18),
            Foreground = Brush(173, 194, 215),
            TextWrapping = TextWrapping.Wrap
        });

        var warning = new Border
        {
            Background = Brush(33, 27, 16),
            BorderBrush = Brush(122, 86, 27),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new TextBlock
            {
                Text = "EXPERIMENTAL: a gravação é segura e somente leitura. A reprodução 3D só é ativada quando o plugin declara suporte a spawn/transform; caso contrário, o NavBR retorna um diagnóstico e não escreve no OMSI.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(245, 202, 126)
            }
        };
        root.Children.Add(warning);

        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        actions.Children.Add(_recordButton);
        actions.Children.Add(_stopButton);
        actions.Children.Add(_playButton);
        actions.Children.Add(CreateButton("Abrir pasta de Ghosts", OpenFolder_Click, false));
        root.Children.Add(actions);

        _status.FontSize = 16;
        _status.FontWeight = FontWeights.SemiBold;
        root.Children.Add(_status);

        _details.Margin = new Thickness(0, 8, 0, 0);
        _details.Foreground = Brush(173, 194, 215);
        _details.TextWrapping = TextWrapping.Wrap;
        _details.FontFamily = new FontFamily("Consolas");
        _details.FontSize = 11;
        root.Children.Add(_details);

        return root;
    }

    private Button CreateButton(string text, RoutedEventHandler handler, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 132,
            Height = 36,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 4, 12, 4),
            Background = primary ? Brush(255, 132, 0) : Brush(18, 38, 56),
            Foreground = primary ? Brushes.Black : Brushes.White,
            BorderBrush = primary ? Brush(255, 165, 54) : Brush(55, 83, 106),
            BorderThickness = new Thickness(1)
        };
        button.Click += handler;
        return button;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        var telemetry = _telemetrySource();
        if (telemetry is null || !telemetry.IsInGame)
        {
            _status.Text = "Carregue um mapa e um ônibus no OMSI antes de gravar.";
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
            _status.Text = "Ghost salvo.";
            _details.Text = path;
        }
        catch (Exception ex)
        {
            _recorder.Cancel();
            _status.Text = "Não foi possível salvar o Ghost.";
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
            Title = "Escolha um Ghost NavBR",
            Filter = $"NavBR Ghost (*{GhostReplayFormat.Extension})|*{GhostReplayFormat.Extension}|Todos os arquivos (*.*)|*.*",
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

        _status.Text = "Iniciando Ghost 3D…";
        _details.Text = dialog.FileName;
        RefreshUi(keepDetails: true);

        try
        {
            await _player.PlayAsync(dialog.FileName);
            _status.Text = "Ghost concluído.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Ghost interrompido.";
        }
        catch (Exception ex)
        {
            _status.Text = "Ghost 3D indisponível nesta configuração.";
            _details.Text = $"{dialog.FileName}\n\n{ex.Message}";
        }
        finally
        {
            RefreshUi(keepDetails: true);
        }
    }

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
        _status.Text = _recorder.IsRecording
            ? $"Gravando… {_recorder.FrameCount:N0} frames"
            : "Pronto.";
        _details.Text = _recorder.IsRecording
            ? "A gravação usa somente a telemetria local do NavBR; nenhum valor é escrito no OMSI."
            : _details.Text;
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
            _status.Text = "Pronto para gravar.";
        }

        if (!keepDetails && string.IsNullOrWhiteSpace(_details.Text))
        {
            _details.Text = $"Pasta: {GhostRecorder.GetGhostDirectory()}";
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
