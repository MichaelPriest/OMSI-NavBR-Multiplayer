using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class PublicRoomBrowserWindow : Window
{
    private const string AllFilterKey = "__all__";

    private readonly string _serverUrl;
    private readonly OmsiCompatibilityManifest? _localManifest;
    private readonly PublicRoomDirectoryClient _directory = new();
    private readonly ListBox _rooms = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _requirementsText = new();
    private readonly TextBlock _compatibilityText = new();
    private readonly Border _requirementsCard = new();
    private readonly Button _refreshButton = new();
    private readonly Button _favoriteButton = new();
    private readonly Button _selectButton = new();
    private readonly TextBox _filterBox = new();
    private readonly CheckBox _favoritesOnly = new();
    private readonly ComboBox _mapFilter = new();
    private readonly ComboBox _navbrVersionFilter = new();
    private readonly ComboBox _compatibilityFilter = new();
    private readonly ComboBox _minimumPlayersFilter = new();
    private IReadOnlyList<PublicRoomSummary> _currentRooms = Array.Empty<PublicRoomSummary>();
    private bool _updatingDynamicFilters;

    public string? SelectedRoomId { get; private set; }
    public PublicRoomSummary? SelectedRoom { get; private set; }

    public PublicRoomBrowserWindow(string serverUrl, OmsiCompatibilityManifest? localManifest = null)
    {
        _serverUrl = serverUrl;
        _localManifest = localManifest;
        Title = Pick("Salas públicas", "Public rooms", "Salas públicas", "Öffentliche Räume", "Salons publics");
        Width = 980;
        Height = 720;
        MinWidth = 760;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(6, 10, 14);
        Foreground = Brushes.White;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headingStack = new StackPanel();
        headingStack.Children.Add(new TextBlock
        {
            Text = Pick(
                "Salas públicas disponíveis",
                "Available public rooms",
                "Salas públicas disponibles",
                "Verfügbare öffentliche Räume",
                "Salons publics disponibles"),
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        headingStack.Children.Add(new TextBlock
        {
            Text = Pick(
                "Confira o mapa obrigatório e os requisitos antes de selecionar uma sala. Favoritos ficam no topo da lista.",
                "Check the required map and requirements before selecting a room. Favorites stay at the top of the list.",
                "Comprueba el mapa obligatorio y los requisitos antes de seleccionar una sala. Los favoritos aparecen primero.",
                "Prüfe die erforderliche Karte und die Anforderungen, bevor du einen Raum auswählst. Favoriten stehen oben.",
                "Vérifiez la carte requise et les prérequis avant de sélectionner un salon. Les favoris restent en haut."),
            Foreground = Brush(143, 163, 177),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var filters = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        _filterBox.Width = 220;
        _filterBox.Height = 34;
        _filterBox.Padding = new Thickness(9, 5, 9, 5);
        _filterBox.Background = Brush(13, 26, 36);
        _filterBox.Foreground = Brushes.White;
        _filterBox.BorderBrush = Brush(35, 53, 65);
        _filterBox.BorderThickness = new Thickness(1);
        _filterBox.ToolTip = Pick(
            "Buscar por sala, mapa, versão, ônibus ou HOF",
            "Search by room, map, version, bus or HOF",
            "Buscar por sala, mapa, versión, autobús o HOF",
            "Nach Raum, Karte, Version, Bus oder HOF suchen",
            "Rechercher par salon, carte, version, bus ou HOF");
        _filterBox.TextChanged += (_, _) => RenderRooms();
        filters.Children.Add(_filterBox);

        ConfigureDynamicFilter(
            _mapFilter,
            170,
            Pick("Filtrar pelo mapa anunciado pela sala.", "Filter by the room's advertised map.", "Filtrar por el mapa anunciado por la sala.", "Nach der vom Raum gemeldeten Karte filtern.", "Filtrer par la carte annoncée par le salon."));
        _mapFilter.SelectionChanged += (_, _) =>
        {
            if (!_updatingDynamicFilters)
            {
                RenderRooms();
            }
        };
        filters.Children.Add(_mapFilter);

        ConfigureDynamicFilter(
            _navbrVersionFilter,
            150,
            Pick("Filtrar pela versão NavBR anunciada pela sala.", "Filter by the room's advertised NavBR version.", "Filtrar por la versión NavBR anunciada por la sala.", "Nach der vom Raum gemeldeten NavBR-Version filtern.", "Filtrer par la version NavBR annoncée par le salon."));
        _navbrVersionFilter.SelectionChanged += (_, _) =>
        {
            if (!_updatingDynamicFilters)
            {
                RenderRooms();
            }
        };
        filters.Children.Add(_navbrVersionFilter);

        _compatibilityFilter.Width = 180;
        _compatibilityFilter.Height = 34;
        _compatibilityFilter.Margin = new Thickness(10, 0, 0, 0);
        _compatibilityFilter.ItemsSource = new[]
        {
            new CompatibilityFilterChoice("all", Pick("Toda compatibilidade", "Any compatibility", "Toda compatibilidad", "Alle Kompatibilität", "Toute compatibilité")),
            new CompatibilityFilterChoice("compatible", Pick("Somente compatíveis", "Compatible only", "Solo compatibles", "Nur kompatibel", "Compatibles uniquement")),
            new CompatibilityFilterChoice("joinable", Pick("Compatíveis / parciais", "Compatible / partial", "Compatibles / parciales", "Kompatibel / teilweise", "Compatibles / partiels")),
            new CompatibilityFilterChoice("blocked", Pick("Somente bloqueadas", "Blocked only", "Solo bloqueadas", "Nur gesperrt", "Bloqués uniquement"))
        };
        _compatibilityFilter.DisplayMemberPath = nameof(CompatibilityFilterChoice.Label);
        _compatibilityFilter.SelectedValuePath = nameof(CompatibilityFilterChoice.Key);
        _compatibilityFilter.SelectedValue = "all";
        _compatibilityFilter.IsEnabled = _localManifest is not null;
        _compatibilityFilter.ToolTip = _localManifest is null
            ? Pick(
                "Carregue uma configuração local para filtrar por compatibilidade.",
                "Load a local setup to filter by compatibility.",
                "Carga una configuración local para filtrar por compatibilidad.",
                "Lade eine lokale Konfiguration, um nach Kompatibilität zu filtern.",
                "Chargez une configuration locale pour filtrer par compatibilité.")
            : null;
        _compatibilityFilter.SelectionChanged += (_, _) => RenderRooms();
        filters.Children.Add(_compatibilityFilter);

        _minimumPlayersFilter.Width = 140;
        _minimumPlayersFilter.Height = 34;
        _minimumPlayersFilter.Margin = new Thickness(10, 0, 0, 0);
        _minimumPlayersFilter.ItemsSource = new[]
        {
            new PlayerFilterChoice(0, Pick("Qualquer lotação", "Any players", "Cualquier cantidad", "Beliebige Spielerzahl", "Tout nombre")),
            new PlayerFilterChoice(1, Pick("1+ jogador", "1+ player", "1+ jugador", "1+ Spieler", "1+ joueur")),
            new PlayerFilterChoice(2, Pick("2+ jogadores", "2+ players", "2+ jugadores", "2+ Spieler", "2+ joueurs")),
            new PlayerFilterChoice(4, Pick("4+ jogadores", "4+ players", "4+ jugadores", "4+ Spieler", "4+ joueurs")),
            new PlayerFilterChoice(8, Pick("8+ jogadores", "8+ players", "8+ jugadores", "8+ Spieler", "8+ joueurs")),
            new PlayerFilterChoice(16, Pick("16+ jogadores", "16+ players", "16+ jugadores", "16+ Spieler", "16+ joueurs"))
        };
        _minimumPlayersFilter.DisplayMemberPath = nameof(PlayerFilterChoice.Label);
        _minimumPlayersFilter.SelectedValuePath = nameof(PlayerFilterChoice.MinimumPlayers);
        _minimumPlayersFilter.SelectedValue = 0;
        _minimumPlayersFilter.SelectionChanged += (_, _) => RenderRooms();
        filters.Children.Add(_minimumPlayersFilter);

        _favoritesOnly.Content = Pick(
            "Somente favoritos",
            "Favorites only",
            "Solo favoritos",
            "Nur Favoriten",
            "Favoris uniquement");
        _favoritesOnly.Foreground = Brush(205, 219, 228);
        _favoritesOnly.VerticalAlignment = VerticalAlignment.Center;
        _favoritesOnly.Margin = new Thickness(14, 0, 0, 0);
        _favoritesOnly.Checked += (_, _) => RenderRooms();
        _favoritesOnly.Unchecked += (_, _) => RenderRooms();
        filters.Children.Add(_favoritesOnly);
        headingStack.Children.Add(filters);
        root.Children.Add(headingStack);

        _status.Margin = new Thickness(0, 10, 0, 12);
        _status.Foreground = Brush(161, 180, 193);
        _status.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_status, 1);
        root.Children.Add(_status);

        _rooms.DisplayMemberPath = nameof(PublicRoomListItem.DisplayText);
        _rooms.Background = Brush(9, 16, 22);
        _rooms.Foreground = Brushes.White;
        _rooms.BorderBrush = Brush(35, 53, 65);
        _rooms.BorderThickness = new Thickness(1);
        _rooms.SelectionChanged += (_, _) => RenderSelection();
        _rooms.MouseDoubleClick += (_, _) => AcceptSelection();
        Grid.SetRow(_rooms, 2);
        root.Children.Add(_rooms);

        var requirementStack = new StackPanel();
        requirementStack.Children.Add(new TextBlock
        {
            Text = Pick("REQUISITOS DA SALA", "ROOM REQUIREMENTS", "REQUISITOS DE LA SALA", "RAUMANFORDERUNGEN", "PRÉREQUIS DU SALON"),
            Foreground = Brush(91, 181, 238),
            FontSize = 9.5,
            FontWeight = FontWeights.Bold
        });
        _requirementsText.Foreground = Brush(222, 234, 241);
        _requirementsText.FontSize = 11.5;
        _requirementsText.TextWrapping = TextWrapping.Wrap;
        _requirementsText.Margin = new Thickness(0, 8, 0, 8);
        requirementStack.Children.Add(_requirementsText);
        _compatibilityText.FontSize = 10.5;
        _compatibilityText.FontWeight = FontWeights.SemiBold;
        _compatibilityText.TextWrapping = TextWrapping.Wrap;
        requirementStack.Children.Add(_compatibilityText);

        _requirementsCard.Margin = new Thickness(0, 14, 0, 0);
        _requirementsCard.Padding = new Thickness(15);
        _requirementsCard.Background = Brush(10, 19, 26);
        _requirementsCard.BorderBrush = Brush(31, 55, 70);
        _requirementsCard.BorderThickness = new Thickness(1);
        _requirementsCard.CornerRadius = new CornerRadius(12);
        _requirementsCard.Child = requirementStack;
        _requirementsCard.Visibility = Visibility.Collapsed;
        Grid.SetRow(_requirementsCard, 3);
        root.Children.Add(_requirementsCard);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        _refreshButton.Content = Pick("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser");
        _refreshButton.MinWidth = 105;
        _refreshButton.Height = 36;
        _refreshButton.Margin = new Thickness(0, 0, 8, 0);
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        actions.Children.Add(_refreshButton);

        _favoriteButton.Content = FavoriteButtonText(false);
        _favoriteButton.MinWidth = 150;
        _favoriteButton.Height = 36;
        _favoriteButton.Margin = new Thickness(0, 0, 8, 0);
        _favoriteButton.IsEnabled = false;
        _favoriteButton.Click += Favorite_Click;
        actions.Children.Add(_favoriteButton);

        _selectButton.Content = Pick("Selecionar sala", "Select room", "Seleccionar sala", "Raum auswählen", "Sélectionner le salon");
        _selectButton.MinWidth = 140;
        _selectButton.Height = 36;
        _selectButton.IsEnabled = false;
        _selectButton.Click += (_, _) => AcceptSelection();
        actions.Children.Add(_selectButton);

        Grid.SetRow(actions, 4);
        root.Children.Add(actions);
        Content = root;

        RefreshDynamicFilterChoices();
        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => _directory.Dispose();
    }

    private async Task RefreshAsync()
    {
        _refreshButton.IsEnabled = false;
        _selectButton.IsEnabled = false;
        _favoriteButton.IsEnabled = false;
        _requirementsCard.Visibility = Visibility.Collapsed;
        _status.Text = Pick(
            "Consultando o servidor…",
            "Checking the server…",
            "Consultando el servidor…",
            "Server wird abgefragt…",
            "Interrogation du serveur…");

        try
        {
            _currentRooms = await _directory.GetRoomsAsync(_serverUrl);
            RefreshDynamicFilterChoices();
            RenderRooms();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _currentRooms = Array.Empty<PublicRoomSummary>();
            RefreshDynamicFilterChoices();
            _rooms.ItemsSource = null;
            _status.Text = Pick(
                $"Não foi possível carregar as salas públicas: {ex.Message}",
                $"Could not load public rooms: {ex.Message}",
                $"No se pudieron cargar las salas públicas: {ex.Message}",
                $"Öffentliche Räume konnten nicht geladen werden: {ex.Message}",
                $"Impossible de charger les salons publics : {ex.Message}");
        }
        finally
        {
            _refreshButton.IsEnabled = true;
            RenderSelection();
        }
    }

    private void RefreshDynamicFilterChoices()
    {
        var previousMap = _mapFilter.SelectedValue as string ?? AllFilterKey;
        var previousVersion = _navbrVersionFilter.SelectedValue as string ?? AllFilterKey;

        _updatingDynamicFilters = true;
        try
        {
            var mapChoices = BuildFilterChoices(
                _currentRooms.Select(room => room.MapName),
                Pick("Todos os mapas", "All maps", "Todos los mapas", "Alle Karten", "Toutes les cartes"));
            var versionChoices = BuildFilterChoices(
                _currentRooms.Select(room => room.NavBRVersion),
                Pick("Todas as versões", "All versions", "Todas las versiones", "Alle Versionen", "Toutes les versions"));

            _mapFilter.ItemsSource = mapChoices;
            _navbrVersionFilter.ItemsSource = versionChoices;
            _mapFilter.SelectedValue = mapChoices.Any(choice => string.Equals(choice.Key, previousMap, StringComparison.OrdinalIgnoreCase))
                ? previousMap
                : AllFilterKey;
            _navbrVersionFilter.SelectedValue = versionChoices.Any(choice => string.Equals(choice.Key, previousVersion, StringComparison.OrdinalIgnoreCase))
                ? previousVersion
                : AllFilterKey;
        }
        finally
        {
            _updatingDynamicFilters = false;
        }
    }

    private static IReadOnlyList<DynamicFilterChoice> BuildFilterChoices(IEnumerable<string?> values, string allLabel)
    {
        var choices = new List<DynamicFilterChoice> { new(AllFilterKey, allLabel) };
        choices.AddRange(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .Select(value => new DynamicFilterChoice(value, value)));
        return choices;
    }

    private static void ConfigureDynamicFilter(ComboBox filter, double width, string toolTip)
    {
        filter.Width = width;
        filter.Height = 34;
        filter.Margin = new Thickness(10, 0, 0, 0);
        filter.DisplayMemberPath = nameof(DynamicFilterChoice.Label);
        filter.SelectedValuePath = nameof(DynamicFilterChoice.Key);
        filter.ToolTip = toolTip;
    }

    private void RenderRooms(string? keepSelectedRoomId = null)
    {
        keepSelectedRoomId ??= (_rooms.SelectedItem as PublicRoomListItem)?.Room.RoomId;
        var query = _filterBox.Text?.Trim();
        var favoritesOnly = _favoritesOnly.IsChecked == true;
        var mapFilter = _mapFilter.SelectedValue as string ?? AllFilterKey;
        var navbrVersionFilter = _navbrVersionFilter.SelectedValue as string ?? AllFilterKey;
        var compatibilityFilter = _compatibilityFilter.SelectedValue as string ?? "all";
        var minimumPlayers = _minimumPlayersFilter.SelectedValue is int value ? value : 0;

        var items = _currentRooms
            .Where(room => !favoritesOnly || PublicRoomFavoritesStore.IsFavorite(room.RoomId))
            .Where(room => room.PlayerCount >= minimumPlayers)
            .Where(room => MatchesExactFilter(room.MapName, mapFilter))
            .Where(room => MatchesExactFilter(room.NavBRVersion, navbrVersionFilter))
            .Where(room => MatchesFilter(room, query))
            .Where(room => MatchesCompatibilityFilter(room, compatibilityFilter))
            .OrderByDescending(room => PublicRoomFavoritesStore.IsFavorite(room.RoomId))
            .ThenByDescending(room => room.PlayerCount)
            .ThenBy(room => room.RoomId, StringComparer.CurrentCultureIgnoreCase)
            .Select(room => new PublicRoomListItem(
                room,
                FormatRoom(room, PublicRoomFavoritesStore.IsFavorite(room.RoomId))))
            .ToArray();
        _rooms.ItemsSource = items;

        if (!string.IsNullOrWhiteSpace(keepSelectedRoomId))
        {
            _rooms.SelectedItem = items.FirstOrDefault(item =>
                string.Equals(item.Room.RoomId, keepSelectedRoomId, StringComparison.OrdinalIgnoreCase));
        }

        if (_currentRooms.Count > 0)
        {
            UpdateStatus(items.Length);
        }
    }

    private void UpdateStatus(int? visibleCount = null)
    {
        if (_currentRooms.Count == 0)
        {
            _status.Text = Pick(
                "Nenhuma sala pública ativa neste servidor. Salas privadas não aparecem nesta lista.",
                "No public rooms are active on this server. Private rooms are not shown here.",
                "No hay salas públicas activas en este servidor. Las salas privadas no aparecen aquí.",
                "Auf diesem Server sind keine öffentlichen Räume aktiv. Private Räume werden hier nicht angezeigt.",
                "Aucun salon public n’est actif sur ce serveur. Les salons privés ne sont pas affichés ici.");
            return;
        }

        var shown = visibleCount ?? _rooms.Items.Count;
        _status.Text = Pick(
            $"{shown} de {_currentRooms.Count} sala(s) exibida(s) • favoritos locais: {PublicRoomFavoritesStore.Count}.",
            $"{shown} of {_currentRooms.Count} public room(s) shown • local favorites: {PublicRoomFavoritesStore.Count}.",
            $"{shown} de {_currentRooms.Count} sala(s) mostrada(s) • favoritos locales: {PublicRoomFavoritesStore.Count}.",
            $"{shown} von {_currentRooms.Count} Raum/Räumen angezeigt • lokale Favoriten: {PublicRoomFavoritesStore.Count}.",
            $"{shown} sur {_currentRooms.Count} salon(s) affiché(s) • favoris locaux : {PublicRoomFavoritesStore.Count}." );
    }

    private static bool MatchesFilter(PublicRoomSummary room, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(room.RoomId, query) ||
               Contains(room.MapName, query) ||
               Contains(room.OmsiVersion, query) ||
               Contains(room.NavBRVersion, query) ||
               Contains(room.VehiclePath, query) ||
               Contains(room.HofName, query);
    }

    private static bool MatchesExactFilter(string? value, string key) =>
        string.Equals(key, AllFilterKey, StringComparison.Ordinal) ||
        (!string.IsNullOrWhiteSpace(value) && string.Equals(value.Trim(), key, StringComparison.CurrentCultureIgnoreCase));

    private bool MatchesCompatibilityFilter(PublicRoomSummary room, string key)
    {
        if (string.Equals(key, "all", StringComparison.OrdinalIgnoreCase) || _localManifest is null)
        {
            return true;
        }

        var level = EvaluateCompatibility(room);
        return key switch
        {
            "compatible" => level == RoomCompatibilityLevel.Compatible,
            "joinable" => level is RoomCompatibilityLevel.Compatible or RoomCompatibilityLevel.Warning,
            "blocked" => level == RoomCompatibilityLevel.Blocked,
            _ => true
        };
    }

    private RoomCompatibilityLevel EvaluateCompatibility(PublicRoomSummary room)
    {
        if (!HasRequiredMap(room))
        {
            return RoomCompatibilityLevel.Blocked;
        }
        if (_localManifest is null)
        {
            return RoomCompatibilityLevel.Unknown;
        }

        var report = OmsiCompatibilityEvaluator.Compare(_localManifest, ToManifest(room));
        if (report.HasBlockingIssues)
        {
            return RoomCompatibilityLevel.Blocked;
        }
        if (report.Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Warning))
        {
            return RoomCompatibilityLevel.Warning;
        }
        return RoomCompatibilityLevel.Compatible;
    }

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_rooms.SelectedItem is not PublicRoomListItem item || string.IsNullOrWhiteSpace(item.Room.RoomId))
        {
            return;
        }

        var roomId = item.Room.RoomId;
        PublicRoomFavoritesStore.Toggle(roomId);
        RenderRooms(roomId);
        RenderSelection();
    }

    private void RenderSelection()
    {
        if (_rooms.SelectedItem is not PublicRoomListItem item)
        {
            _requirementsCard.Visibility = Visibility.Collapsed;
            _selectButton.IsEnabled = false;
            _favoriteButton.IsEnabled = false;
            _favoriteButton.Content = FavoriteButtonText(false);
            return;
        }

        var room = item.Room;
        _requirementsCard.Visibility = Visibility.Visible;
        _requirementsText.Text = BuildRequirementText(room);

        var compatibility = GetCompatibilityStatus(room);
        _compatibilityText.Text = compatibility.Text;
        _compatibilityText.Foreground = compatibility.Brush;

        var favorite = PublicRoomFavoritesStore.IsFavorite(room.RoomId);
        _favoriteButton.IsEnabled = !string.IsNullOrWhiteSpace(room.RoomId);
        _favoriteButton.Content = FavoriteButtonText(favorite);

        _selectButton.IsEnabled = HasRequiredMap(room);
        _selectButton.ToolTip = HasRequiredMap(room)
            ? null
            : Pick(
                "O host ainda não informou o mapa obrigatório. Esta sala não pode ser selecionada ainda.",
                "The host has not reported the required map yet. This room cannot be selected yet.",
                "El host aún no informó el mapa obligatorio. Esta sala todavía no se puede seleccionar.",
                "Der Host hat die erforderliche Karte noch nicht gemeldet. Der Raum kann noch nicht ausgewählt werden.",
                "L’hôte n’a pas encore indiqué la carte requise. Ce salon ne peut pas encore être sélectionné.");
    }

    private void AcceptSelection()
    {
        if (_rooms.SelectedItem is not PublicRoomListItem item || !HasRequiredMap(item.Room))
        {
            return;
        }

        SelectedRoom = item.Room;
        SelectedRoomId = item.Room.RoomId;
        DialogResult = true;
        Close();
    }

    private static bool HasRequiredMap(PublicRoomSummary room) => !string.IsNullOrWhiteSpace(room.MapName);

    private static string FormatRoom(PublicRoomSummary room, bool favorite)
    {
        var map = string.IsNullOrWhiteSpace(room.MapName)
            ? Pick("MAPA PENDENTE", "MAP PENDING", "MAPA PENDIENTE", "KARTE AUSSTEHEND", "CARTE EN ATTENTE")
            : room.MapName;
        var players = Pick("jogador(es)", "player(s)", "jugador(es)", "Spieler", "joueur(s)");
        var prefix = favorite
            ? Pick("FAVORITO • ", "FAVORITE • ", "FAVORITO • ", "FAVORIT • ", "FAVORI • ")
            : string.Empty;
        return $"{prefix}{room.RoomId}   •   {room.PlayerCount} {players}   •   {Pick("MAPA", "MAP", "MAPA", "KARTE", "CARTE")}: {map}";
    }

    private static string FavoriteButtonText(bool favorite) => favorite
        ? Pick("Remover favorito", "Remove favorite", "Quitar favorito", "Favorit entfernen", "Retirer des favoris")
        : Pick("Adicionar aos favoritos", "Add to favorites", "Añadir a favoritos", "Zu Favoriten", "Ajouter aux favoris");

    private static string BuildRequirementText(PublicRoomSummary room)
    {
        var map = ValueOrNotReported(room.MapName);
        var mapBuild = ShortFingerprint(room.MapCompatibilityId);
        var bus = ValueOrNotReported(room.VehiclePath);
        var hof = ValueOrNotReported(room.HofName);
        var omsi = ValueOrNotReported(room.OmsiVersion);
        var navbr = ValueOrNotReported(room.NavBRVersion);
        var protocol = room.PluginProtocolVersion > 0
            ? room.PluginProtocolVersion.ToString()
            : Pick("não informado", "not reported", "no informado", "nicht gemeldet", "non indiqué");

        return string.Join(Environment.NewLine,
            $"{Pick("MAPA OBRIGATÓRIO", "REQUIRED MAP", "MAPA OBLIGATORIO", "ERFORDERLICHE KARTE", "CARTE REQUISE")}: {map}",
            $"{Pick("Build do mapa", "Map build", "Build del mapa", "Karten-Build", "Build de la carte")}: {mapBuild}",
            $"{Pick("Ônibus do host", "Host bus", "Autobús del host", "Host-Bus", "Bus de l’hôte")}: {bus}",
            $"HOF: {hof}",
            $"OMSI: {omsi}   •   NavBR: {navbr}   •   {Pick("Protocolo", "Protocol", "Protocolo", "Protokoll", "Protocole")}: {protocol}");
    }

    private (string Text, Brush Brush) GetCompatibilityStatus(PublicRoomSummary room)
    {
        if (!HasRequiredMap(room))
        {
            return (
                Pick(
                    "BLOQUEADO • aguardando o host informar o mapa obrigatório.",
                    "BLOCKED • waiting for the host to report the required map.",
                    "BLOQUEADO • esperando que el host informe el mapa obligatorio.",
                    "GESPERRT • der Host muss die erforderliche Karte melden.",
                    "BLOQUÉ • en attente de la carte requise indiquée par l’hôte."),
                Brush(244, 116, 116));
        }

        if (_localManifest is null)
        {
            return (
                Pick(
                    "MAPA INFORMADO • carregue esse mapa no OMSI antes de entrar.",
                    "MAP REPORTED • load this map in OMSI before joining.",
                    "MAPA INFORMADO • carga este mapa en OMSI antes de entrar.",
                    "KARTE GEMELDET • lade diese Karte in OMSI, bevor du beitrittst.",
                    "CARTE INDIQUÉE • chargez cette carte dans OMSI avant de rejoindre."),
                Brush(240, 194, 105));
        }

        var report = OmsiCompatibilityEvaluator.Compare(_localManifest, ToManifest(room));

        if (report.HasBlockingIssues)
        {
            return (
                Pick(
                    "ATENÇÃO • sua configuração atual não corresponde aos requisitos da sala. Você ainda pode selecionar a sala e carregar o mapa correto antes de conectar.",
                    "ATTENTION • your current setup does not match the room requirements. You may still select the room and load the correct map before connecting.",
                    "ATENCIÓN • tu configuración actual no coincide con los requisitos. Puedes seleccionar la sala y cargar el mapa correcto antes de conectar.",
                    "ACHTUNG • deine aktuelle Konfiguration entspricht nicht den Anforderungen. Du kannst den Raum auswählen und vor dem Verbinden die richtige Karte laden.",
                    "ATTENTION • votre configuration actuelle ne correspond pas aux prérequis. Vous pouvez sélectionner le salon puis charger la bonne carte avant la connexion."),
                Brush(244, 116, 116));
        }

        if (report.Issues.Any(issue => issue.Severity == CompatibilityIssueSeverity.Warning))
        {
            return (
                Pick(
                    "PARCIAL • mapa compatível, mas há diferenças não bloqueantes de versão/HOF.",
                    "PARTIAL • map compatible, but there are non-blocking version/HOF differences.",
                    "PARCIAL • mapa compatible, pero hay diferencias no bloqueantes de versión/HOF.",
                    "TEILWEISE • Karte kompatibel, aber es gibt nicht blockierende Versions-/HOF-Unterschiede.",
                    "PARTIEL • carte compatible, avec des différences non bloquantes de version/HOF."),
                Brush(240, 194, 105));
        }

        return (
            Pick(
                "COMPATÍVEL • os requisitos anunciados correspondem à configuração local.",
                "COMPATIBLE • advertised requirements match the local setup.",
                "COMPATIBLE • los requisitos anunciados coinciden con la configuración local.",
                "KOMPATIBEL • die gemeldeten Anforderungen entsprechen der lokalen Konfiguration.",
                "COMPATIBLE • les prérequis annoncés correspondent à la configuration locale."),
            Brush(110, 216, 153));
    }

    private static OmsiCompatibilityManifest ToManifest(PublicRoomSummary room) => new(
        room.OmsiVersion,
        room.NavBRVersion,
        room.MapName,
        room.MapCompatibilityId,
        room.VehiclePath,
        room.VehicleCompatibilityId,
        room.HofName,
        room.HofCompatibilityId,
        room.PluginProtocolVersion,
        null,
        null);

    private static string ValueOrNotReported(string? value) => string.IsNullOrWhiteSpace(value)
        ? Pick("não informado", "not reported", "no informado", "nicht gemeldet", "non indiqué")
        : value.Trim();

    private static string ShortFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Pick("não informado", "not reported", "no informado", "nicht gemeldet", "non indiqué");
        }

        var normalized = value.Trim();
        return normalized[..Math.Min(16, normalized.Length)];
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private static string Pick(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private enum RoomCompatibilityLevel
    {
        Unknown,
        Compatible,
        Warning,
        Blocked
    }

    private sealed record PublicRoomListItem(PublicRoomSummary Room, string DisplayText);
    private sealed record DynamicFilterChoice(string Key, string Label);
    private sealed record CompatibilityFilterChoice(string Key, string Label);
    private sealed record PlayerFilterChoice(int MinimumPlayers, string Label);
}
