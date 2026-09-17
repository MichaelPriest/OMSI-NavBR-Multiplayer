using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class PublicRoomBrowserWindow : Window
{
    private readonly string _serverUrl;
    private readonly OmsiCompatibilityManifest? _localManifest;
    private readonly PublicRoomDirectoryClient _directory = new();
    private readonly ListBox _rooms = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _requirementsText = new();
    private readonly TextBlock _compatibilityText = new();
    private readonly Border _requirementsCard = new();
    private readonly Button _refreshButton = new();
    private readonly Button _selectButton = new();

    public string? SelectedRoomId { get; private set; }
    public PublicRoomSummary? SelectedRoom { get; private set; }

    public PublicRoomBrowserWindow(string serverUrl, OmsiCompatibilityManifest? localManifest = null)
    {
        _serverUrl = serverUrl;
        _localManifest = localManifest;
        Title = Pick("Salas públicas", "Public rooms", "Salas públicas", "Öffentliche Räume", "Salons publics");
        Width = 780;
        Height = 620;
        MinWidth = 640;
        MinHeight = 500;
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
                "Confira o mapa obrigatório e os requisitos antes de selecionar uma sala.",
                "Check the required map and requirements before selecting a room.",
                "Comprueba el mapa obligatorio y los requisitos antes de seleccionar una sala.",
                "Prüfe die erforderliche Karte und die Anforderungen, bevor du einen Raum auswählst.",
                "Vérifiez la carte requise et les prérequis avant de sélectionner un salon."),
            Foreground = Brush(143, 163, 177),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
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

        _selectButton.Content = Pick("Selecionar sala", "Select room", "Seleccionar sala", "Raum auswählen", "Sélectionner le salon");
        _selectButton.MinWidth = 140;
        _selectButton.Height = 36;
        _selectButton.IsEnabled = false;
        _selectButton.Click += (_, _) => AcceptSelection();
        actions.Children.Add(_selectButton);

        Grid.SetRow(actions, 4);
        root.Children.Add(actions);
        Content = root;

        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => _directory.Dispose();
    }

    private async Task RefreshAsync()
    {
        _refreshButton.IsEnabled = false;
        _selectButton.IsEnabled = false;
        _requirementsCard.Visibility = Visibility.Collapsed;
        _status.Text = Pick(
            "Consultando o servidor…",
            "Checking the server…",
            "Consultando el servidor…",
            "Server wird abgefragt…",
            "Interrogation du serveur…");

        try
        {
            var rooms = await _directory.GetRoomsAsync(_serverUrl);
            _rooms.ItemsSource = rooms
                .OrderByDescending(room => room.PlayerCount)
                .ThenBy(room => room.RoomId, StringComparer.CurrentCultureIgnoreCase)
                .Select(room => new PublicRoomListItem(room, FormatRoom(room)))
                .ToArray();

            _status.Text = rooms.Count == 0
                ? Pick(
                    "Nenhuma sala pública ativa neste servidor. Salas privadas não aparecem nesta lista.",
                    "No public rooms are active on this server. Private rooms are not shown here.",
                    "No hay salas públicas activas en este servidor. Las salas privadas no aparecen aquí.",
                    "Auf diesem Server sind keine öffentlichen Räume aktiv. Private Räume werden hier nicht angezeigt.",
                    "Aucun salon public n’est actif sur ce serveur. Les salons privés ne sont pas affichés ici.")
                : Pick(
                    $"{rooms.Count} sala(s) pública(s) encontrada(s). Selecione uma para conferir o mapa obrigatório.",
                    $"{rooms.Count} public room(s) found. Select one to check the required map.",
                    $"{rooms.Count} sala(s) pública(s) encontrada(s). Selecciona una para comprobar el mapa obligatorio.",
                    $"{rooms.Count} öffentliche(r) Raum/Räume gefunden. Wähle einen Raum, um die erforderliche Karte zu prüfen.",
                    $"{rooms.Count} salon(s) public(s) trouvé(s). Sélectionnez-en un pour vérifier la carte requise.");
        }
        catch (Exception ex)
        {
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

    private void RenderSelection()
    {
        if (_rooms.SelectedItem is not PublicRoomListItem item)
        {
            _requirementsCard.Visibility = Visibility.Collapsed;
            _selectButton.IsEnabled = false;
            return;
        }

        var room = item.Room;
        _requirementsCard.Visibility = Visibility.Visible;
        _requirementsText.Text = BuildRequirementText(room);

        var compatibility = GetCompatibilityStatus(room);
        _compatibilityText.Text = compatibility.Text;
        _compatibilityText.Foreground = compatibility.Brush;

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

    private static string FormatRoom(PublicRoomSummary room)
    {
        var map = string.IsNullOrWhiteSpace(room.MapName)
            ? Pick("MAPA PENDENTE", "MAP PENDING", "MAPA PENDIENTE", "KARTE AUSSTEHEND", "CARTE EN ATTENTE")
            : room.MapName;
        var players = Pick("jogador(es)", "player(s)", "jugador(es)", "Spieler", "joueur(s)");
        return $"{room.RoomId}   •   {room.PlayerCount} {players}   •   {Pick("MAPA", "MAP", "MAPA", "KARTE", "CARTE")}: {map}";
    }

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

        var remote = new OmsiCompatibilityManifest(
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
        var report = OmsiCompatibilityEvaluator.Compare(_localManifest, remote);

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

    private sealed record PublicRoomListItem(PublicRoomSummary Room, string DisplayText);
}
