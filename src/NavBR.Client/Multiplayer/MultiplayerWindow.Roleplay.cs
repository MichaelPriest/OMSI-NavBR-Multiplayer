using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private CheckBox? _roleplayCharacterCheckBox;
    private Button? _roleplayCharacterSelectButton;
    private TextBlock? _roleplayCharacterSelectionText;
    private DispatcherTimer? _roleplayCatalogTimer;
    private RoleplayCharacterOption? _selectedRoleplayCharacter;
    private string? _roleplayMapKey;
    private string? _roleplayPromptedMapKey;
    private bool _roleplayUiInstalled;
    private bool _roleplaySelectorOpen;

    internal RoleplayCharacterOption? SelectedRoleplayCharacter =>
        RoleplayCharacterSelectionStore.Get(_roleplayMapKey);

    internal Task PublishLocalRoleplayCharacterAsync(
        RoleplayCharacterState state,
        CancellationToken cancellationToken = default) =>
        _client.PublishRoleplayCharacterAsync(state, cancellationToken);

    internal Task ReleaseLocalRoleplayCharacterAsync(
        CancellationToken cancellationToken = default) =>
        _client.ReleaseRoleplayCharacterAsync(cancellationToken);

    private void InitializeRoleplayCharacterSelector()
    {
        ExperimentalFeatureFlags.SetRoleplayCharacterEnabled(
            _settings.ExperimentalRoleplayCharacterEnabled);

        _roleplayCatalogTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1d)
        };
        _roleplayCatalogTimer.Tick -= RoleplayCatalogTimer_Tick;
        _roleplayCatalogTimer.Tick += RoleplayCatalogTimer_Tick;
        _roleplayCatalogTimer.Start();

        RoleplayCharacterSelectionStore.Changed += RoleplayCharacterSelectionStore_Changed;
        Closed += (_, _) =>
        {
            _roleplayCatalogTimer?.Stop();
            RoleplayCharacterSelectionStore.Changed -= RoleplayCharacterSelectionStore_Changed;
        };
        RefreshRoleplayCharacterSelector(openWhenReady: true);
    }

    private void InstallRoleplayOptions(StackPanel options)
    {
        if (_roleplayUiInstalled)
        {
            return;
        }

        _roleplayUiInstalled = true;

        _roleplayCharacterCheckBox = new CheckBox
        {
            IsChecked = _settings.ExperimentalRoleplayCharacterEnabled,
            Content = RoleplayLabel(),
            ToolTip = RoleplayWarning(),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 0d, 0d, 6d)
        };
        _roleplayCharacterCheckBox.Click += RoleplayCharacterCheckBox_Click;
        options.Children.Add(_roleplayCharacterCheckBox);

        var selectorRow = new Grid
        {
            Margin = new Thickness(0d, 0d, 0d, 7d)
        };
        selectorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        selectorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        _roleplayCharacterSelectButton = new Button
        {
            Content = RoleplaySelectButtonText(),
            Padding = new Thickness(10d, 5d, 10d, 5d),
            MinWidth = 150d,
            IsEnabled = false,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _roleplayCharacterSelectButton.Click += RoleplayCharacterSelectButton_Click;
        Grid.SetColumn(_roleplayCharacterSelectButton, 0);
        selectorRow.Children.Add(_roleplayCharacterSelectButton);

        _roleplayCharacterSelectionText = new TextBlock
        {
            Text = RoleplayWaitingForMapText(),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(151, 171, 185)),
            FontSize = 10d,
            Margin = new Thickness(10d, 0d, 0d, 0d),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(_roleplayCharacterSelectionText, 1);
        selectorRow.Children.Add(_roleplayCharacterSelectionText);

        options.Children.Add(selectorRow);
    }

    private void RoleplayCatalogTimer_Tick(object? sender, EventArgs e) =>
        RefreshRoleplayCharacterSelector(openWhenReady: true);

    private void RoleplayCharacterCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_roleplayCharacterCheckBox is null)
        {
            return;
        }

        var enabled = _roleplayCharacterCheckBox.IsChecked == true;
        _settings = _settings with { ExperimentalRoleplayCharacterEnabled = enabled };
        MultiplayerSettingsStore.Save(_settings);
        ExperimentalFeatureFlags.SetRoleplayCharacterEnabled(enabled);

        if (!enabled)
        {
            _selectedRoleplayCharacter = null;
            _roleplayPromptedMapKey = null;
            RoleplayCharacterSelectionStore.Clear();
            StatusDetailText.Text = RoleplayDisabledText();
        }
        else
        {
            StatusDetailText.Text = RoleplayEnabledText();
        }

        RefreshRoleplayCharacterSelector(openWhenReady: enabled);
    }

    private void RoleplayCharacterSelectButton_Click(object sender, RoutedEventArgs e) =>
        OpenRoleplayCharacterSelector();

    private void RefreshRoleplayCharacterSelector(bool openWhenReady)
    {
        if (_roleplayCharacterSelectButton is null ||
            _roleplayCharacterSelectionText is null)
        {
            return;
        }

        var enabled = _roleplayCharacterCheckBox?.IsChecked == true &&
                      ExperimentalFeatureFlags.RoleplayCharacterEnabled;
        var telemetry = _telemetrySource();
        var map = _activeMapSource();
        var mapKey = telemetry?.MapCompatibilityId ??
                     map?.CompatibilityId ??
                     telemetry?.MapName ??
                     map?.FolderName;

        if (!string.Equals(_roleplayMapKey, mapKey, StringComparison.OrdinalIgnoreCase))
        {
            _roleplayMapKey = mapKey;
            RoleplayCharacterSelectionStore.ResetForMap(mapKey);
            _selectedRoleplayCharacter = RoleplayCharacterSelectionStore.Get(mapKey);
            _roleplayPromptedMapKey = null;
        }
        else
        {
            _selectedRoleplayCharacter = RoleplayCharacterSelectionStore.Get(mapKey);
        }

        var mapReady = enabled &&
                       telemetry?.IsInGame == true &&
                       !string.IsNullOrWhiteSpace(mapKey);
        if (!mapReady)
        {
            _roleplayCharacterSelectButton.IsEnabled = false;
            _roleplayCharacterSelectionText.Text = enabled
                ? RoleplayWaitingForMapText()
                : RoleplayDisabledText();
            return;
        }

        var options = SafeReadRoleplayOptions();
        _roleplayCharacterSelectButton.IsEnabled = options.Count > 0;

        if (options.Count == 0)
        {
            _roleplayCharacterSelectionText.Text = RoleplayNoDriversText();
            return;
        }

        if (_selectedRoleplayCharacter is not null)
        {
            var current = options.FirstOrDefault(option =>
                string.Equals(
                    option.Id,
                    _selectedRoleplayCharacter.Id,
                    StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                _selectedRoleplayCharacter = null;
            }
            else
            {
                _selectedRoleplayCharacter = current;
            }
        }

        _roleplayCharacterSelectionText.Text = _selectedRoleplayCharacter is null
            ? RoleplaySelectPromptText(options.Count)
            : RoleplaySelectedText(_selectedRoleplayCharacter.DisplayName);

        if (openWhenReady &&
            _selectedRoleplayCharacter is null &&
            !_roleplaySelectorOpen &&
            IsVisible &&
            WindowState != WindowState.Minimized &&
            !string.Equals(_roleplayPromptedMapKey, mapKey, StringComparison.OrdinalIgnoreCase))
        {
            _roleplayPromptedMapKey = mapKey;
            OpenRoleplayCharacterSelector();
        }
    }

    private IReadOnlyList<RoleplayCharacterOption> SafeReadRoleplayOptions()
    {
        try
        {
            return _roleplayCharacterOptionsSource()
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

    private void OpenRoleplayCharacterSelector()
    {
        if (_roleplaySelectorOpen ||
            _roleplayCharacterSelectButton?.IsEnabled != true)
        {
            return;
        }

        var options = SafeReadRoleplayOptions();
        if (options.Count == 0)
        {
            if (_roleplayCharacterSelectionText is not null)
            {
                _roleplayCharacterSelectionText.Text = RoleplayNoDriversText();
            }
            return;
        }

        _roleplaySelectorOpen = true;
        try
        {
            var selector = new RoleplayCharacterSelectorWindow(
                options,
                _selectedRoleplayCharacter?.Id)
            {
                Owner = this
            };

            if (selector.ShowDialog() == true &&
                selector.SelectedCharacter is { } selected)
            {
                _selectedRoleplayCharacter = selected;
                if (!string.IsNullOrWhiteSpace(_roleplayMapKey))
                {
                    RoleplayCharacterSelectionStore.Set(_roleplayMapKey, selected);
                }
                if (_roleplayCharacterSelectionText is not null)
                {
                    _roleplayCharacterSelectionText.Text =
                        RoleplaySelectedText(selected.DisplayName);
                }

                StatusDetailText.Text = RoleplaySelectedStatusText(selected.DisplayName);
            }
        }
        finally
        {
            _roleplaySelectorOpen = false;
        }
    }

    private void RoleplayCharacterSelectionStore_Changed()
    {
        Dispatcher.BeginInvoke(() =>
            RefreshRoleplayCharacterSelector(openWhenReady: false));
    }

    private static string RoleplayLabel() =>
        T(
            "Personagem do motorista / RP (TESTE ALPHA)",
            "Driver character / RP (ALPHA TEST)",
            "Personaje del conductor / RP (PRUEBA ALPHA)",
            "Fahrercharakter / RP (ALPHA-TEST)",
            "Personnage conducteur / RP (TEST ALPHA)");

    private static string RoleplayWarning() =>
        T(
            "Após o mapa carregar, lê somente os personagens reais da lista Drivers do OMSI. A escolha identifica o avatar do jogador. O controle a pé permanece experimental e só será habilitado quando o plugin confirmar um motorista humano compatível no ônibus do jogador.",
            "After the map loads, reads only real characters from OMSI's Drivers list. The selection identifies the player's avatar. On-foot control remains experimental and is enabled only when the plugin confirms a compatible human driver in the player's bus.",
            "Tras cargar el mapa, lee solo los personajes reales de la lista Drivers de OMSI. La selección identifica el avatar del jugador. El control a pie sigue siendo experimental.",
            "Nach dem Laden der Karte werden nur echte Charaktere aus OMSIs Drivers-Liste gelesen. Die Auswahl bestimmt den Spieler-Avatar. Die Steuerung zu Fuß bleibt experimentell.",
            "Après le chargement de la carte, seuls les personnages réels de la liste Drivers d’OMSI sont lus. La sélection identifie l’avatar du joueur. Le contrôle à pied reste expérimental.");

    private static string RoleplaySelectButtonText() =>
        T("Selecionar personagem", "Select character", "Seleccionar personaje", "Charakter wählen", "Choisir le personnage");

    private static string RoleplayWaitingForMapText() =>
        T("Aguardando mapa carregado no OMSI.", "Waiting for an OMSI map.", "Esperando un mapa de OMSI.", "Warte auf eine OMSI-Karte.", "En attente d’une carte OMSI.");

    private static string RoleplayNoDriversText() =>
        T("O mapa não expôs personagens Drivers utilizáveis.", "The map exposed no usable Drivers characters.", "El mapa no expuso personajes Drivers utilizables.", "Die Karte stellt keine nutzbaren Drivers-Charaktere bereit.", "La carte n’expose aucun personnage Drivers utilisable.");

    private static string RoleplayDisabledText() =>
        T("Modo Personagem/RP desativado.", "Character/RP mode disabled.", "Modo Personaje/RP desactivado.", "Charakter-/RP-Modus deaktiviert.", "Mode Personnage/RP désactivé.");

    private static string RoleplayEnabledText() =>
        T("Modo Personagem/RP ativado. O seletor será liberado após o mapa carregar.", "Character/RP mode enabled. The selector unlocks after the map loads.", "Modo Personaje/RP activado. El selector se habilita tras cargar el mapa.", "Charakter-/RP-Modus aktiviert. Die Auswahl wird nach dem Laden der Karte freigeschaltet.", "Mode Personnage/RP activé. Le sélecteur sera disponible après le chargement de la carte.");

    private static string RoleplaySelectPromptText(int count) =>
        string.Format(
            T("{0} personagem(ns) real(is) disponível(is).", "{0} real character(s) available.", "{0} personaje(s) real(es) disponible(s).", "{0} echte(r) Charakter(e) verfügbar.", "{0} personnage(s) réel(s) disponible(s)."),
            count);

    private static string RoleplaySelectedText(string name) =>
        string.Format(
            T("Selecionado: {0}", "Selected: {0}", "Seleccionado: {0}", "Ausgewählt: {0}", "Sélectionné : {0}"),
            name);

    private static string RoleplaySelectedStatusText(string name) =>
        string.Format(
            T("Personagem {0} selecionado para o motorista deste mapa.", "Character {0} selected for this map's driver.", "Personaje {0} seleccionado para el conductor de este mapa.", "Charakter {0} für den Fahrer dieser Karte ausgewählt.", "Personnage {0} sélectionné pour le conducteur de cette carte."),
            name);

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
