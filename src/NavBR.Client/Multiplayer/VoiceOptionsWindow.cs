using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

internal sealed class VoiceOptionsWindow : Window
{
    private readonly ComboBox _channel = new();
    private readonly TextBox _distance = new();
    private readonly TextBlock _distanceLabel = new();
    private readonly TextBlock _hint = new();

    public string SelectedChannel { get; private set; }
    public double ProximityMeters { get; private set; }

    public VoiceOptionsWindow(string selectedChannel, double proximityMeters)
    {
        SelectedChannel = VoiceChannelSession.NormalizeChannel(selectedChannel);
        ProximityMeters = Math.Clamp(
            double.IsFinite(proximityMeters) ? proximityMeters : 120d,
            20d,
            1000d);

        Title = T("Configurações de voz", "Voice settings", "Configuración de voz", "Spracheinstellungen", "Paramètres vocaux");
        Width = 480;
        Height = 350;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(22) };
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

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
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

        Content = root;
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
}
