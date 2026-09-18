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

    internal async Task PublishLocalRoleplayCharacterAsync(
        RoleplayCharacterState state,
        CancellationToken cancellationToken = default)
    {
        SetLocalRoleplayCharacterState(state);
        await _client.PublishRoleplayCharacterAsync(state, cancellationToken);
    }

    internal async Task ReleaseLocalRoleplayCharacterAsync(
        CancellationToken cancellationToken = default)
    {
        SetLocalRoleplayCharacterState(null);
        await _client.ReleaseRoleplayCharacterAsync(cancellationToken);
    }

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

    private void RoleplayActionButton_Click(object sender, RoutedEventArgs e)
    {
        var enabled = _roleplayCharacterCheckBox?.IsChecked == true &&
                      ExperimentalFeatureFlags.RoleplayCharacterEnabled;
        var telemetry = _telemetrySource();
        var map = _activeMapSource();
        var mapKey = telemetry?.MapCompatibilityId ??
                     map?.CompatibilityId ??
                     telemetry?.MapName ??
                     map?.FolderName;
        var mapReady = enabled &&
                       telemetry?.IsInGame == true &&
                       !string.IsNullOrWhiteSpace(mapKey);

        if (!enabled)
        {
            StatusDetailText.Text = RoleplayDisabledText();
            return;
        }

        if (mapReady && RoleplayCharacterSelectionStore.Get(mapKey) is null)
        {
            OpenRoleplayCharacterSelector();
            return;
        }

        RoleplayActionRequested?.Invoke();
    }

    internal void ShowRoleplayTab()
    {
        MultiplayerTabs.SelectedItem = RoleplayTab;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        ShowInTaskbar = true;
        Activate();
        RefreshRoleplayCharacterSelector(openWhenReady: false);
        RefreshRoleplayTechnicalStatus();
    }

    internal void SetRoleplayRuntimeStatus(string status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetRoleplayRuntimeStatus(status));
            return;
        }

        StatusDetailText.Text = RoleplayRuntimeStatusText(status);
        RefreshRoleplayTechnicalStatus();
    }

    private void RefreshRoleplayTechnicalStatus()
    {
        if (!IsInitialized)
        {
            return;
        }

        var bridgeConnected =
            Application.Current is App app &&
            app.PluginBridge.IsConnected;
        var possession =
            Application.Current is App possessionApp &&
            possessionApp.PluginBridge.SupportsCapability(
                NavBR.Shared.PluginBridge.PluginBridgeProtocol.CapabilityCharacterPossession);
        var transform =
            Application.Current is App transformApp &&
            transformApp.PluginBridge.SupportsCapability(
                NavBR.Shared.PluginBridge.PluginBridgeProtocol.CapabilityCharacterTransform);

        RoleplayPluginStatusText.Text = !bridgeConnected
            ? RpT(
                "Plugin NavBR v3 desconectado.",
                "NavBR v3 plugin disconnected.",
                "Plugin NavBR v3 desconectado.",
                "NavBR-v3-Plugin getrennt.",
                "Plugin NavBR v3 déconnecté.")
            : possession && transform
                ? RpT(
                    "Plugin v3 pronto para Personagem/RP.",
                    "Plugin v3 ready for Character/RP.",
                    "Plugin v3 listo para Personaje/RP.",
                    "Plugin v3 bereit für Charakter/RP.",
                    "Plugin v3 prêt pour Personnage/RP.")
                : RpT(
                    "Plugin conectado; aguardando capacidades RP do OMSI.",
                    "Plugin connected; waiting for OMSI RP capabilities.",
                    "Plugin conectado; esperando capacidades RP de OMSI.",
                    "Plugin verbunden; warte auf OMSI-RP-Funktionen.",
                    "Plugin connecté ; attente des capacités RP d’OMSI.");

        RoleplaySyncStatusText.Text = _client.IsConnected
            ? RpT(
                "Multiplayer conectado • estado RP é sincronizado com a sala.",
                "Multiplayer connected • RP state is synchronized with the room.",
                "Multijugador conectado • el estado RP se sincroniza con la sala.",
                "Multiplayer verbunden • RP-Status wird mit dem Raum synchronisiert.",
                "Multijoueur connecté • l’état RP est synchronisé avec le salon.")
            : RpT(
                "Modo local • o mesmo controlador RP funciona sem multiplayer.",
                "Local mode • the same RP controller works without multiplayer.",
                "Modo local • el mismo controlador RP funciona sin multijugador.",
                "Lokaler Modus • derselbe RP-Controller funktioniert ohne Multiplayer.",
                "Mode local • le même contrôleur RP fonctionne sans multijoueur.");
    }

    private static string RoleplayRuntimeStatusText(string status) => status switch
    {
        "roleplay-active" => RpT(
            "Personagem ativo.",
            "Character active.",
            "Personaje activo.",
            "Charakter aktiv.",
            "Personnage actif."),
        "roleplay-character-required" => RpT(
            "Selecione o personagem do motorista primeiro.",
            "Select the driver character first.",
            "Selecciona primero el personaje del conductor.",
            "Wähle zuerst den Fahrercharakter.",
            "Sélectionnez d’abord le personnage conducteur."),
        "selected-driver-not-active" => RpT(
            "O personagem selecionado não corresponde ao motorista humano do ônibus atual.",
            "The selected character does not match the human driver of the current bus.",
            "El personaje seleccionado no corresponde al conductor humano del autobús actual.",
            "Der ausgewählte Charakter entspricht nicht dem menschlichen Fahrer des aktuellen Busses.",
            "Le personnage sélectionné ne correspond pas au conducteur humain du bus actuel."),
        "roleplay-plugin-unavailable" or "character-backend-unavailable" => RpT(
            "Plugin Personagem/RP indisponível. Atualize o plugin NavBR v3 e reinicie o OMSI.",
            "Character/RP plugin unavailable. Update to the NavBR v3 plugin and restart OMSI.",
            "Plugin Personaje/RP no disponible. Actualiza el plugin NavBR v3 y reinicia OMSI.",
            "Charakter/RP-Plugin nicht verfügbar. NavBR-v3-Plugin aktualisieren und OMSI neu starten.",
            "Plugin Personnage/RP indisponible. Mettez à jour le plugin NavBR v3 et redémarrez OMSI."),
        "roleplay-release-failed" => RpT(
            "RP encerrado no NavBR, mas o plugin não confirmou a restauração do motorista.",
            "RP ended in NavBR, but the plugin did not confirm driver restoration.",
            "RP terminó en NavBR, pero el plugin no confirmó la restauración del conductor.",
            "RP in NavBR beendet, aber die Fahrerwiederherstellung wurde nicht bestätigt.",
            "RP terminé dans NavBR, mais la restauration du conducteur n’a pas été confirmée."),
        "roleplay-returned-to-bus" or "roleplay-exit" => RpT(
            "No ônibus.",
            "In bus.",
            "En autobús.",
            "Im Bus.",
            "Dans le bus."),
        _ => status.Replace('-', ' ')
    };

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
        RefreshRoleplayActionState(enabled, mapReady);
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

    private void RefreshRoleplayActionState(bool enabled, bool mapReady)
    {
        var selected = RoleplayCharacterSelectionStore.Get(_roleplayMapKey);
        var active = _localRoleplayCharacter?.IsActive == true;

        if (active)
        {
            RoleplayStateText.Text = string.Format(
                RpT(
                    "A pé • {0}",
                    "On foot • {0}",
                    "A pie • {0}",
                    "Zu Fuß • {0}",
                    "À pied • {0}"),
                _localRoleplayCharacter?.CharacterName ??
                selected?.DisplayName ??
                RpT("personagem", "character", "personaje", "Charakter", "personnage"));
            RoleplayActionButton.Content = RpT(
                "Voltar ao ônibus",
                "Return to bus",
                "Volver al autobús",
                "Zurück zum Bus",
                "Retour au bus");
            RoleplayActionButton.IsEnabled = true;
            return;
        }

        if (!enabled)
        {
            RoleplayStateText.Text = RoleplayDisabledText();
            RoleplayActionButton.Content = RpT(
                "Configurar personagem",
                "Configure character",
                "Configurar personaje",
                "Charakter konfigurieren",
                "Configurer le personnage");
            RoleplayActionButton.IsEnabled = true;
            return;
        }

        if (!mapReady)
        {
            RoleplayStateText.Text = RoleplayWaitingForMapText();
            RoleplayActionButton.Content = RpT(
                "Aguardando mapa",
                "Waiting for map",
                "Esperando mapa",
                "Warte auf Karte",
                "En attente de la carte");
            RoleplayActionButton.IsEnabled = false;
            return;
        }

        if (selected is null)
        {
            RoleplayStateText.Text = RpT(
                "Mapa pronto • selecione o motorista",
                "Map ready • select the driver",
                "Mapa listo • selecciona el conductor",
                "Karte bereit • Fahrer auswählen",
                "Carte prête • sélectionnez le conducteur");
            RoleplayActionButton.Content = RoleplaySelectButtonText();
            RoleplayActionButton.IsEnabled = true;
            return;
        }

        RoleplayStateText.Text = string.Format(
            RpT(
                "No ônibus • {0}",
                "In bus • {0}",
                "En autobús • {0}",
                "Im Bus • {0}",
                "Dans le bus • {0}"),
            selected.DisplayName);
        RoleplayActionButton.Content = RpT(
            "Ativar personagem",
            "Activate character",
            "Activar personaje",
            "Charakter aktivieren",
            "Activer personnage");
        RoleplayActionButton.IsEnabled = true;
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
        RpT(
            "Personagem do motorista / RP (TESTE ALPHA)",
            "Driver character / RP (ALPHA TEST)",
            "Personaje del conductor / RP (PRUEBA ALPHA)",
            "Fahrercharakter / RP (ALPHA-TEST)",
            "Personnage conducteur / RP (TEST ALPHA)");

    private static string RoleplayWarning() =>
        RpT(
            "Após o mapa carregar, lê somente os personagens reais da lista Drivers do OMSI. A escolha identifica o avatar do jogador. O controle a pé permanece experimental e só será habilitado quando o plugin confirmar um motorista humano compatível no ônibus do jogador.",
            "After the map loads, reads only real characters from OMSI's Drivers list. The selection identifies the player's avatar. On-foot control remains experimental and is enabled only when the plugin confirms a compatible human driver in the player's bus.",
            "Tras cargar el mapa, lee solo los personajes reales de la lista Drivers de OMSI. La selección identifica el avatar del jugador. El control a pie sigue siendo experimental.",
            "Nach dem Laden der Karte werden nur echte Charaktere aus OMSIs Drivers-Liste gelesen. Die Auswahl bestimmt den Spieler-Avatar. Die Steuerung zu Fuß bleibt experimentell.",
            "Après le chargement de la carte, seuls les personnages réels de la liste Drivers d’OMSI sont lus. La sélection identifie l’avatar du joueur. Le contrôle à pied reste expérimental.");

    private static string RoleplaySelectButtonText() =>
        RpT("Selecionar personagem", "Select character", "Seleccionar personaje", "Charakter wählen", "Choisir le personnage");

    private static string RoleplayWaitingForMapText() =>
        RpT("Aguardando mapa carregado no OMSI.", "Waiting for an OMSI map.", "Esperando un mapa de OMSI.", "Warte auf eine OMSI-Karte.", "En attente d’une carte OMSI.");

    private static string RoleplayNoDriversText() =>
        RpT("O mapa não expôs personagens Drivers utilizáveis.", "The map exposed no usable Drivers characters.", "El mapa no expuso personajes Drivers utilizables.", "Die Karte stellt keine nutzbaren Drivers-Charaktere bereit.", "La carte n’expose aucun personnage Drivers utilisable.");

    private static string RoleplayDisabledText() =>
        RpT("Modo Personagem/RP desativado.", "Character/RP mode disabled.", "Modo Personaje/RP desactivado.", "Charakter-/RP-Modus deaktiviert.", "Mode Personnage/RP désactivé.");

    private static string RoleplayEnabledText() =>
        RpT("Modo Personagem/RP ativado. O seletor será liberado após o mapa carregar.", "Character/RP mode enabled. The selector unlocks after the map loads.", "Modo Personaje/RP activado. El selector se habilita tras cargar el mapa.", "Charakter-/RP-Modus aktiviert. Die Auswahl wird nach dem Laden der Karte freigeschaltet.", "Mode Personnage/RP activé. Le sélecteur sera disponible après le chargement de la carte.");

    private static string RoleplaySelectPromptText(int count) =>
        string.Format(
            RpT("{0} personagem(ns) real(is) disponível(is).", "{0} real character(s) available.", "{0} personaje(s) real(es) disponible(s).", "{0} echte(r) Charakter(e) verfügbar.", "{0} personnage(s) réel(s) disponible(s)."),
            count);

    private static string RoleplaySelectedText(string name) =>
        string.Format(
            RpT("Selecionado: {0}", "Selected: {0}", "Seleccionado: {0}", "Ausgewählt: {0}", "Sélectionné : {0}"),
            name);

    private static string RoleplaySelectedStatusText(string name) =>
        string.Format(
            RpT("Personagem {0} selecionado para o motorista deste mapa.", "Character {0} selected for this map's driver.", "Personaje {0} seleccionado para el conductor de este mapa.", "Charakter {0} für den Fahrer dieser Karte ausgewählt.", "Personnage {0} sélectionné pour le conducteur de cette carte."),
            name);

    private static string RpT(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
