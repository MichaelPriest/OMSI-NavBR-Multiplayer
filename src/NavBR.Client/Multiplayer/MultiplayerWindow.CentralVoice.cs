using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _centralVoiceInstalled;
    private Border? _centralVoiceCard;
    private TextBlock? _centralVoiceTitle;
    private TextBlock? _centralVoiceDetail;
    private Button? _centralVoiceOpenButton;
    private DispatcherTimer? _centralVoiceTimer;

    [ModuleInitializer]
    internal static void InitializeCentralVoiceBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(CentralVoiceWindowLoaded));
    }

    private static void CentralVoiceWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallCentralVoiceCard),
            DispatcherPriority.ContextIdle);
    }

    private void InstallCentralVoiceCard()
    {
        if (_centralVoiceInstalled)
        {
            return;
        }

        var host = FindCentralVoiceAncestor<DockPanel>(PlayersHeadingText);
        var heading = FindCentralVoiceAncestor<Grid>(PlayersHeadingText);
        if (host is null || heading is null)
        {
            return;
        }

        _centralVoiceInstalled = true;

        _centralVoiceTitle = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 10.5d,
            FontWeight = FontWeights.SemiBold
        };
        _centralVoiceDetail = new TextBlock
        {
            Foreground = CentralVoiceBrush(116, 144, 163),
            FontSize = 9d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 3d, 8d, 0d)
        };

        var copy = new StackPanel();
        copy.Children.Add(_centralVoiceTitle);
        copy.Children.Add(_centralVoiceDetail);

        _centralVoiceOpenButton = new Button
        {
            MinWidth = 76d,
            Height = 31d,
            Padding = new Thickness(9d, 4d, 9d, 4d),
            Background = CentralVoiceBrush(11, 39, 58),
            Foreground = CentralVoiceBrush(190, 222, 243),
            BorderBrush = CentralVoiceBrush(37, 87, 119),
            BorderThickness = new Thickness(1d),
            FontSize = 9.2d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _centralVoiceOpenButton.Click += CentralVoiceOpenButton_Click;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(copy, 0);
        Grid.SetColumn(_centralVoiceOpenButton, 1);
        grid.Children.Add(copy);
        grid.Children.Add(_centralVoiceOpenButton);

        _centralVoiceCard = new Border
        {
            Background = CentralVoiceBrush(7, 25, 37),
            BorderBrush = CentralVoiceBrush(27, 54, 72),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Padding = new Thickness(10d, 8d, 10d, 8d),
            Margin = new Thickness(0d, 0d, 0d, 9d),
            Child = grid
        };
        DockPanel.SetDock(_centralVoiceCard, Dock.Top);

        var headingIndex = host.Children.IndexOf(heading);
        host.Children.Insert(Math.Max(0, headingIndex + 1), _centralVoiceCard);

        VoiceEnabledCheckBox.Checked += CentralVoiceEnabledChanged;
        VoiceEnabledCheckBox.Unchecked += CentralVoiceEnabledChanged;

        _centralVoiceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650d)
        };
        _centralVoiceTimer.Tick += CentralVoiceTimer_Tick;
        _centralVoiceTimer.Start();
        Closed += CentralVoiceWindowClosed;

        RenderCentralVoiceCard();
    }

    private void CentralVoiceOpenButton_Click(object sender, RoutedEventArgs e)
    {
        VoiceChannelButton_Click(sender, e);
        RenderCentralVoiceCard();
    }

    private void CentralVoiceEnabledChanged(object sender, RoutedEventArgs e) => RenderCentralVoiceCard();

    private void CentralVoiceTimer_Tick(object? sender, EventArgs e) => RenderCentralVoiceCard();

    private void CentralVoiceWindowClosed(object? sender, EventArgs e)
    {
        if (_centralVoiceTimer is not null)
        {
            _centralVoiceTimer.Stop();
            _centralVoiceTimer.Tick -= CentralVoiceTimer_Tick;
            _centralVoiceTimer = null;
        }

        VoiceEnabledCheckBox.Checked -= CentralVoiceEnabledChanged;
        VoiceEnabledCheckBox.Unchecked -= CentralVoiceEnabledChanged;
        if (_centralVoiceOpenButton is not null)
        {
            _centralVoiceOpenButton.Click -= CentralVoiceOpenButton_Click;
        }
        Closed -= CentralVoiceWindowClosed;
    }

    private void RenderCentralVoiceCard()
    {
        if (_centralVoiceCard is null ||
            _centralVoiceTitle is null ||
            _centralVoiceDetail is null ||
            _centralVoiceOpenButton is null)
        {
            return;
        }

        var enabled = VoiceEnabledCheckBox.IsChecked == true;
        var channel = CentralVoiceChannelLabel();
        var ptt = _voiceChat.IsPushToTalkActive;
        var quality = _voiceChat.GetQualitySnapshot();

        var accent = !enabled
            ? CentralVoiceBrush(104, 124, 138)
            : ptt
                ? CentralVoiceBrush(86, 220, 149)
                : CentralVoiceBrush(84, 170, 229);

        _centralVoiceCard.BorderBrush = accent;
        _centralVoiceTitle.Foreground = accent;
        _centralVoiceTitle.Text = !enabled
            ? CentralVoiceText("Voz desligada", "Voice off", "Voz desactivada", "Sprache aus", "Voix désactivée")
            : ptt
                ? CentralVoiceText($"Transmitindo • {channel}", $"Transmitting • {channel}", $"Transmitiendo • {channel}", $"Sendet • {channel}", $"Transmission • {channel}")
                : CentralVoiceText($"Voz • {channel}", $"Voice • {channel}", $"Voz • {channel}", $"Sprache • {channel}", $"Voix • {channel}");

        if (!enabled)
        {
            _centralVoiceDetail.Text = CentralVoiceText(
                "Ative a voz nas configurações da sala para usar PTT.",
                "Enable voice in room settings to use PTT.",
                "Activa la voz en la configuración de la sala para usar PTT.",
                "Aktivieren Sie Sprache in den Raumeinstellungen für PTT.",
                "Activez la voix dans les paramètres de la salle pour utiliser le PTT.");
        }
        else if (quality.ActiveStreams > 0)
        {
            _centralVoiceDetail.Text = CentralVoiceText(
                $"{quality.ActiveStreams} fluxo(s) • jitter {quality.AverageJitterMilliseconds:0} ms • perda {quality.EstimatedLossPercent:0.#}% • buffer {quality.TargetBufferMilliseconds} ms",
                $"{quality.ActiveStreams} stream(s) • jitter {quality.AverageJitterMilliseconds:0} ms • loss {quality.EstimatedLossPercent:0.#}% • buffer {quality.TargetBufferMilliseconds} ms",
                $"{quality.ActiveStreams} flujo(s) • jitter {quality.AverageJitterMilliseconds:0} ms • pérdida {quality.EstimatedLossPercent:0.#}% • búfer {quality.TargetBufferMilliseconds} ms",
                $"{quality.ActiveStreams} Stream(s) • Jitter {quality.AverageJitterMilliseconds:0} ms • Verlust {quality.EstimatedLossPercent:0.#}% • Puffer {quality.TargetBufferMilliseconds} ms",
                $"{quality.ActiveStreams} flux • jitter {quality.AverageJitterMilliseconds:0} ms • perte {quality.EstimatedLossPercent:0.#}% • tampon {quality.TargetBufferMilliseconds} ms");
        }
        else
        {
            _centralVoiceDetail.Text = _settings.VoiceDeafened
                ? CentralVoiceText(
                    "Áudio remoto silenciado.",
                    "Remote audio is deafened.",
                    "El audio remoto está silenciado.",
                    "Remote-Audio ist stummgeschaltet.",
                    "L’audio distant est mis en sourdine.")
                : CentralVoiceText(
                    "PTT pronto • a qualidade aparece quando outro motorista falar.",
                    "PTT ready • quality appears when another driver speaks.",
                    "PTT listo • la calidad aparece cuando otro conductor habla.",
                    "PTT bereit • Qualität erscheint, wenn ein anderer Fahrer spricht.",
                    "PTT prêt • la qualité apparaît lorsqu’un autre conducteur parle.");
        }

        _centralVoiceOpenButton.Content = CentralVoiceText("Configurar", "Configure", "Configurar", "Einstellen", "Configurer");
        _centralVoiceOpenButton.ToolTip = CentralVoiceText(
            "Escolher canal, dispositivos, proximidade, mute e volume por jogador.",
            "Choose channel, devices, proximity, mute and per-player volume.",
            "Elegir canal, dispositivos, proximidad, silencio y volumen por jugador.",
            "Kanal, Geräte, Nähe, Stummschaltung und Lautstärke pro Spieler wählen.",
            "Choisir le canal, les périphériques, la proximité, la sourdine et le volume par joueur.");
    }

    private string CentralVoiceChannelLabel() =>
        VoiceChannelSession.NormalizeChannel(_settings.VoiceChannel) switch
        {
            "company" => CentralVoiceText("Empresa", "Company", "Empresa", "Unternehmen", "Entreprise"),
            "dispatch" => CentralVoiceText("CCO", "Dispatcher", "CCO", "Leitstelle", "CCO"),
            "proximity" => CentralVoiceText(
                $"Proximidade {_settings.VoiceProximityMeters:0} m",
                $"Proximity {_settings.VoiceProximityMeters:0} m",
                $"Proximidad {_settings.VoiceProximityMeters:0} m",
                $"Nähe {_settings.VoiceProximityMeters:0} m",
                $"Proximité {_settings.VoiceProximityMeters:0} m"),
            _ => CentralVoiceText("Geral", "General", "General", "Allgemein", "Général")
        };

    private static T? FindCentralVoiceAncestor<T>(DependencyObject? element)
        where T : DependencyObject
    {
        var current = element;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static string CentralVoiceText(string pt, string en, string es, string de, string fr) =>
        Localization.LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static SolidColorBrush CentralVoiceBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
