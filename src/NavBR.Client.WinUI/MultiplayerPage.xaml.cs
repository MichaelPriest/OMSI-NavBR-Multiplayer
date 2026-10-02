using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace NavBR.Client.WinUI;

public sealed partial class MultiplayerPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<NativePlayerRow> _players = new();
    private readonly ObservableCollection<NativePublicRoomRow> _publicRooms = new();
    private readonly ObservableCollection<NativeChatRow> _chat = new();
    private readonly ObservableCollection<NativeVoiceDeviceOption> _voiceInputDevices = new();
    private readonly ObservableCollection<NativeVoiceDeviceOption> _voiceOutputDevices = new();
    private readonly ObservableCollection<NativeVoiceMixerRow> _voiceMixers = new();
    private bool _applyingState;
    private string? _publicRoomServerUrl;
    private string? _configuredServerUrl;
    private string? _defaultOnlineServerUrl;
    private string? _relayServerUrl;
    private string? _lastInviteServerUrl;
    private string? _lastInviteRoomId;
    private string _lastInviteMode = "peer-host";
    private string? _lastChatSignature;
    private string? _lastPublicRoomsSignature;

    public MultiplayerPage()
    {
        InitializeComponent();
        PlayersList.ItemsSource = _players;
        PublicRoomsList.ItemsSource = _publicRooms;
        ChatList.ItemsSource = _chat;
        VoiceInputComboBox.ItemsSource = _voiceInputDevices;
        VoiceOutputComboBox.ItemsSource = _voiceOutputDevices;
        VoiceMixersList.ItemsSource = _voiceMixers;
        ConnectionModeComboBox.SelectedIndex = 0;
        VoiceChannelComboBox.SelectedIndex = 0;
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

        _configuredServerUrl =
            JsonState.String(multiplayer, "serverUrl")
            ?? _configuredServerUrl;
        _defaultOnlineServerUrl =
            JsonState.String(multiplayer, "defaultOnlineServerUrl")
            ?? _defaultOnlineServerUrl;
        _relayServerUrl =
            JsonState.String(multiplayer, "relayServerUrl")
            ?? _relayServerUrl;

        _applyingState = true;
        try
        {
            if (ServerTextBox.FocusState == FocusState.Unfocused)
            {
                ServerTextBox.Text =
                    PreferredServerForMode(SelectedConnectionMode)
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

        _lastInviteRoomId =
            JsonState.String(multiplayer, "roomId");
        _lastInviteServerUrl = hostRunning
            ? !string.IsNullOrWhiteSpace(internetInvite)
                ? internetInvite
                : invites.FirstOrDefault(value =>
                    !value.StartsWith("Internet:", StringComparison.OrdinalIgnoreCase))
            : JsonState.String(multiplayer, "serverUrl");
        _lastInviteMode =
            transportMode == "dedicated-server"
                ? "relay"
                : "peer-host";

        ApplyConnectivity(state, hostRunning);
        ApplyPublicRooms(state);
        ApplyVoice(multiplayer, connected);
        ApplyChat(multiplayer);
        ApplyPlayers(multiplayer);
    }

    private void ApplyConnectivity(
        JsonElement state,
        bool hostRunning)
    {
        var network = JsonState.Property(state, "network");
        var diagnostics = JsonState.Property(network, "diagnostics");
        var probe = JsonState.Property(network, "externalProbe");

        _applyingState = true;
        try
        {
            AutomaticUpnpToggle.IsOn =
                JsonState.Bool(network, "automaticUpnpEnabled");
        }
        finally
        {
            _applyingState = false;
        }

        var networkError = JsonState.String(network, "error");
        var networkMessage = JsonState.String(network, "message");
        ConnectivityStatusText.Text =
            !string.IsNullOrWhiteSpace(networkError)
                ? networkError
                : !string.IsNullOrWhiteSpace(networkMessage)
                    ? networkMessage
                    : "Diagnóstico de rede disponível sob demanda.";

        var firewallPresent =
            JsonState.Bool(diagnostics, "firewallRulePresent");
        var listening =
            JsonState.Bool(diagnostics, "localPortListening");
        var gatewayFound =
            JsonState.Bool(diagnostics, "upnpGatewayFound");
        var environment =
            JsonState.String(diagnostics, "environmentKind");
        var externalVerified =
            JsonState.Bool(diagnostics, "externalPortVerified");

        var probeStatus =
            JsonState.String(probe, "status");
        var probeReachable =
            JsonState.Bool(probe, "reachable");
        var probeDuration =
            JsonState.Double(probe, "durationMilliseconds");

        var diagnosticParts = new List<string>();
        if (diagnostics.ValueKind is not (
                JsonValueKind.Undefined or
                JsonValueKind.Null))
        {
            diagnosticParts.Add(
                listening
                    ? "TCP 27730 escutando"
                    : "TCP 27730 não está escutando");
            diagnosticParts.Add(
                firewallPresent
                    ? "Firewall OK"
                    : "Regra de Firewall ausente/não confirmada");
            diagnosticParts.Add(
                gatewayFound
                    ? "gateway UPnP encontrado"
                    : "gateway UPnP não confirmado");
            if (!string.IsNullOrWhiteSpace(environment))
            {
                diagnosticParts.Add($"NAT: {environment}");
            }

            if (externalVerified)
            {
                diagnosticParts.Add("porta externa verificada");
            }
        }

        if (!string.IsNullOrWhiteSpace(probeStatus))
        {
            diagnosticParts.Add(
                probeReachable
                    ? $"teste externo OK ({probeDuration ?? 0d:0} ms)"
                    : $"teste externo: {probeStatus}");
        }

        ConnectivityDetailText.Text =
            diagnosticParts.Count == 0
                ? "—"
                : string.Join(" · ", diagnosticParts);

        ExternalPortProbeButton.IsEnabled =
            hostRunning &&
            JsonState.Bool(network, "externalProbeConfigured");
    }

    private void ApplyPublicRooms(JsonElement state)
    {
        var directory = JsonState.Property(state, "roomDirectory");
        _publicRoomServerUrl =
            JsonState.String(directory, "serverUrl")
            ?? _publicRoomServerUrl;

        var error = JsonState.String(directory, "error");
        var nextRooms = JsonState.Array(directory, "rooms")
            .Select(room =>
            {
                var roomId = JsonState.String(room, "roomId");
                return string.IsNullOrWhiteSpace(roomId)
                    ? null
                    : new NativePublicRoomRow(
                        roomId,
                        $"{JsonState.Int(room, "playerCount") ?? 0} jogadores",
                        JsonState.String(room, "mapName") ?? "Mapa não informado",
                        JsonState.Bool(room, "favorite") ? "★" : string.Empty);
            })
            .Where(room => room is not null)
            .Cast<NativePublicRoomRow>()
            .ToArray();

        // The native shell receives full state snapshots at telemetry cadence.
        // Rebuilding the ListView on every frame clears WinUI selection/focus,
        // which made a public room visibly blink and nearly impossible to
        // select. Only rebuild when the visible directory contents changed.
        var nextSignature = string.Join(
            "\u001F",
            nextRooms.Select(room =>
                $"{room.RoomId}\u001E{room.Players}\u001E{room.MapName}\u001E{room.Favorite}"));
        if (!string.Equals(
                nextSignature,
                _lastPublicRoomsSignature,
                StringComparison.Ordinal))
        {
            var selectedRoomId =
                (PublicRoomsList.SelectedItem as NativePublicRoomRow)?.RoomId;
            _lastPublicRoomsSignature = nextSignature;
            _publicRooms.Clear();
            foreach (var room in nextRooms)
            {
                _publicRooms.Add(room);
            }

            if (!string.IsNullOrWhiteSpace(selectedRoomId))
            {
                PublicRoomsList.SelectedItem = _publicRooms.FirstOrDefault(room =>
                    string.Equals(
                        room.RoomId,
                        selectedRoomId,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        PublicRoomStatusText.Text = !string.IsNullOrWhiteSpace(error)
            ? $"Falha ao consultar salas: {error}"
            : _publicRooms.Count == 0
                ? "Nenhuma sala pública carregada. Use Atualizar."
                : $"{_publicRooms.Count} sala(s) pública(s) disponível(is).";
    }

    private void ApplyVoice(
        JsonElement multiplayer,
        bool connected)
    {
        var selectedMixerId =
            (VoiceMixersList.SelectedItem as NativeVoiceMixerRow)?.PlayerId;

        _applyingState = true;
        try
        {
            VoiceEnabledToggle.IsOn =
                JsonState.Bool(multiplayer, "voiceEnabled");
            VoiceDeafenedToggle.IsOn =
                JsonState.Bool(multiplayer, "voiceDeafened");

            var channel =
                JsonState.String(multiplayer, "voiceChannel")
                ?? "general";
            var channelItem = VoiceChannelComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    string.Equals(
                        item.Tag?.ToString(),
                        channel,
                        StringComparison.OrdinalIgnoreCase));
            if (channelItem is not null)
            {
                VoiceChannelComboBox.SelectedItem = channelItem;
            }

            var proximity =
                JsonState.Double(multiplayer, "voiceProximityMeters");
            if (proximity is double radius &&
                double.IsFinite(radius))
            {
                VoiceProximityNumberBox.Value =
                    Math.Clamp(radius, 20d, 1000d);
            }

            SyncVoiceDevices(
                _voiceInputDevices,
                JsonState.Array(multiplayer, "voiceInputDevices"));
            SyncVoiceDevices(
                _voiceOutputDevices,
                JsonState.Array(multiplayer, "voiceOutputDevices"));

            var inputNumber =
                JsonState.Int(multiplayer, "voiceInputDeviceNumber");
            var outputNumber =
                JsonState.Int(multiplayer, "voiceOutputDeviceNumber");
            if (inputNumber is int input)
            {
                VoiceInputComboBox.SelectedValue = input;
            }
            if (outputNumber is int output)
            {
                VoiceOutputComboBox.SelectedValue = output;
            }
        }
        finally
        {
            _applyingState = false;
        }

        _voiceMixers.Clear();
        foreach (var mixer in JsonState.Array(multiplayer, "voiceMixers"))
        {
            var playerId =
                JsonState.String(mixer, "playerId");
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var muted =
                JsonState.Bool(mixer, "muted");
            var speaking =
                JsonState.Bool(mixer, "speaking");
            var gain =
                JsonState.Double(mixer, "gain") ?? 1d;

            _voiceMixers.Add(new NativeVoiceMixerRow(
                playerId,
                JsonState.String(mixer, "displayName") ?? "Jogador",
                muted,
                Math.Clamp(gain, 0d, 2d),
                speaking,
                muted
                    ? "MUDO"
                    : speaking
                        ? "FALANDO"
                        : "OUVINDO",
                $"{Math.Clamp(gain, 0d, 2d) * 100d:0}%"));
        }

        if (!string.IsNullOrWhiteSpace(selectedMixerId))
        {
            var selected = _voiceMixers.FirstOrDefault(item =>
                string.Equals(
                    item.PlayerId,
                    selectedMixerId,
                    StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                VoiceMixersList.SelectedItem = selected;
                RemoteVoiceGainNumberBox.Value = selected.Gain;
            }
        }

        var quality =
            JsonState.Property(multiplayer, "voiceQuality");
        var activeStreams =
            JsonState.Int(quality, "activeStreams") ?? 0;
        var jitter =
            JsonState.Double(quality, "averageJitterMilliseconds") ?? 0d;
        var loss =
            JsonState.Double(quality, "estimatedLossPercent") ?? 0d;
        var targetBuffer =
            JsonState.Int(quality, "targetBufferMilliseconds") ?? 0;

        VoiceQualityText.Text =
            $"Qualidade: {activeStreams} stream(s) · jitter {jitter:0.0} ms · " +
            $"perda {loss:0.0}% · buffer {targetBuffer} ms";

        var channelLabel =
            (VoiceChannelComboBox.SelectedItem as ComboBoxItem)?
                .Content?
                .ToString()
            ?? "Geral";
        VoiceStatusText.Text =
            VoiceEnabledToggle.IsOn
                ? connected
                    ? $"{channelLabel} · {(VoiceDeafenedToggle.IsOn ? "recepção silenciada" : "PTT pronto")}"
                    : $"{channelLabel} · voz configurada; conecte a uma sala"
                : "Voz desativada.";
    }

    private static void SyncVoiceDevices(
        ObservableCollection<NativeVoiceDeviceOption> target,
        IEnumerable<JsonElement> source)
    {
        var next = source
            .Select(item => new NativeVoiceDeviceOption(
                JsonState.Int(item, "deviceNumber") ?? -1,
                JsonState.String(item, "displayName") ?? "Dispositivo"))
            .ToArray();

        if (target.Count == next.Length &&
            target.Zip(next).All(pair =>
                pair.First.DeviceNumber == pair.Second.DeviceNumber &&
                string.Equals(
                    pair.First.DisplayName,
                    pair.Second.DisplayName,
                    StringComparison.Ordinal)))
        {
            return;
        }

        target.Clear();
        foreach (var item in next)
        {
            target.Add(item);
        }
    }

    private void ApplyChat(JsonElement multiplayer)
    {
        var messages =
            JsonState.Array(multiplayer, "chat").ToArray();
        var last = messages.LastOrDefault();
        var signature = messages.Length == 0
            ? "0"
            : $"{messages.Length}|{JsonState.String(last, "timestampUtc")}|{JsonState.String(last, "playerId")}|{JsonState.String(last, "text")}";

        if (string.Equals(
                signature,
                _lastChatSignature,
                StringComparison.Ordinal))
        {
            return;
        }

        _lastChatSignature = signature;
        _chat.Clear();

        foreach (var message in messages)
        {
            var rawTimestamp =
                JsonState.String(message, "timestampUtc");
            var time = DateTimeOffset.TryParse(
                rawTimestamp,
                out var timestamp)
                ? timestamp.ToLocalTime().ToString("HH:mm")
                : string.Empty;

            var isSystem =
                JsonState.Bool(message, "isSystem");
            _chat.Add(new NativeChatRow(
                isSystem
                    ? "SISTEMA"
                    : JsonState.String(message, "displayName") ?? "Jogador",
                JsonState.String(message, "text") ?? string.Empty,
                time));
        }

        if (_chat.Count > 0)
        {
            ChatList.ScrollIntoView(_chat[^1]);
        }
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

            var isLocal = JsonState.Bool(player, "isLocal");
            var physical = JsonState.Bool(player, "physicalVehicleSpawned")
                ? "FÍSICO"
                : JsonState.String(player, "physicalVehicleState") ?? "—";
            if (JsonState.Bool(player, "roleplayActive"))
            {
                physical += " · RP";
            }

            var physicalDetailParts = new List<string>();
            if (isLocal)
            {
                physicalDetailParts.Add("veículo local");
            }
            else
            {
                var partCount =
                    JsonState.Int(player, "physicalVehiclePartCount");
                var expectedParts =
                    JsonState.Int(player, "physicalVehicleExpectedPartCount");
                if (partCount is int || expectedParts is int)
                {
                    var parts = partCount ?? 0;
                    physicalDetailParts.Add(
                        $"partes {parts}/{expectedParts ?? parts}");
                }

                var tile =
                    JsonState.Int(player, "physicalTelemetryRemoteTileIndex");
                var gridX =
                    JsonState.Int(player, "physicalTelemetryGridX");
                var gridY =
                    JsonState.Int(player, "physicalTelemetryGridY");
                if (tile is int tileIndex)
                {
                    physicalDetailParts.Add($"tile {tileIndex}");
                }
                if (gridX is int gx && gridY is int gy)
                {
                    physicalDetailParts.Add($"grid {gx},{gy}");
                }

                var telemetryAge =
                    JsonState.Double(player, "telemetryAgeSeconds");
                if (telemetryAge is double age)
                {
                    physicalDetailParts.Add(
                        JsonState.Bool(player, "telemetryStale")
                            ? $"tele {age:0.0}s STALE"
                            : $"tele {age:0.0}s");
                }

                var physicalUpdatedAt =
                    JsonState.String(player, "physicalVehicleUpdatedAtUtc");
                if (DateTimeOffset.TryParse(
                        physicalUpdatedAt,
                        out var updatedAt))
                {
                    var physicalAge =
                        Math.Max(
                            0d,
                            (DateTimeOffset.UtcNow - updatedAt).TotalSeconds);
                    physicalDetailParts.Add($"phys {physicalAge:0.0}s");
                }

                var errorCode =
                    JsonState.String(player, "physicalVehicleErrorCode");
                var errorMessage =
                    JsonState.String(player, "physicalVehicleErrorMessage");
                if (!string.IsNullOrWhiteSpace(errorCode))
                {
                    physicalDetailParts.Add($"erro {errorCode}");
                }
                else if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    physicalDetailParts.Add(
                        errorMessage.Length > 80
                            ? errorMessage[..80] + "…"
                            : errorMessage);
                }
            }

            _players.Add(new NativePlayerRow(
                JsonState.String(player, "displayName") ?? "Jogador",
                badgeText,
                $"{JsonState.String(player, "line") ?? "—"} / {JsonState.String(player, "route") ?? "—"}",
                JsonState.String(player, "vehicleName") ?? "—",
                physical,
                physicalDetailParts.Count == 0
                    ? "sem diagnóstico físico"
                    : string.Join(" · ", physicalDetailParts),
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
        SelectionChangedEventArgs e)
    {
        UpdateConnectionModeUi();
        if (_applyingState ||
            ServerTextBox is null)
        {
            return;
        }

        var preferred =
            PreferredServerForMode(SelectedConnectionMode);
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            ServerTextBox.Text = preferred;
        }
    }

    private string? PreferredServerForMode(string mode) =>
        mode switch
        {
            "create-online" =>
                _defaultOnlineServerUrl
                ?? _relayServerUrl
                ?? _configuredServerUrl,
            "relay-host" =>
                _relayServerUrl
                ?? _defaultOnlineServerUrl
                ?? _configuredServerUrl,
            "join-server" =>
                _configuredServerUrl
                ?? _defaultOnlineServerUrl,
            _ => _configuredServerUrl
        };

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
                    "Cria a sala no Servidor NavBR oficial. Não abre TCP 27730 no seu PC e funciona mesmo atrás de CGNAT.";
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

    private async Task RunAsync(
        string command,
        object? payload = null) =>
        _ = await TryRunAsync(command, payload);

    private async Task<bool> TryRunAsync(
        string command,
        object? payload = null)
    {
        try
        {
            if (CommandHandler is null)
            {
                return false;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = "Multiplayer atualizado.";
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
            return false;
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

    private async void TogglePublicRoomFavorite_Click(
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

        await RunAsync(
            "toggleRoomFavorite",
            new { roomId = room.RoomId });

        if (!string.IsNullOrWhiteSpace(_publicRoomServerUrl))
        {
            await RunAsync(
                "refreshPublicRooms",
                new { serverUrl = _publicRoomServerUrl });
        }
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

    private void CopyInvite_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastInviteServerUrl) ||
            string.IsNullOrWhiteSpace(_lastInviteRoomId))
        {
            NoticeBar.Message =
                "Hospede ou conecte a uma sala antes de copiar o convite.";
            NoticeBar.Severity = InfoBarSeverity.Warning;
            NoticeBar.IsOpen = true;
            return;
        }

        var invite = string.Join(
            Environment.NewLine,
            "NAVBR_INVITE_V1",
            $"server={_lastInviteServerUrl}",
            $"room={_lastInviteRoomId}",
            $"mode={_lastInviteMode}");

        var package = new DataPackage();
        package.SetText(invite);
        Clipboard.SetContent(package);
        Clipboard.Flush();

        NoticeBar.Message = "Convite NavBR copiado.";
        NoticeBar.Severity = InfoBarSeverity.Success;
        NoticeBar.IsOpen = true;
    }

    private async void PasteInvite_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                throw new InvalidOperationException(
                    "A área de transferência não contém um convite em texto.");
            }

            var text = (await content.GetTextAsync()).Trim();
            if (!TryParseInvite(
                    text,
                    out var serverUrl,
                    out var roomId))
            {
                throw new InvalidOperationException(
                    "O texto copiado não é um convite NavBR válido.");
            }

            ServerTextBox.Text = serverUrl;
            RoomTextBox.Text = roomId;
            ConnectionModeComboBox.SelectedIndex = 0;
            NoticeBar.Message =
                "Convite carregado. Confira seu nome e clique em Entrar na sala.";
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

    private static bool TryParseInvite(
        string text,
        out string serverUrl,
        out string roomId)
    {
        serverUrl = string.Empty;
        roomId = string.Empty;

        var lines = text
            .Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return false;
        }

        if (string.Equals(
                lines[0],
                "NAVBR_INVITE_V1",
                StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in lines.Skip(1))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 ||
                    separator >= line.Length - 1)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (string.Equals(
                        key,
                        "server",
                        StringComparison.OrdinalIgnoreCase))
                {
                    serverUrl = value;
                }
                else if (string.Equals(
                             key,
                             "room",
                             StringComparison.OrdinalIgnoreCase))
                {
                    roomId = value;
                }
            }
        }
        else
        {
            var serverIndex = Array.FindIndex(
                lines,
                line =>
                    line.StartsWith(
                        "http://",
                        StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(
                        "https://",
                        StringComparison.OrdinalIgnoreCase));
            if (serverIndex >= 0)
            {
                serverUrl = lines[serverIndex];
                if (serverIndex + 1 < lines.Length)
                {
                    roomId = lines[serverIndex + 1];
                }
            }
        }

        return Uri.TryCreate(
                   serverUrl,
                   UriKind.Absolute,
                   out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps) &&
               roomId.Length is > 0 and <= 64;
    }

    private async void RefreshNetworkDiagnostics_Click(
        object sender,
        RoutedEventArgs e) =>
        await RunAsync("refreshNetworkDiagnostics");

    private async void ApplyFirewall_Click(
        object sender,
        RoutedEventArgs e) =>
        await RunAsync("applyFirewallRule");

    private async void RunExternalPortProbe_Click(
        object sender,
        RoutedEventArgs e) =>
        await RunAsync("runExternalPortProbe");

    private async void AutomaticUpnp_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_applyingState)
        {
            return;
        }

        await RunAsync(
            "setAutomaticUpnp",
            new { enabled = AutomaticUpnpToggle.IsOn });
    }

    private async void VoiceEnabled_Toggled(
        object sender,
        RoutedEventArgs e)
    {
        if (_applyingState)
        {
            return;
        }

        await RunAsync(
            "setVoiceEnabled",
            new { enabled = VoiceEnabledToggle.IsOn });
    }

    private async void ApplyVoiceSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        var channel =
            (VoiceChannelComboBox.SelectedItem as ComboBoxItem)?
                .Tag?
                .ToString()
            ?? "general";
        var proximity =
            double.IsFinite(VoiceProximityNumberBox.Value)
                ? VoiceProximityNumberBox.Value
                : 120d;

        var configured = await TryRunAsync(
            "configureVoice",
            new
            {
                channel,
                proximityMeters = Math.Clamp(proximity, 20d, 1000d),
                deafened = VoiceDeafenedToggle.IsOn
            });

        if (!configured)
        {
            return;
        }

        await RunAsync(
            "configureVoiceDevices",
            new
            {
                inputDeviceNumber =
                    VoiceInputComboBox.SelectedValue is int input
                        ? input
                        : (int?)null,
                outputDeviceNumber =
                    VoiceOutputComboBox.SelectedValue is int output
                        ? output
                        : (int?)null
            });
    }

    private async void ApplyRemoteVoiceGain_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (VoiceMixersList.SelectedItem is not NativeVoiceMixerRow mixer)
        {
            NoticeBar.Message =
                "Selecione um jogador na lista de voz.";
            NoticeBar.Severity = InfoBarSeverity.Warning;
            NoticeBar.IsOpen = true;
            return;
        }

        var gain =
            double.IsFinite(RemoteVoiceGainNumberBox.Value)
                ? Math.Clamp(
                    RemoteVoiceGainNumberBox.Value,
                    0d,
                    2d)
                : mixer.Gain;

        await RunAsync(
            "configureRemoteVoice",
            new
            {
                playerId = mixer.PlayerId,
                muted = mixer.Muted,
                gain
            });
    }

    private async void ToggleRemoteVoiceMute_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (VoiceMixersList.SelectedItem is not NativeVoiceMixerRow mixer)
        {
            NoticeBar.Message =
                "Selecione um jogador na lista de voz.";
            NoticeBar.Severity = InfoBarSeverity.Warning;
            NoticeBar.IsOpen = true;
            return;
        }

        await RunAsync(
            "configureRemoteVoice",
            new
            {
                playerId = mixer.PlayerId,
                muted = !mixer.Muted,
                gain = mixer.Gain
            });
    }

    private async void SendChat_Click(
        object sender,
        RoutedEventArgs e)
    {
        var textValue = ChatTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(textValue))
        {
            return;
        }

        if (await TryRunAsync(
                "sendChat",
                new { text = textValue }))
        {
            ChatTextBox.Text = string.Empty;
        }
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
    string PhysicalDetail,
    string Latency);

public sealed record NativePublicRoomRow(
    string RoomId,
    string Players,
    string MapName,
    string Favorite);

public sealed record NativeChatRow(
    string DisplayName,
    string Text,
    string Time);

public sealed record NativeVoiceDeviceOption(
    int DeviceNumber,
    string DisplayName);

public sealed record NativeVoiceMixerRow(
    string PlayerId,
    string DisplayName,
    bool Muted,
    double Gain,
    bool Speaking,
    string State,
    string GainText);
