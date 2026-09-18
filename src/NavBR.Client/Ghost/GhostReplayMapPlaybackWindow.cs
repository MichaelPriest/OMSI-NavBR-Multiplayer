using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NavBR.Client.Ghost;

internal sealed class GhostReplayMapPlaybackWindow : Window
{
    private const double CanvasWidth = 1000d;
    private const double CanvasHeight = 620d;
    private const double Padding = 42d;

    private readonly GhostReplayFrame[] _frames;
    private readonly Canvas _canvas = new();
    private readonly Ellipse _vehicleMarker = new();
    private readonly ProgressBar _progress = new();
    private readonly TextBlock _timeText = new();
    private readonly TextBlock _statusText = new();
    private readonly Button _playPauseButton;
    private readonly ComboBox _speedCombo;
    private readonly DispatcherTimer _timer;

    private double _sourceMilliseconds;
    private DateTimeOffset _lastTickUtc;
    private bool _playing;
    private double _minX;
    private double _minZ;
    private double _scale;
    private double _offsetX;
    private double _offsetY;

    public GhostReplayMapPlaybackWindow(GhostReplayDocument document)
    {
        _frames = document.Frames
            .Where(frame => double.IsFinite(frame.Telemetry.X) && double.IsFinite(frame.Telemetry.Z))
            .ToArray();

        Title = "NavBR Ghost / Replay — Reprodução local";
        Width = 1120d;
        Height = 820d;
        MinWidth = 860d;
        MinHeight = 640d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brush(218, 230, 238);
        FontFamily = new FontFamily("Segoe UI");
        Icon = Application.Current?.MainWindow?.Icon;

        _playPauseButton = Button("Reproduzir", PlayPause_Click, primary: true);
        _speedCombo = new ComboBox
        {
            Width = 90d,
            Height = 36d,
            ItemsSource = new[] { 0.5d, 1d, 1.5d, 2d },
            SelectedItem = 1d,
            Background = Brush(13, 26, 36),
            Foreground = Brush(218, 230, 238),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d)
        };

