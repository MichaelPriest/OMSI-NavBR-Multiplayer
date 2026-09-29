using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class MultiplayerPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<NativePlayerRow> _players = new();
    private readonly ObservableCollection<NativePublicRoomRow> _publicRooms = new();
    private bool _applyingState;
    private string? _publicRoomServerUrl;

    public MultiplayerPage()
    {
        InitializeComponent();
        PlayersList.ItemsSource = _players;
        PublicRoomsList.ItemsSource = _publicRooms;
        ConnectionModeComboBox.SelectedIndex = 0;
        UpdateConnectionModeUi();
    }

    public void ApplyState(JsonElement state)
    {
        var multiplayer = JsonState.Property(state, "multiplayer");
        var connected = JsonState.Bool(multiplayer, "connected");
        var hostRunning = JsonState.Bool(multiplayer, "hostRunning");
        var transportMode =
            JsonState.String(multiplayer, "transportMode") ?? "none";

        ConnectionText.Text = connected
            ? JsonState.String(multiplayer, "connectionState") ?? "ONLINE"
            : "OFFLINE";
        PlayerCountText.Text =
            (JsonState.Int(multiplayer, "playerCount") ?? 0).ToString();
        SubtitleText.Text = connected
            ? $"Sala {JsonState.String(multiplayer, "roomId") ?? "NavBR"} · {transportMode}"
            : hostRunning
                ? "Hospedagem ativa · aguardando/conectando à sala."
                : "Escolha como deseja entrar ou hospedar a sessão.";

        var quality = JsonState.Property(multiplayer, "networkQuality");
        var latency = JsonState.Double(quality, "roundTripMs");
        var jitter = JsonState.Double(quality, "jitterMs");
        NetworkText.Text = latency is null
            ? JsonState.String(quality, "level") ?? "—"
            : $"{latency:0} ms · J {jitter ?? 0d:0}";

        _applyingState = true;
        try
        {
            if (ServerTextBox.FocusState == FocusState.Unfocused)
            {
                var preferredServer = SelectedConnectionMode == "relay-host"
                    ? JsonState.String(multiplayer, "relayServerUrl")
                    : JsonState.String(multiplayer, "serverUrl");
                ServerTextBox.Text =
                    preferredServer
                    ?? JsonState.String(multiplayer, "serverUrl")
                    ?? ServerTextBox.Text;
            }

            if (RoomTextBox.FocusState == FocusState.Unfocused)
            {
                RoomTextBox.Text =
                    JsonState.String(multiplayer, "roomId")
                    ?? RoomTextBox.Text;
            }

            if (DisplayNameTextBox.FocusState == FocusState.Unfocused)
            {
                DisplayNameTextBox.Text =
                    JsonState.String(multiplayer, "displayName")
                    ?? DisplayNameTextBox.Text;
            }

            PhysicalVehiclesToggle.IsOn =
                JsonState.Bool(multiplayer, "physicalVehiclesEnabled");

            if (connected || hostRunning)
            {
                PrivateRoomCheckBox.IsChecked =
                    JsonState.Bool(multiplayer, "roomIsPrivate");
            }
        }
        finally
        {
            _applyingState = false;
        }

        ConnectButton.IsEnabled = !connected && !hostRunning;
        DisconnectButton.IsEnabled = connected;
        StopHostButton.Visibility = hostRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

        var hostPort = JsonState.Int(multiplayer, "hostPort");
        var hostReachability =
            JsonState.String(multiplayer, "hostReachability") ?? "inactive";
        var internetInvite =
            JsonState.String(multiplayer, "internetInviteAddress");
        HostStatusText.Text = hostRunning
            ? $"Host ativo · TCP {hostPort?.ToString() ?? "27730"} · {hostReachability}"
            : connected
                ? $"Conectado via {transportMode}."
                : "Nenhuma hospedagem local ativa.";

        var invites = JsonState.Array(multiplayer, "inviteAddresses")
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString()
                : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
        if (!string.IsNullOrWhiteSpace(internetInvite))
        {
            invites.Insert(0, $"Internet: {internetInvite}");
        }

        InviteText.Text = invites.Count == 0
            ? "—"
            : string.Join(" · ", invites);

        ApplyPublicRooms(state);
        ApplyPlayers(multiplayer);
    }

    private void ApplyPublicRooms(JsonElement state)
    {
        var directory = JsonState.Property(state, "roomDirectory");
        _publicRoomServerUrl =
            JsonState.String(directory, "serverUrl")
            ?? _publicRoomServerUrl;

        var error = JsonState.String(directory, "error");
        _publicRooms.Clear();

        foreach (var room in JsonState.Array(directory, "rooms"))
        {
            var roomId = JsonState.String(room, "roomId");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                continue;
            }

            _publicRooms.Add(new NativePublicRoomRow(
                roomId,
                $"{JsonState.Int(room, "playerCount") ?? 0} jogadores",
                JsonState.String(room, "mapName") ?? "Mapa não informado",
                JsonState.Bool(room, "favorite") ? "★" : string.Empty));
        }

        PublicRoomStatusText.Text = !string.IsNullOrWhiteSpace(error)
            ? $"Falha ao consultar salas: {error}"
            : _publicRooms.Count == 0
                ? "Nenhuma sala pública carregada. Use Atualizar."
                : $"{_publicRooms.Count} sala(s) pública(s) disponível(is).";
    }

    private void ApplyPlayers(JsonElement multiplayer)
    {
        _players.Clear();
        foreach (var player in JsonState.Array(multiplayer, "players"))
        {
            var badge = JsonState.Property(player, "companyBadge");
            var employee = JsonState.String(badge, "employeeNumber");
            var badgeText = employee is null
                ? (JsonState.Bool(player, "isLocal") ? "VOCÊ" : "Sem crachá")
                : $"#{employee} · {JsonState.String(badge, "companyShortName") ?? "EMPRESA"} · {JsonState.String(badge, "role") ?? "membro"}";

            var physical = JsonState.Bool(player, "physicalVehicleSpawned")
                ? "FÍSICO"
                : JsonState.String(player, "physicalVehicleState") ?? "—";
            if (JsonState.Bool(player, "roleplayActive"))
            {
                physical += " · RP";
            }

            _players.Add(new NativePlayerRow(
                JsonState.String(player, "displayName") ?? "Jogador",
                badgeText,
                $"{JsonState.String(player, "line") ?? "—"} / {JsonState.String(player, "route") ?? "—"}",
                JsonState.String(player, "vehicleName") ?? "—",
                physical,
                JsonState.Double(player, "latencyMs") is double playerLatency
                    ? $"{playerLatency:0} ms"
                    : "—"));
        }
    }

    private string SelectedConnectionMode =>
        (ConnectionModeComboBox.SelectedItem as ComboBoxItem)?
            .Tag?
            .ToString()
        ?? "join-server";

    private void ConnectionMode_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateConnectionModeUi();

    private void UpdateConnectionModeUi()
    {
        if (ServerTextBox is null ||
            ModeDetailText is null ||
            ConnectButton is null)
        {
            return;
        }

        switch (SelectedConnectionMode)
        {
            case "create-online":
                ServerTextBox.IsEnabled = true;
                ServerTextBox.Header = "Servidor NavBR";
                ConnectButton.Content = "Criar sala online";
                ModeDetailText.Text =
                    "Cria a sala no servidor NavBR informado. Usa o servidor online padrão quando o campo estiver vazio e não exige abrir porta no roteador.";
                break;

            case "lan-host":
                ServerTextBox.IsEnabled = false;
                ServerTextBox.Header = "Servidor (não usado em LAN)";
                ConnectButton.Content = "Hospedar na LAN";
                ModeDetailText.Text =
                    "Abre um host local TCP 27730 para computadores da mesma rede. UPnP fica desativado.";
                break;

            case "internet-host":
                ServerTextBox.IsEnabled = false;
                ServerTextBox.Header = "Servidor (host direto)";
                ConnectButton.Content = "Hospedar na Internet";
                ModeDetailText.Text =
                    "Abre o host local e tenta mapear TCP 27730 automaticamente por UPnP. LAN continua disponível.";
                break;

            case "relay-host":
                ServerTextBox.IsEnabled = true;
                ServerTextBox.Header = "Relay personalizado";
                ConnectButton.Content = "Hospedar via Relay";
                ModeDetailText.Text =
                    "Cria a sala usando um relay personalizado. Use quando você possui outro relay ou não quer usar o servidor NavBR padrão.";
                break;

            default:
                ServerTextBox.IsEnabled = true;
                ServerTextBox.Header = "Servidor";
                ConnectButton.Content = "Entrar na sala";
                ModeDetailText.Text =
                    "Entra em uma sala existente. Informe servidor, sala, nome e senha caso a sala seja privada.";
                break;
        }
    }

    private async Task RunAsync(string command, object? payload = null)
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = "Multiplayer atualizado.";
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
        }
    }

    private object CommonRoomPayload(bool includeServer = false) =>
        includeServer
            ? new
            {
                serverUrl = ServerTextBox.Text.Trim(),
                roomId = RoomTextBox.Text.Trim(),
                displayName = DisplayNameTextBox.Text.Trim(),
                isPrivate = PrivateRoomCheckBox.IsChecked == true,
                roomPassword = PasswordTextBox.Password
            }
            : new
            {
                roomId = RoomTextBox.Text.Trim(),
                displayName = DisplayNameTextBox.Text.Trim(),
                isPrivate = PrivateRoomCheckBox.IsChecked == true,
                roomPassword = PasswordTextBox.Password
            };

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        switch (SelectedConnectionMode)
        {
            case "create-online":
                await RunAsync(
                    "createOnlineRoom",
                    CommonRoomPayload(includeServer: true));
                break;

            case "lan-host":
                await RunAsync(
                    "createLocalRoom",
                    new
                    {
                        roomId = RoomTextBox.Text.Trim(),
                        displayName = DisplayNameTextBox.Text.Trim(),
                        isPrivate = PrivateRoomCheckBox.IsChecked == true,
                        roomPassword = PasswordTextBox.Password,
                        useRelay = false,
                        exposeInternet = false
                    });
                break;

            case "internet-host":
                await RunAsync(
                    "createLocalRoom",
                    new
                    {
                        roomId = RoomTextBox.Text.Trim(),
                        displayName = DisplayNameTextBox.Text.Trim(),
                        isPrivate = PrivateRoomCheckBox.IsChecked == true,
                        roomPassword = PasswordTextBox.Password,
                        useRelay = false,
                        exposeInternet = true
                    });
                break;

            case "relay-host":
                await RunAsync(
                    "createLocalRoom",
                    new
                    {
                        roomId = RoomTextBox.Text.Trim(),
                        displayName = DisplayNameTextBox.Text.Trim(),
                        isPrivate = PrivateRoomCheckBox.IsChecked == true,
                        roomPassword = PasswordTextBox.Password,
                        useRelay = true,
                        exposeInternet = false,
                        relayServerUrl = ServerTextBox.Text.Trim()
                    });
                break;

            default:
                await RunAsync(
                    "connectRoom",
                    new
                    {
                        serverUrl = ServerTextBox.Text.Trim(),
                        roomId = RoomTextBox.Text.Trim(),
                        displayName = DisplayNameTextBox.Text.Trim(),
                        roomPassword = PasswordTextBox.Password
                    });
                break;
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("disconnectRoom");

    private async void StopHost_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("stopLocalHost");

    private async void RefreshPublicRooms_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunAsync(
            "refreshPublicRooms",
            new { serverUrl = ServerTextBox.Text.Trim() });
    }

    private async void JoinPublicRoom_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (PublicRoomsList.SelectedItem is not NativePublicRoomRow room)
        {
            NoticeBar.Message = "Selecione uma sala pública primeiro.";
            NoticeBar.Severity = InfoBarSeverity.Warning;
            NoticeBar.IsOpen = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_publicRoomServerUrl))
        {
            ServerTextBox.Text = _publicRoomServerUrl;
        }

        RoomTextBox.Text = room.RoomId;
        ConnectionModeComboBox.SelectedIndex = 0;

        await RunAsync(
            "connectRoom",
            new
            {
                serverUrl = ServerTextBox.Text.Trim(),
                roomId = room.RoomId,
                displayName = DisplayNameTextBox.Text.Trim(),
                roomPassword = PasswordTextBox.Password
            });
    }

    private async void PhysicalVehicles_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_applyingState)
        {
            return;
        }

        await RunAsync(
            "setPhysicalVehiclesEnabled",
            new { enabled = PhysicalVehiclesToggle.IsOn });
    }
}

public sealed record NativePlayerRow(
    string DisplayName,
    string Badge,
    string Service,
    string Vehicle,
    string PhysicalState,
    string Latency);

public sealed record NativePublicRoomRow(
    string RoomId,
    string Players,
    string MapName,
    string Favorite);
