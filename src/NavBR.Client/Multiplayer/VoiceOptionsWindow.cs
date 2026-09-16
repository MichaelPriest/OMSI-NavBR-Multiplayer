using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

internal sealed class VoiceOptionsWindow : Window
{
    private readonly VoiceChatService _voiceChat;
    private readonly ComboBox _channel = new();
    private readonly TextBox _distance = new();
    private readonly TextBlock _distanceLabel = new();
    private readonly TextBlock _hint = new();
    private readonly CheckBox _deafen = new();
    private readonly ComboBox _player = new();
    private readonly CheckBox _mutePlayer = new();
    private readonly Slider _gain = new();
    private readonly TextBlock _gainValue = new();
    private bool _loadingPlayer;

    public string SelectedChannel { get; private set; }
    public double ProximityMeters { get; private set; }
    public bool Deafened { get; private set; }

    public VoiceOptionsWindow(
        string selectedChannel,
        double proximityMeters,
        bool deafened,
        IReadOnlyList<(string PlayerId, string DisplayName)> players,
        VoiceChatService voiceChat)
    {
        _voiceChat = voiceChat;
        SelectedChannel = VoiceChannelSession.NormalizeChannel(selectedChannel);
        ProximityMeters = Math.Clamp(
            double.IsFinite(proximityMeters) ? proximityMeters : 120d,
            20d,
            1000d);
        Deafened = deafened;

        Title = T("Configurações de voz", "Voice settings", "Configuración de voz", "Spracheinstellungen", "Paramètres vocaux");
        Width = 520;
        Height = 590;
        MinHeight = 520;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var root = new StackPanel { Margin = new Thickness(22) };
        scroll.Content = root;

        root.Children.Add(new TextBlock
        {
            Text = T("Canal de voz", "Voice channel", "Canal de voz", "Sprachkanal", "Canal vocal"),
            FontSize = 21,
            FontWeight = FontWeights.SemiBold
        });

        root.Children.Add(new TextBlock
        {
            Text = T(
                "O PTT continua o mesmo. Você transmite e escuta somente o canal selecionado.",
                "PTT stays the same. You transmit and hear only the selected channel.",
                "El PTT sigue igual. Transmites y escuchas solo el canal seleccionado.",
                "PTT bleibt unverändert. Gesendet und empfangen wird nur im gewählten Kanal.",
                "Le PTT reste identique. Vous émettez et écoutez uniquement le canal sélectionné."),
            Margin = new Thickness(0, 6, 0, 14),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });

        _channel.ItemsSource = new[]
        {
            new ChannelItem("general", T("Geral", "General", "General", "Allgemein", "Général")),
            new ChannelItem("company", T("Empresa / equipe", "Company / team", "Empresa / equipo", "Unternehmen / Team", "Entreprise / équipe")),
            new ChannelItem("dispatch", T("CCO / Dispatcher", "Control / Dispatcher", "CCO / Dispatcher", "Leitstelle / Dispatcher", "CCO / Dispatcher")),
            new ChannelItem("proximity", T("Proximidade", "Proximity", "Proximidad", "Nähe", "Proximité"))
        };
        _channel.DisplayMemberPath = nameof(ChannelItem.Label);
        _channel.SelectedValuePath = nameof(ChannelItem.Id);
        _channel.SelectedValue = SelectedChannel;
        _channel.SelectionChanged += (_, _) => RenderDistanceState();
        root.Children.Add(_channel);

        _distanceLabel.Text = T("Raio de proximidade (metros)", "Proximity radius (meters)", "Radio de proximidad (metros)", "Nähe-Radius (Meter)", "Rayon de proximité (mètres)");
        _distanceLabel.Margin = new Thickness(0, 16, 0, 5);
        root.Children.Add(_distanceLabel);

        _distance.Text = ProximityMeters.ToString("0", CultureInfo.InvariantCulture);
        _distance.MaxLength = 4;
        root.Children.Add(_distance);

        _hint.Margin = new Thickness(0, 6, 0, 0);
        _hint.TextWrapping = TextWrapping.Wrap;
        _hint.Opacity = 0.75;
        _hint.Text = T(
            "Faixa permitida: 20–1000 m. Proximidade exige o mesmo mapa e telemetria válida dos dois jogadores.",
            "Allowed range: 20–1000 m. Proximity requires the same map and valid telemetry from both players.",
            "Rango permitido: 20–1000 m. La proximidad requiere el mismo mapa y telemetría válida de ambos jugadores.",
            "Zulässiger Bereich: 20–1000 m. Nähe erfordert dieselbe Karte und gültige Telemetrie beider Spieler.",
            "Plage autorisée : 20–1000 m. La proximité exige la même carte et une télémétrie valide des deux joueurs.");
        root.Children.Add(_hint);

        _deafen.Content = T(
            "Silenciar toda recepção de voz (deafen)",
            "Mute all incoming voice (deafen)",
            "Silenciar toda la voz entrante (deafen)",
            "Gesamten Sprachempfang stummschalten (Deafen)",
            "Couper toute réception vocale (deafen)");
        _deafen.IsChecked = deafened;
        _deafen.Margin = new Thickness(0, 18, 0, 0);
        root.Children.Add(_deafen);

        var separator = new Border
        {
            Height = 1,
            Opacity = 0.25,
            Margin = new Thickness(0, 18, 0, 16),
            Background = System.Windows.Media.Brushes.Gray
        };
        root.Children.Add(separator);

        root.Children.Add(new TextBlock
        {
            Text = T("Mixer por jogador", "Player mixer", "Mezclador por jugador", "Spieler-Mixer", "Mixeur par joueur"),
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = T(
                "Mute e volume são ajustes temporários desta sessão.",
                "Mute and volume are temporary settings for this session.",
                "Mute y volumen son ajustes temporales de esta sesión.",
                "Stummschaltung und Lautstärke gelten nur für diese Sitzung.",
                "Le mute et le volume sont temporaires pour cette session."),
            Margin = new Thickness(0, 4, 0, 10),
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        });

        var playerItems = (players ?? Array.Empty<(string PlayerId, string DisplayName)>())
            .Where(player => !string.IsNullOrWhiteSpace(player.PlayerId))
            .OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(player => new PlayerItem(player.PlayerId, string.IsNullOrWhiteSpace(player.DisplayName) ? player.PlayerId : player.DisplayName))
            .ToArray();
        _player.ItemsSource = playerItems;
        _player.DisplayMemberPath = nameof(PlayerItem.DisplayName);
        _player.SelectionChanged += (_, _) => LoadSelectedPlayer();
        root.Children.Add(_player);

        _mutePlayer.Content = T("Mutar jogador", "Mute player", "Silenciar jugador", "Spieler stummschalten", "Couper le joueur");
        _mutePlayer.Margin = new Thickness(0, 10, 0, 0);
        _mutePlayer.Checked += (_, _) => ApplyPlayerMute(true);
        _mutePlayer.Unchecked += (_, _) => ApplyPlayerMute(false);
        root.Children.Add(_mutePlayer);

        var gainHeader = new Grid { Margin = new Thickness(0, 10, 0, 3) };
        gainHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gainHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gainHeader.Children.Add(new TextBlock
        {
            Text = T("Volume do jogador", "Player volume", "Volumen del jugador", "Spielerlautstärke", "Volume du joueur")
        });
        Grid.SetColumn(_gainValue, 1);
        gainHeader.Children.Add(_gainValue);
        root.Children.Add(gainHeader);

        _gain.Minimum = 0;
        _gain.Maximum = 2;
        _gain.SmallChange = 0.05;
        _gain.LargeChange = 0.25;
        _gain.TickFrequency = 0.25;
        _gain.IsSnapToTickEnabled = false;
        _gain.ValueChanged += (_, _) => ApplyPlayerGain();
        root.Children.Add(_gain);

        if (playerItems.Length > 0)
        {
            _player.SelectedIndex = 0;
        }
        else
        {
            _player.IsEnabled = false;
            _mutePlayer.IsEnabled = false;
            _gain.IsEnabled = false;
            _gainValue.Text = "100%";
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var cancel = new Button
        {
            Content = T("Cancelar", "Cancel", "Cancelar", "Abbrechen", "Annuler"),
            MinWidth = 100,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        actions.Children.Add(cancel);

        var save = new Button
        {
            Content = T("Aplicar", "Apply", "Aplicar", "Übernehmen", "Appliquer"),
            MinWidth = 110,
            IsDefault = true
        };
        save.Click += Save_Click;
        actions.Children.Add(save);
        root.Children.Add(actions);

        Content = scroll;
        RenderDistanceState();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var channel = VoiceChannelSession.NormalizeChannel(_channel.SelectedValue?.ToString());
        var distance = ProximityMeters;
        if (channel == "proximity")
        {
            if (!double.TryParse(
                    _distance.Text.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out distance) ||
                !double.IsFinite(distance) ||
                distance is < 20d or > 1000d)
            {
                MessageBox.Show(
                    T(
                        "Informe um raio entre 20 e 1000 metros.",
                        "Enter a radius between 20 and 1000 meters.",
                        "Introduzca un radio entre 20 y 1000 metros.",
                        "Einen Radius zwischen 20 und 1000 Metern eingeben.",
                        "Saisissez un rayon entre 20 et 1000 mètres."),
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                _distance.Focus();
                return;
            }
        }

        SelectedChannel = channel;
        ProximityMeters = Math.Clamp(distance, 20d, 1000d);
        Deafened = _deafen.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void RenderDistanceState()
    {
        var enabled = VoiceChannelSession.NormalizeChannel(_channel.SelectedValue?.ToString()) == "proximity";
        _distanceLabel.IsEnabled = enabled;
        _distance.IsEnabled = enabled;
        _hint.IsEnabled = enabled;
    }

    private void LoadSelectedPlayer()
    {
        if (_player.SelectedItem is not PlayerItem player)
        {
            return;
        }

        _loadingPlayer = true;
        try
        {
            _mutePlayer.IsChecked = _voiceChat.IsRemoteMuted(player.PlayerId);
            _gain.Value = _voiceChat.GetRemoteGain(player.PlayerId);
            _gainValue.Text = $"{_gain.Value * 100d:0}%";
        }
        finally
        {
            _loadingPlayer = false;
        }
    }

    private void ApplyPlayerMute(bool muted)
    {
        if (_loadingPlayer || _player.SelectedItem is not PlayerItem player)
        {
            return;
        }

        _voiceChat.SetRemoteMuted(player.PlayerId, muted);
    }

    private void ApplyPlayerGain()
    {
        _gainValue.Text = $"{_gain.Value * 100d:0}%";
        if (_loadingPlayer || _player.SelectedItem is not PlayerItem player)
        {
            return;
        }

        _voiceChat.SetRemoteGain(player.PlayerId, _gain.Value);
    }

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private sealed record ChannelItem(string Id, string Label);
    private sealed record PlayerItem(string PlayerId, string DisplayName);
}
