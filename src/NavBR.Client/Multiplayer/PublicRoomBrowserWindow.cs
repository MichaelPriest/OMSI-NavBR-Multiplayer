using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class PublicRoomBrowserWindow : Window
{
    private readonly string _serverUrl;
    private readonly PublicRoomDirectoryClient _directory = new();
    private readonly ListBox _rooms = new();
    private readonly TextBlock _status = new();
    private readonly Button _refreshButton = new();
    private readonly Button _selectButton = new();

    public string? SelectedRoomId { get; private set; }

    public PublicRoomBrowserWindow(string serverUrl)
    {
        _serverUrl = serverUrl;
        Title = Pick("Salas públicas", "Public rooms", "Salas públicas", "Öffentliche Räume", "Salons publics");
        Width = 620;
        Height = 480;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new TextBlock
        {
            Text = Pick(
                "Salas públicas neste servidor",
                "Public rooms on this server",
                "Salas públicas en este servidor",
                "Öffentliche Räume auf diesem Server",
                "Salons publics sur ce serveur"),
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        };
        root.Children.Add(heading);

        _status.Margin = new Thickness(0, 7, 0, 12);
        _status.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_status, 1);
        root.Children.Add(_status);

        _rooms.DisplayMemberPath = nameof(PublicRoomListItem.DisplayText);
        _rooms.SelectionChanged += (_, _) => _selectButton.IsEnabled = _rooms.SelectedItem is PublicRoomListItem;
        _rooms.MouseDoubleClick += (_, _) => AcceptSelection();
        Grid.SetRow(_rooms, 2);
        root.Children.Add(_rooms);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        _refreshButton.Content = Pick("Atualizar", "Refresh", "Actualizar", "Aktualisieren", "Actualiser");
        _refreshButton.MinWidth = 105;
        _refreshButton.Margin = new Thickness(0, 0, 8, 0);
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        actions.Children.Add(_refreshButton);

        _selectButton.Content = Pick("Usar sala", "Use room", "Usar sala", "Raum verwenden", "Utiliser le salon");
        _selectButton.MinWidth = 120;
        _selectButton.IsEnabled = false;
        _selectButton.Click += (_, _) => AcceptSelection();
        actions.Children.Add(_selectButton);

        Grid.SetRow(actions, 3);
        root.Children.Add(actions);
        Content = root;

        Loaded += async (_, _) => await RefreshAsync();
        Closed += (_, _) => _directory.Dispose();
    }

    private async Task RefreshAsync()
    {
        _refreshButton.IsEnabled = false;
        _selectButton.IsEnabled = false;
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
                    $"{rooms.Count} sala(s) pública(s) encontrada(s).",
                    $"{rooms.Count} public room(s) found.",
                    $"{rooms.Count} sala(s) pública(s) encontrada(s).",
                    $"{rooms.Count} öffentliche(r) Raum/Räume gefunden.",
                    $"{rooms.Count} salon(s) public(s) trouvé(s).");
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
            _selectButton.IsEnabled = _rooms.SelectedItem is PublicRoomListItem;
        }
    }

    private void AcceptSelection()
    {
        if (_rooms.SelectedItem is not PublicRoomListItem item)
        {
            return;
        }

        SelectedRoomId = item.Room.RoomId;
        DialogResult = true;
        Close();
    }

    private static string FormatRoom(PublicRoomSummary room)
    {
        var map = string.IsNullOrWhiteSpace(room.MapName)
            ? Pick("mapa não informado", "map not reported", "mapa no informado", "Karte nicht gemeldet", "carte non indiquée")
            : room.MapName;
        return $"{room.RoomId}   •   {room.PlayerCount} player(s)   •   {map}";
    }

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
