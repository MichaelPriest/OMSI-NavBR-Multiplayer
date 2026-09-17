using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class RoleplayCharacterWindow : Window
{
    private readonly Func<IReadOnlyList<RoleplayCharacterOption>> _optionsSource;
    private readonly Func<string?> _mapKeySource;
    private readonly Func<bool> _mapReadySource;
    private readonly RoleplayCharacterController _controller;
    private readonly DispatcherTimer _refreshTimer;

    private readonly CheckBox _enabledCheckBox = new();
    private readonly TextBlock _mapState = new();
    private readonly TextBlock _selectionState = new();
    private readonly TextBlock _runtimeState = new();
    private readonly Button _selectButton = new();
    private readonly Button _controlButton = new();

    private bool _selectorOpen;
    private string? _lastMapKey;
    private string? _promptedMapKey;

    public RoleplayCharacterWindow(
        Func<IReadOnlyList<RoleplayCharacterOption>> optionsSource,
        Func<string?> mapKeySource,
        Func<bool> mapReadySource,
        RoleplayCharacterController controller)
    {
        _optionsSource = optionsSource;
        _mapKeySource = mapKeySource;
        _mapReadySource = mapReadySource;
        _controller = controller;

        Title = T(
            "Personagem / RP",
            "Character / RP",
            "Personaje / RP",
            "Charakter / RP",
            "Personnage / RP");
        Width = 720d;
        Height = 580d;
        MinWidth = 620d;
        MinHeight = 500d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 16, 26);
        Foreground = Brushes.White;

        Content = BuildContent();

        _controller.StateChanged += Controller_StateChanged;
        _controller.StatusChanged += Controller_StatusChanged;
        RoleplayCharacterSelectionStore.Changed += SelectionStore_Changed;

        Loaded += (_, _) => Refresh(openSelectorWhenReady: true);
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _controller.StateChanged -= Controller_StateChanged;
            _controller.StatusChanged -= Controller_StatusChanged;
            RoleplayCharacterSelectionStore.Changed -= SelectionStore_Changed;
        };

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(750d)
        };
        _refreshTimer.Tick += (_, _) => Refresh(openSelectorWhenReady: true);
        _refreshTimer.Start();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = T(
                "PERSONAGEM DO MOTORISTA",
                "DRIVER CHARACTER",
                "PERSONAJE DEL CONDUCTOR",
                "FAHRERCHARAKTER",
                "PERSONNAGE CONDUCTEUR"),
            Foreground = Brush(113, 198, 255),
            FontSize = 11d,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Text = T(
                "Use no modo normal ou multiplayer. O personagem é o motorista humano real do seu ônibus no OMSI.",
                "Use it in normal mode or multiplayer. The character is the real human driver of your bus in OMSI.",
                "Úsalo en modo normal o multijugador. El personaje es el conductor humano real de tu autobús en OMSI.",
                "Im normalen Modus oder Multiplayer nutzbar. Der Charakter ist der echte menschliche Fahrer deines OMSI-Busses.",
                "Utilisable en mode normal ou multijoueur. Le personnage est le vrai conducteur humain de votre bus OMSI."),
            Margin = new Thickness(0d, 6d, 0d, 18d),
            Foreground = Brush(151, 171, 185),
            FontSize = 12d,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var statusCard = Card();
        var status = (StackPanel)statusCard.Child;
        _enabledCheckBox.Content = T(
            "Ativar Personagem / RP (EXPERIMENTAL)",
            "Enable Character / RP (EXPERIMENTAL)",
            "Activar Personaje / RP (EXPERIMENTAL)",
            "Charakter / RP aktivieren (EXPERIMENTELL)",
            "Activer Personnage / RP (EXPÉRIMENTAL)");
        _enabledCheckBox.IsChecked = ExperimentalFeatureFlags.RoleplayCharacterEnabled;
        _enabledCheckBox.FontWeight = FontWeights.SemiBold;
        _enabledCheckBox.Click += EnabledCheckBox_Click;
        status.Children.Add(_enabledCheckBox);

        _mapState.Margin = new Thickness(0d, 10d, 0d, 0d);
        _mapState.Foreground = Brush(190, 205, 215);
        _mapState.FontSize = 11d;
        _mapState.TextWrapping = TextWrapping.Wrap;
        status.Children.Add(_mapState);

        _selectionState.Margin = new Thickness(0d, 5d, 0d, 0d);
        _selectionState.Foreground = Brush(151, 171, 185);
        _selectionState.FontSize = 11d;
        _selectionState.TextWrapping = TextWrapping.Wrap;
        status.Children.Add(_selectionState);

        Grid.SetRow(statusCard, 1);
        root.Children.Add(statusCard);

        var actionsCard = Card();
        actionsCard.Margin = new Thickness(0d, 14d, 0d, 0d);
        var actionsBody = (StackPanel)actionsCard.Child;
        actionsBody.Children.Add(Label(T(
            "PERSONAGEM",
            "CHARACTER",
            "PERSONAJE",
            "CHARAKTER",
            "PERSONNAGE")));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 10d, 0d, 0d)
        };

        StyleButton(_selectButton, false);
        _selectButton.Content = T(
            "Selecionar personagem",
            "Select character",
            "Seleccionar personaje",
            "Charakter auswählen",
            "Sélectionner le personnage");
        _selectButton.Click += (_, _) => OpenSelector();
        buttons.Children.Add(_selectButton);

        StyleButton(_controlButton, true);
        _controlButton.Margin = new Thickness(10d, 0d, 0d, 0d);
        _controlButton.Click += ControlButton_Click;
        buttons.Children.Add(_controlButton);

        actionsBody.Children.Add(buttons);

        _runtimeState.Margin = new Thickness(0d, 12d, 0d, 0d);
        _runtimeState.Foreground = Brush(190, 205, 215);
        _runtimeState.FontSize = 11d;
        _runtimeState.TextWrapping = TextWrapping.Wrap;
        actionsBody.Children.Add(_runtimeState);

        Grid.SetRow(actionsCard, 2);
        root.Children.Add(actionsCard);

        var helpCard = Card();
        helpCard.Margin = new Thickness(0d, 14d, 0d, 0d);
        var help = (StackPanel)helpCard.Child;
        help.Children.Add(Label(T(
            "CONTROLES DO MODO A PÉ",
            "ON-FOOT CONTROLS",
            "CONTROLES A PIE",
            "STEUERUNG ZU FUSS",
            "CONTRÔLES À PIED")));
        help.Children.Add(BodyText(T(
            "W / S: frente e trás   •   A / D: girar   •   Shift: correr   •   Esc: voltar ao ônibus\n\nNesta primeira Alpha o deslocamento é limitado a uma área próxima ao ônibus e usa a altura atual do motorista. Terreno inclinado, câmera dedicada e animações avançadas ainda precisam de validação no OMSI.",
            "W / S: forward/back   •   A / D: turn   •   Shift: run   •   Esc: return to bus\n\nIn this first Alpha movement is limited to an area near the bus and keeps the driver's current height. Sloped terrain, a dedicated camera and advanced animations still require OMSI validation.",
            "W / S: avanzar/retroceder   •   A / D: girar   •   Shift: correr   •   Esc: volver al autobús\n\nEn esta primera Alpha el movimiento queda limitado cerca del autobús y mantiene la altura actual del conductor.",
            "W / S: vor/zurück   •   A / D: drehen   •   Shift: laufen   •   Esc: zurück zum Bus\n\nIn dieser ersten Alpha bleibt die Bewegung in Busnähe und behält die aktuelle Höhe des Fahrers.",
            "W / S : avancer/reculer   •   A / D : tourner   •   Shift : courir   •   Esc : retour au bus\n\nDans cette première Alpha le déplacement reste proche du bus et conserve la hauteur actuelle du conducteur.")));
        Grid.SetRow(helpCard, 3);
        root.Children.Add(helpCard);

        var close = new Button
        {
            Content = T("Fechar", "Close", "Cerrar", "Schließen", "Fermer"),
            Padding = new Thickness(18d, 8d, 18d, 8d),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brush(13, 26, 36),
            Foreground = Brushes.White,
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            FontWeight = FontWeights.SemiBold
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 4);
        root.Children.Add(close);

        return root;
    }

    private async void EnabledCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var enabled = _enabledCheckBox.IsChecked == true;
        ExperimentalFeatureFlags.SetRoleplayCharacterEnabled(enabled);

        var settings = MultiplayerSettingsStore.Load() with
        {
            ExperimentalRoleplayCharacterEnabled = enabled
        };
        MultiplayerSettingsStore.Save(settings);

        if (!enabled && _controller.IsActive)
        {
            await _controller.StopAsync("roleplay-disabled");
        }

        Refresh(openSelectorWhenReady: enabled);
    }

    private async void ControlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.IsActive)
        {
            await _controller.StopAsync("roleplay-returned-to-bus");
        }
        else
        {
            _ = await _controller.StartAsync();
        }

        Refresh(openSelectorWhenReady: false);
    }

    private void Refresh(bool openSelectorWhenReady)
    {
        var mapKey = _mapKeySource();
        var mapReady = _mapReadySource();
        var enabled = ExperimentalFeatureFlags.RoleplayCharacterEnabled;

        if (!string.Equals(_lastMapKey, mapKey, StringComparison.OrdinalIgnoreCase))
        {
            _lastMapKey = mapKey;
            _promptedMapKey = null;
            RoleplayCharacterSelectionStore.ResetForMap(mapKey);
        }

        var options = enabled && mapReady
            ? SafeReadOptions()
            : Array.Empty<RoleplayCharacterOption>();
        var selected = RoleplayCharacterSelectionStore.Get(mapKey);

        _mapState.Text = !enabled
            ? T(
                "Modo RP desativado.",
                "RP mode disabled.",
                "Modo RP desactivado.",
                "RP-Modus deaktiviert.",
                "Mode RP désactivé.")
            : !mapReady
                ? T(
                    "Aguardando o mapa terminar de carregar no OMSI.",
                    "Waiting for the OMSI map to finish loading.",
                    "Esperando que termine de cargar el mapa de OMSI.",
                    "Warte bis die OMSI-Karte vollständig geladen ist.",
                    "En attente du chargement complet de la carte OMSI.")
                : string.Format(
                    T(
                        "Mapa pronto • {0} personagem(ns) Drivers encontrado(s)",
                        "Map ready • {0} Drivers character(s) found",
                        "Mapa listo • {0} personaje(s) Drivers encontrado(s)",
                        "Karte bereit • {0} Drivers-Charakter(e) gefunden",
                        "Carte prête • {0} personnage(s) Drivers trouvé(s)"),
                    options.Count);

        _selectionState.Text = selected is null
            ? T(
                "Nenhum personagem selecionado.",
                "No character selected.",
                "Ningún personaje seleccionado.",
                "Kein Charakter ausgewählt.",
                "Aucun personnage sélectionné.")
            : string.Format(
                T(
                    "Motorista selecionado: {0}",
                    "Selected driver: {0}",
                    "Conductor seleccionado: {0}",
                    "Ausgewählter Fahrer: {0}",
                    "Conducteur sélectionné : {0}"),
                selected.DisplayName);

        _selectButton.IsEnabled =
            enabled &&
            mapReady &&
            options.Count > 0 &&
            !_controller.IsActive;

        _controlButton.IsEnabled =
            enabled &&
            mapReady &&
            selected is not null &&
            (_controller.IsActive || _controller.IsRuntimeAvailable);
        _controlButton.Content = _controller.IsActive
            ? T(
                "Voltar ao ônibus",
                "Return to bus",
                "Volver al autobús",
                "Zurück zum Bus",
                "Retour au bus")
            : T(
                "Sair do ônibus / controlar personagem",
                "Exit bus / control character",
                "Salir del autobús / controlar personaje",
                "Bus verlassen / Charakter steuern",
                "Quitter le bus / contrôler le personnage");

        _runtimeState.Text = _controller.IsActive
            ? T(
                "PERSONAGEM ATIVO • WASD controla o motorista fora do ônibus.",
                "CHARACTER ACTIVE • WASD controls the driver outside the bus.",
                "PERSONAJE ACTIVO • WASD controla al conductor fuera del autobús.",
                "CHARAKTER AKTIV • WASD steuert den Fahrer außerhalb des Busses.",
                "PERSONNAGE ACTIF • WASD contrôle le conducteur hors du bus.")
            : !_controller.IsRuntimeAvailable && enabled
                ? T(
                    "Aguardando OMSI 2.3.004 + plugin NavBR com suporte Personagem/RP.",
                    "Waiting for OMSI 2.3.004 + NavBR plugin with Character/RP support.",
                    "Esperando OMSI 2.3.004 + plugin NavBR con soporte Personaje/RP.",
                    "Warte auf OMSI 2.3.004 + NavBR-Plugin mit Charakter/RP-Unterstützung.",
                    "En attente d’OMSI 2.3.004 + plugin NavBR avec prise en charge Personnage/RP.")
                : T(
                    "Pronto para teste experimental.",
                    "Ready for experimental testing.",
                    "Listo para prueba experimental.",
                    "Bereit für experimentelle Tests.",
                    "Prêt pour le test expérimental.");

        if (openSelectorWhenReady &&
            enabled &&
            mapReady &&
            options.Count > 0 &&
            selected is null &&
            !_selectorOpen &&
            IsVisible &&
            !string.Equals(_promptedMapKey, mapKey, StringComparison.OrdinalIgnoreCase))
        {
            _promptedMapKey = mapKey;
            OpenSelector();
        }
    }

    private void OpenSelector()
    {
        var mapKey = _mapKeySource();
        if (_selectorOpen ||
            string.IsNullOrWhiteSpace(mapKey) ||
            !_mapReadySource())
        {
            return;
        }

        var options = SafeReadOptions();
        if (options.Count == 0)
        {
            return;
        }

        _selectorOpen = true;
        try
        {
            var current = RoleplayCharacterSelectionStore.Get(mapKey);
            var selector = new RoleplayCharacterSelectorWindow(
                options,
                current?.Id)
            {
                Owner = this
            };

            if (selector.ShowDialog() == true &&
                selector.SelectedCharacter is { } selected)
            {
                RoleplayCharacterSelectionStore.Set(mapKey, selected);
                Refresh(openSelectorWhenReady: false);
            }
        }
        finally
        {
            _selectorOpen = false;
        }
    }

    private IReadOnlyList<RoleplayCharacterOption> SafeReadOptions()
    {
        try
        {
            return _optionsSource()
                .Where(option =>
                    option.DefinitionPointer > 0 &&
                    !string.IsNullOrWhiteSpace(option.Id) &&
                    !string.IsNullOrWhiteSpace(option.DisplayName))
                .ToArray();
        }
        catch
        {
            return Array.Empty<RoleplayCharacterOption>();
        }
    }

    private void Controller_StateChanged(RoleplayCharacterState? state) =>
        Dispatcher.BeginInvoke(() => Refresh(openSelectorWhenReady: false));

    private void Controller_StatusChanged(string status) =>
        Dispatcher.BeginInvoke(() =>
        {
            _runtimeState.Text = TranslateStatus(status);
            Refresh(openSelectorWhenReady: false);
        });

    private void SelectionStore_Changed() =>
        Dispatcher.BeginInvoke(() => Refresh(openSelectorWhenReady: false));

    private static Border Card() => new()
    {
        Padding = new Thickness(18d),
        Background = Brush(9, 20, 29),
        BorderBrush = Brush(31, 47, 57),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(12d),
        Child = new StackPanel()
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = Brush(113, 198, 255),
        FontSize = 10d,
        FontWeight = FontWeights.Bold
    };

    private static TextBlock BodyText(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0d, 8d, 0d, 0d),
        Foreground = Brush(190, 205, 215),
        FontSize = 11d,
        TextWrapping = TextWrapping.Wrap
    };

    private static void StyleButton(Button button, bool primary)
    {
        button.Padding = new Thickness(14d, 8d, 14d, 8d);
        button.MinWidth = 150d;
        button.Background = primary ? Brush(61, 137, 196) : Brush(13, 26, 36);
        button.Foreground = Brushes.White;
        button.BorderBrush = primary ? Brush(113, 198, 255) : Brush(31, 47, 57);
        button.BorderThickness = new Thickness(1d);
        button.FontWeight = FontWeights.SemiBold;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static string TranslateStatus(string status) => status switch
    {
        "roleplay-active" => T(
            "Personagem ativo.",
            "Character active.",
            "Personaje activo.",
            "Charakter aktiv.",
            "Personnage actif."),
        "roleplay-character-required" => T(
            "Selecione o personagem do motorista primeiro.",
            "Select the driver character first.",
            "Selecciona primero el personaje del conductor.",
            "Wähle zuerst den Fahrercharakter.",
            "Sélectionnez d’abord le personnage conducteur."),
        "selected-driver-not-active" => T(
            "O personagem selecionado não é o motorista humano ativo deste ônibus.",
            "The selected character is not this bus's active human driver.",
            "El personaje seleccionado no es el conductor humano activo de este autobús.",
            "Der ausgewählte Charakter ist nicht der aktive menschliche Fahrer dieses Busses.",
            "Le personnage sélectionné n’est pas le conducteur humain actif de ce bus."),
        "roleplay-plugin-unavailable" => T(
            "Plugin Personagem/RP indisponível.",
            "Character/RP plugin unavailable.",
            "Plugin Personaje/RP no disponible.",
            "Charakter/RP-Plugin nicht verfügbar.",
            "Plugin Personnage/RP indisponible."),
        _ => status.Replace('-', ' ')
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