        Content = BuildContent(document.Metadata.Name);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33d) };
        _timer.Tick += Timer_Tick;
        Closed += (_, _) => _timer.Stop();

        InitializeRoute();
        UpdatePlaybackUi();
    }

    private UIElement BuildContent(string replayName)
    {
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 16d) };
        heading.Children.Add(new TextBlock
        {
            Text = replayName,
            FontSize = 24d,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(218, 230, 238),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Reprodução local do replay • somente leitura • nenhum comando é enviado ao OMSI",
            Margin = new Thickness(0d, 5d, 0d, 0d),
            Foreground = Brush(151, 171, 185),
            FontSize = 11.5d
        });
        root.Children.Add(heading);

        var controlsCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(14d),
            Margin = new Thickness(0d, 0d, 0d, 16d)
        };
        var controls = new Grid();
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.Children.Add(_playPauseButton);
        var restart = Button("Reiniciar", Restart_Click, primary: false);
        restart.Margin = new Thickness(8d, 0d, 0d, 0d);
        Grid.SetColumn(restart, 1);
        controls.Children.Add(restart);
        _speedCombo.Margin = new Thickness(12d, 0d, 0d, 0d);
        Grid.SetColumn(_speedCombo, 2);
        controls.Children.Add(_speedCombo);
        _timeText.Foreground = Brush(218, 230, 238);
        _timeText.FontSize = 12d;
        _timeText.FontWeight = FontWeights.SemiBold;
        _timeText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_timeText, 4);
        controls.Children.Add(_timeText);
        controlsCard.Child = controls;
        Grid.SetRow(controlsCard, 1);
        root.Children.Add(controlsCard);

        var playbackCard = new Border
        {
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(28, 42, 51),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(14d)
        };
        var playbackGrid = new Grid();
        playbackGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        playbackGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _canvas.Width = CanvasWidth;
        _canvas.Height = CanvasHeight;
        _canvas.Background = Brush(7, 18, 28);
        _canvas.ClipToBounds = true;
        var viewbox = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            Child = _canvas
        };
        playbackGrid.Children.Add(viewbox);

        var footer = new StackPanel { Margin = new Thickness(0d, 12d, 0d, 0d) };
        _progress.Minimum = 0d;
        _progress.Maximum = 1d;
        _progress.Height = 5d;
        _progress.Background = Brush(28, 42, 51);
        _progress.Foreground = Brush(113, 198, 255);
        footer.Children.Add(_progress);
        _statusText.Margin = new Thickness(0d, 7d, 0d, 0d);
        _statusText.Foreground = Brush(151, 171, 185);
        _statusText.FontSize = 10d;
        footer.Children.Add(_statusText);
        Grid.SetRow(footer, 1);
        playbackGrid.Children.Add(footer);
        playbackCard.Child = playbackGrid;
        Grid.SetRow(playbackCard, 2);
        root.Children.Add(playbackCard);
        return root;
    }

    private void InitializeRoute()
    {
        if (_frames.Length < 2)
        {
            _playPauseButton.IsEnabled = false;
            _statusText.Text = "Replay sem coordenadas suficientes para reprodução local.";
            return;
        }

        _minX = _frames.Min(frame => frame.Telemetry.X);
        var maxX = _frames.Max(frame => frame.Telemetry.X);
        _minZ = _frames.Min(frame => frame.Telemetry.Z);
        var maxZ = _frames.Max(frame => frame.Telemetry.Z);
        var spanX = Math.Max(1d, maxX - _minX);
        var spanZ = Math.Max(1d, maxZ - _minZ);
        _scale = Math.Min(
            (CanvasWidth - (Padding * 2d)) / spanX,
            (CanvasHeight - (Padding * 2d)) / spanZ);
        _offsetX = (CanvasWidth - (spanX * _scale)) / 2d;
        _offsetY = (CanvasHeight - (spanZ * _scale)) / 2d;

        var points = new PointCollection();
        var step = Math.Max(1, _frames.Length / 4000);
        for (var index = 0; index < _frames.Length; index += step)
        {
            points.Add(ToCanvas(_frames[index].Telemetry.X, _frames[index].Telemetry.Z));
        }
        if ((_frames.Length - 1) % step != 0)
        {
            points.Add(ToCanvas(_frames[^1].Telemetry.X, _frames[^1].Telemetry.Z));
        }

        _canvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = Brush(61, 137, 196),
            StrokeThickness = 4d,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0.7d
        });

        _vehicleMarker.Width = 18d;
        _vehicleMarker.Height = 18d;
        _vehicleMarker.Fill = Brush(113, 198, 255);
        _vehicleMarker.Stroke = Brushes.White;
        _vehicleMarker.StrokeThickness = 2d;
        _canvas.Children.Add(_vehicleMarker);
        MoveMarker(_frames[0].Telemetry.X, _frames[0].Telemetry.Z);
        _statusText.Text = "Pronto para reprodução local.";
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Length < 2)
        {
            return;
        }

        if (_playing)
        {
            _playing = false;
            _timer.Stop();
            UpdatePlaybackUi();
            return;
        }

        if (_sourceMilliseconds >= DurationMilliseconds())
        {
            _sourceMilliseconds = 0d;
        }
        _playing = true;
        _lastTickUtc = DateTimeOffset.UtcNow;
        _timer.Start();
        UpdatePlaybackUi();
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        _playing = false;
        _timer.Stop();
        _sourceMilliseconds = 0d;
        UpdateMarkerForTime();
        UpdatePlaybackUi();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_playing || _frames.Length < 2)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = Math.Max(0d, (now - _lastTickUtc).TotalMilliseconds);
        _lastTickUtc = now;
        var speed = _speedCombo.SelectedItem is double selected ? selected : 1d;
        _sourceMilliseconds += elapsed * speed;
        var duration = DurationMilliseconds();
        if (_sourceMilliseconds >= duration)
        {
            _sourceMilliseconds = duration;
            _playing = false;
            _timer.Stop();
        }

        UpdateMarkerForTime();
        UpdatePlaybackUi();
    }

    private void UpdateMarkerForTime()
    {
        if (_frames.Length < 2)
        {
            return;
        }

        var upperIndex = FindUpperFrameIndex(_sourceMilliseconds);
        var lowerIndex = Math.Max(0, upperIndex - 1);
        var lower = _frames[lowerIndex];
        var upper = _frames[Math.Min(upperIndex, _frames.Length - 1)];
        var denominator = upper.OffsetMilliseconds - lower.OffsetMilliseconds;
        var t = denominator <= 0
            ? 0d
            : Math.Clamp((_sourceMilliseconds - lower.OffsetMilliseconds) / denominator, 0d, 1d);
        var x = lower.Telemetry.X + ((upper.Telemetry.X - lower.Telemetry.X) * t);
        var z = lower.Telemetry.Z + ((upper.Telemetry.Z - lower.Telemetry.Z) * t);
        MoveMarker(x, z);
    }

    private int FindUpperFrameIndex(double sourceMilliseconds)
    {
        var low = 0;
        var high = _frames.Length - 1;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (_frames[mid].OffsetMilliseconds < sourceMilliseconds)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }

    private void MoveMarker(double x, double z)
    {
        var point = ToCanvas(x, z);
        Canvas.SetLeft(_vehicleMarker, point.X - (_vehicleMarker.Width / 2d));
        Canvas.SetTop(_vehicleMarker, point.Y - (_vehicleMarker.Height / 2d));
    }

    private Point ToCanvas(double x, double z) => new(
        _offsetX + ((x - _minX) * _scale),
        CanvasHeight - (_offsetY + ((z - _minZ) * _scale)));

    private void UpdatePlaybackUi()
    {
        var duration = DurationMilliseconds();
        _progress.Value = duration <= 0d ? 0d : Math.Clamp(_sourceMilliseconds / duration, 0d, 1d);
        _playPauseButton.Content = _playing ? "Pausar" : "Reproduzir";
        _timeText.Text = $"{FormatTime(_sourceMilliseconds)} / {FormatTime(duration)}";
        if (_frames.Length >= 2)
        {
            _statusText.Text = _playing
                ? "Reprodução local em andamento — nenhum comando é enviado ao OMSI."
                : _sourceMilliseconds >= duration && duration > 0d
                    ? "Reprodução local concluída."
                    : "Reprodução local pausada/pronta.";
        }
    }

    private double DurationMilliseconds() =>
        _frames.Length < 2 ? 0d : Math.Max(0d, _frames[^1].OffsetMilliseconds);

    private static string FormatTime(double milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(0d, milliseconds));
        return duration.TotalHours >= 1d
            ? duration.ToString("hh\\:mm\\:ss")
            : duration.ToString("mm\\:ss");
    }

    private static Button Button(string text, RoutedEventHandler handler, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 112d,
            Height = 36d,
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

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
