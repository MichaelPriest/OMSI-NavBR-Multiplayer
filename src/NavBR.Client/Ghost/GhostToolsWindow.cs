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
    private readonly Button _previewButton;
    private readonly Button _compareButton;
    private readonly Button _playButton;
    private readonly Button _exportButton;
    private readonly ComboBox _speedCombo;
    private readonly CheckBox _loopCheck;

    public GhostToolsWindow(Func<VehicleTelemetry?> telemetrySource)
    {
        _telemetrySource = telemetrySource;
        Title = "NavBR Ghost / Replay — Alpha.12";
        Width = 900d;
        Height = 620d;
        MinWidth = 740d;
        MinHeight = 540d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        FontFamily = new FontFamily("Segoe UI");
        Icon = Application.Current?.MainWindow?.Icon;

        _recordButton = CreateButton("Gravar viagem", Record_Click, primary: true);
        _stopButton = CreateButton("Parar e salvar", Stop_Click, primary: false);
        _previewButton = CreateButton("Visualizar trajeto", Preview_Click, primary: false);
        _compareButton = CreateButton("Comparar replays", Compare_Click, primary: false);
        _playButton = CreateButton("Reproduzir Ghost 3D", Play_Click, primary: true);
        _exportButton = CreateButton("Exportar cópia", ExportCopy_Click, primary: false);

        _speedCombo = new ComboBox
        {
            Width = 92d,
            Height = 36d,
            ItemsSource = new[] { 0.5d, 1d, 1.5d, 2d },
            SelectedItem = 1d,
            Background = Brush(13, 26, 36),
            Foreground = Brush(218, 230, 238),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            ToolTip = "Velocidade da reprodução"
        };
        _loopCheck = new CheckBox
        {
            Content = "Repetir",
            Foreground = Brush(218, 230, 238),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12d, 0d, 0d, 0d)
        };

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

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new Grid { Margin = new Thickness(0d, 0d, 0d, 18d) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingText = new StackPanel();
        headingText.Children.Add(new TextBlock
        {
            Text = "Ghost / Replay",
            FontSize = 26d,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(218, 230, 238)
        });
        headingText.Children.Add(new TextBlock
        {
            Text = "Grave a telemetria real da sua viagem, analise e compare replays localmente ou reproduza pelo bridge físico experimental.",
            Margin = new Thickness(0d, 5d, 18d, 0d),
            Foreground = Brush(151, 171, 185),
            FontSize = 12d,
            TextWrapping = TextWrapping.Wrap
        });
        heading.Children.Add(headingText);
        var badge = new Border
        {
            Padding = new Thickness(11d, 6d, 11d, 6d),
            Background = Brush(13, 26, 36),
            BorderBrush = Brush(61, 137, 196),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "EXPERIMENTAL",
                Foreground = Brush(113, 198, 255),
                FontSize = 9d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(badge, 1);
        heading.Children.Add(badge);
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var warning = new Border
        {
            Background = Brush(31, 27, 16),
            BorderBrush = Brush(109, 82, 29),
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(15d),
            CornerRadius = new CornerRadius(12d),
            Margin = new Thickness(0d, 0d, 0d, 16d),
            Child = new TextBlock
            {
                Text = "Gravação, análise, comparação e exportação são locais. A reprodução 3D só envia comandos quando o plugin experimental confirma suporte a spawn/transform; sem suporte, o NavBR interrompe o replay e não escreve no OMSI.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(242, 184, 75),
                FontSize = 11.5d
            }
        };
        Grid.SetRow(warning, 1);
        root.Children.Add(warning);

        var controlsCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(16d),
            Margin = new Thickness(0d, 0d, 0d, 16d)
        };
        var controls = new StackPanel();
        var actions = new WrapPanel();
        actions.Children.Add(_recordButton);
        actions.Children.Add(_stopButton);
        actions.Children.Add(_previewButton);
        actions.Children.Add(_compareButton);
        actions.Children.Add(_playButton);
        actions.Children.Add(_exportButton);
        actions.Children.Add(CreateButton("Abrir pasta de Ghosts", OpenFolder_Click, primary: false));
        controls.Children.Add(actions);

        var playbackOptions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 10d, 0d, 0d),
            VerticalAlignment = VerticalAlignment.Center
        };
        playbackOptions.Children.Add(new TextBlock
        {
            Text = "Velocidade",
            Foreground = Brush(151, 171, 185),
            FontSize = 11d,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 8d, 0d)
        });
        playbackOptions.Children.Add(_speedCombo);
        playbackOptions.Children.Add(_loopCheck);
        controls.Children.Add(playbackOptions);
        controlsCard.Child = controls;
        Grid.SetRow(controlsCard, 2);
        root.Children.Add(controlsCard);

        var statusCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(18d)
        };
        var statusStack = new StackPanel();
        statusStack.Children.Add(new TextBlock
        {
            Text = "ESTADO",
            Foreground = Brush(151, 171, 185),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 0d, 0d, 8d)
        });
        _status.FontSize = 16d;
        _status.FontWeight = FontWeights.SemiBold;
        _status.Foreground = Brush(218, 230, 238);
        statusStack.Children.Add(_status);
        _details.Margin = new Thickness(0d, 9d, 0d, 0d);
        _details.Foreground = Brush(151, 171, 185);
        _details.TextWrapping = TextWrapping.Wrap;
        _details.FontFamily = new FontFamily("Consolas");
        _details.FontSize = 10.5d;
        statusStack.Children.Add(_details);
        statusCard.Child = statusStack;
        Grid.SetRow(statusCard, 3);
        root.Children.Add(statusCard);

        return root;
    }

    private static Button CreateButton(string text, RoutedEventHandler handler, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 136d,
            Height = 38d,
            Margin = new Thickness(0d, 0d, 8d, 8d),
            Padding = new Thickness(14d, 6d, 14d, 6d),
            Background = primary ? Brush(61, 137, 196) : Brush(13, 26, 36),
            Foreground = Brushes.White,
            BorderBrush = primary ? Brush(113, 198, 255) : Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
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

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateOpenDialog("Escolha um Ghost NavBR para visualizar");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _status.Text = "Carregando pré-visualização…";
        _details.Text = dialog.FileName;
        RefreshUi(keepDetails: true);

        try
        {
            var document = await _player.LoadAsync(dialog.FileName);
            var preview = new GhostReplayPreviewWindow(document, dialog.FileName)
            {
                Owner = this
            };
            _status.Text = "Replay carregado para pré-visualização.";
            preview.ShowDialog();
        }
        catch (Exception ex)
        {
            _status.Text = "Não foi possível abrir o replay.";
            _details.Text = $"{dialog.FileName}\n\n{ex.Message}";
        }
        finally
        {
            RefreshUi(keepDetails: true);
        }
    }

    private async void Compare_Click(object sender, RoutedEventArgs e)
    {
        var firstDialog = CreateOpenDialog("Escolha o Replay A");
        if (firstDialog.ShowDialog(this) != true)
        {
            return;
        }

        var secondDialog = CreateOpenDialog("Escolha o Replay B");
        if (secondDialog.ShowDialog(this) != true)
        {
            return;
        }

        _status.Text = "Carregando comparação…";
        _details.Text = $"A: {firstDialog.FileName}\nB: {secondDialog.FileName}";
        RefreshUi(keepDetails: true);

        try
        {
            var left = await _player.LoadAsync(firstDialog.FileName);
            var right = await _player.LoadAsync(secondDialog.FileName);
            var comparison = new GhostReplayComparisonWindow(
                left,
                firstDialog.FileName,
                right,
                secondDialog.FileName)
            {
                Owner = this
            };
            _status.Text = "Comparação carregada.";
            comparison.ShowDialog();
        }
        catch (Exception ex)
        {
            _status.Text = "Não foi possível comparar os replays.";
            _details.Text = $"{firstDialog.FileName}\n{secondDialog.FileName}\n\n{ex.Message}";
        }
        finally
        {
            RefreshUi(keepDetails: true);
        }
    }

    private async void ExportCopy_Click(object sender, RoutedEventArgs e)
    {
        var sourceDialog = CreateOpenDialog("Escolha o Ghost NavBR para exportar");
        if (sourceDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _player.LoadAsync(sourceDialog.FileName);
        }
        catch (Exception ex)
        {
            _status.Text = "O arquivo selecionado não é um replay NavBR válido.";
            _details.Text = ex.Message;
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Exportar cópia do Ghost NavBR",
            Filter = $"NavBR Ghost (*{GhostReplayFormat.Extension})|*{GhostReplayFormat.Extension}",
            FileName = Path.GetFileName(sourceDialog.FileName),
            AddExtension = true,
            DefaultExt = GhostReplayFormat.Extension,
            OverwritePrompt = true
        };
        if (saveDialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var source = Path.GetFullPath(sourceDialog.FileName);
            var destination = Path.GetFullPath(saveDialog.FileName);
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                _status.Text = "O destino escolhido é o próprio arquivo original.";
                _details.Text = source;
                return;
            }

            File.Copy(source, destination, overwrite: true);
            _status.Text = "Cópia do replay exportada.";
            _details.Text = destination;
        }
        catch (Exception ex)
        {
            _status.Text = "Não foi possível exportar a cópia do replay.";
            _details.Text = ex.Message;
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateOpenDialog("Escolha um Ghost NavBR");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _recordTimer.Stop();
        if (_recorder.IsRecording)
        {
            _recorder.Cancel();
        }

        var speed = _speedCombo.SelectedItem is double selectedSpeed ? selectedSpeed : 1d;
        var loop = _loopCheck.IsChecked == true;
        _status.Text = $"Iniciando Ghost 3D • {speed:0.#}x{(loop ? " • loop" : string.Empty)}";
        _details.Text = dialog.FileName;
        RefreshUi(keepDetails: true);

        try
        {
            await _player.PlayAsync(dialog.FileName, speed, loop);
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

    private static OpenFileDialog CreateOpenDialog(string title)
    {
        return new OpenFileDialog
        {
            Title = title,
            Filter = $"NavBR Ghost (*{GhostReplayFormat.Extension})|*{GhostReplayFormat.Extension}|Todos os arquivos (*.*)|*.*",
            InitialDirectory = Directory.Exists(GhostRecorder.GetGhostDirectory())
                ? GhostRecorder.GetGhostDirectory()
                : null
        };
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
            ? $"Gravando • {_recorder.FrameCount:N0} frames"
            : "Pronto.";
        _details.Text = _recorder.IsRecording
            ? "A gravação usa somente a telemetria local real do NavBR; nenhum valor é escrito no OMSI."
            : _details.Text;
    }

    private void RefreshUi(bool keepDetails = false)
    {
        _recordButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _stopButton.IsEnabled = _recorder.IsRecording;
        _previewButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _compareButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _playButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _exportButton.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _speedCombo.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;
        _loopCheck.IsEnabled = !_recorder.IsRecording && !_player.IsPlaying;

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
