using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow : Window
{
    private const int DefaultHostPort = 27730;

    private readonly Func<VehicleTelemetry?> _telemetrySource;
    private readonly Func<OmsiMapInfo?> _activeMapSource;
    private readonly Func<IReadOnlyList<RoleplayCharacterOption>> _roleplayCharacterOptionsSource;
    private readonly MultiplayerClientService _client = new();
    private readonly RoomHostService _host = new();
    private readonly VoiceChatService _voiceChat = new();
    private readonly DispatcherTimer _publishTimer;
    private readonly SemaphoreSlim _voiceSendGate = new(1, 1);
    private readonly Dictionary<string, PlayerPresence> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VehicleTelemetry> _remoteTelemetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoleplayCharacterFrame> _remoteRoleplayCharacters = new(StringComparer.OrdinalIgnoreCase);
    private RoleplayCharacterState? _localRoleplayCharacter;
    private readonly List<ChatMessage> _chatMessages = [];

    private MultiplayerSettings _settings;
    private bool _publishing;

    public event Action<PlayerTelemetryFrame>? RemoteTelemetryReceived;
    public event Action<string>? RemotePlayerLeft;
    public event Action? RemotePlayersReset;
    public event Action<ChatMessage>? ChatMessageReceived;
    public event Action<string, string>? RemoteSpeakerActive;
    public event Action<string>? VoiceError;
    public event Action<bool, string?>? MultiplayerConnectionChanged;
    public event Action<string>? LocalDisplayNameChanged;

    public bool IsConnected => _client.IsConnected;
    public string CurrentDisplayName => _settings.DisplayName;
    public string CurrentRoomId => _settings.RoomId;

    public MultiplayerWindow(
        Func<VehicleTelemetry?> telemetrySource,
        Func<OmsiMapInfo?> activeMapSource)
        : this(telemetrySource, activeMapSource, null)
    {
    }

    internal MultiplayerWindow(
        Func<VehicleTelemetry?> telemetrySource,
        Func<OmsiMapInfo?> activeMapSource,
        Func<IReadOnlyList<RoleplayCharacterOption>>? roleplayCharacterOptionsSource)
    {
        _telemetrySource = telemetrySource;
        _activeMapSource = activeMapSource;
        _roleplayCharacterOptionsSource =
            roleplayCharacterOptionsSource ?? (() => Array.Empty<RoleplayCharacterOption>());
        _settings = MultiplayerSettingsStore.Load();

        InitializeComponent();

        _publishTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _publishTimer.Tick += PublishTimer_Tick;

        _client.ConnectionStateChanged += state => Dispatcher.BeginInvoke(() => RenderConnectionState(state));
        _client.RoomSnapshotReceived += snapshot => Dispatcher.BeginInvoke(() => ApplySnapshot(snapshot));
        _client.PlayerJoined += presence => Dispatcher.BeginInvoke(() => UpsertPresence(presence));
        _client.PlayerPresenceChanged += presence => Dispatcher.BeginInvoke(() => UpsertPresence(presence));
        _client.PlayerLeft += playerId => Dispatcher.BeginInvoke(() => RemovePlayer(playerId));
        _client.TelemetryReceived += frame => Dispatcher.BeginInvoke(() => ApplyRemoteTelemetry(frame));
        _client.RoleplayCharacterReceived += frame =>
            Dispatcher.BeginInvoke(() => ApplyRemoteRoleplayCharacter(frame));
        _client.RoleplayCharacterRemoved += playerId =>
            Dispatcher.BeginInvoke(() => RemoveRemoteRoleplayCharacter(playerId));
        _client.ChatMessageReceived += message => Dispatcher.BeginInvoke(() => ApplyChatMessage(message));
        _client.VoiceFrameReceived += frame =>
        {
            if (VoiceEnabledCheckBox.Dispatcher.CheckAccess())
            {
                if (VoiceEnabledCheckBox.IsChecked == true)
                {
                    _voiceChat.Receive(frame);
                }
            }
            else
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (VoiceEnabledCheckBox.IsChecked == true)
                    {
                        _voiceChat.Receive(frame);
                    }
                });
            }
        };

        _voiceChat.EncodedFrameReady += (sequence, payload) =>
            _ = PublishVoiceFrameSafeAsync(sequence, payload);
        _voiceChat.RemoteSpeakerActive += playerId =>
            Dispatcher.BeginInvoke(() =>
            {
                var displayName = _players.TryGetValue(playerId, out var player)
                    ? player.DisplayName
                    : playerId;
                RemoteSpeakerActive?.Invoke(playerId, displayName);
            });
        _voiceChat.VoiceError += message => Dispatcher.BeginInvoke(() =>
        {
            VoiceError?.Invoke(message);
            StatusDetailText.Text = LocalizationService.Format("MultiplayerVoiceError", message);
        });

        Loaded += (_, _) =>
        {
            LoadSettingsIntoUi();
            ApplyLocalization();
            RenderConnectionState(HubConnectionState.Disconnected);
            RenderPlayers();
            RenderChat();
            InitializePhysicalVehiclesPublicTest();
            HookDiagnosticsLifecycle();
            InitializePersistentLifetime();
            InitializeRoleplayCharacterSelector();
        };

        Closed += async (_, _) =>
        {
            _publishTimer.Stop();
            _voiceChat.Dispose();
            await _client.DisposeAsync();
            await _host.DisposeAsync();
            _voiceSendGate.Dispose();
            RemotePlayersReset?.Invoke();
        };
    }

    public async Task SendChatFromOverlayAsync(string text)
    {
        if (!_client.IsConnected || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _client.SendChatMessageAsync(text.Trim());
    }

    public void SetPushToTalk(bool active)
    {
        if (!_client.IsConnected || VoiceEnabledCheckBox.IsChecked != true)
        {
            _voiceChat.SetPushToTalk(false);
            return;
        }

        _voiceChat.SetPushToTalk(active);
    }

    private void LoadSettingsIntoUi()
    {
        ServerTextBox.Text = _settings.ServerUrl;
        RoomTextBox.Text = _settings.RoomId;
        NicknameTextBox.Text = _settings.DisplayName;
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("MultiplayerWindowTitle");
        HeadingText.Text = LocalizationService.Get("MultiplayerHeading");
        DescriptionText.Text = LocalizationService.Get("MultiplayerPeerDescription");
        ServerLabelText.Text = LocalizationService.Get("MultiplayerHostAddress");
        RoomLabelText.Text = LocalizationService.Get("MultiplayerRoom");
        NicknameLabelText.Text = LocalizationService.Get("MultiplayerNickname");
        LocalMapLabelText.Text = LocalizationService.Get("MultiplayerLocalMap");
        VoiceEnabledCheckBox.Content = LocalizationService.Get("MultiplayerVoiceEnabled");
        PlayersHeadingText.Text = LocalizationService.Get("MultiplayerPlayers");
        ChatHeadingText.Text = LocalizationService.Get("MultiplayerChat");
        SendChatButton.Content = LocalizationService.Get("MultiplayerSend");
        FooterText.Text = LocalizationService.Get("MultiplayerPeerFooter");
        ApplyTabLocalization();
        UpdateLocalMapText();
        UpdateButtons();
        RenderPlayers();
        RenderChat();
    }

    private async void CreateRoomButton_Click(object sender, RoutedEventArgs e)
    {
        if (_host.IsRunning)
        {
            await DisconnectAsync();
            await _host.StopAsync();
            InviteAddressText.Text = string.Empty;
            SetInputsEnabled(true);
            UpdateButtons();
            return;
        }

        if (string.IsNullOrWhiteSpace(RoomTextBox.Text))
        {
            RoomTextBox.Text = $"navbr-{Random.Shared.Next(1000, 9999)}";
        }

        if (string.IsNullOrWhiteSpace(NicknameTextBox.Text))
        {
            StatusDetailText.Text = LocalizationService.Get("MultiplayerRequiredFields");
            return;
        }

        try
        {
            await _host.StartAsync(DefaultHostPort);
            ServerTextBox.Text = _host.LocalServerUrl;
            RenderInviteAddresses();
            UpdateButtons();
            await ConnectToConfiguredServerAsync();
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = LocalizationService.Format("MultiplayerHostError", ex.Message);
            await _host.StopAsync();
            UpdateButtons();
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client.State != HubConnectionState.Disconnected)
        {
            await DisconnectAsync();
            return;
        }

        await ConnectToConfiguredServerAsync();
    }

    private async Task ConnectToConfiguredServerAsync()
    {
        var serverUrl = ServerTextBox.Text.Trim();
        var roomId = RoomTextBox.Text.Trim();
        var displayName = NicknameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(roomId) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            StatusDetailText.Text = LocalizationService.Get("MultiplayerRequiredFields");
            return;
        }

        _settings = _settings with
        {
            ServerUrl = serverUrl,
            RoomId = roomId,
            DisplayName = displayName
        };
        MultiplayerSettingsStore.Save(_settings);
        LocalDisplayNameChanged?.Invoke(displayName);

        SetInputsEnabled(false);
        StatusDetailText.Text = LocalizationService.Get("MultiplayerConnectingDetail");

        try
        {
            var localTelemetry = _telemetrySource();
            var activeMap = _activeMapSource();
            var compatibility = OmsiCompatibilityManifestFactory.Create(
                localTelemetry,
                activeMap);
            var snapshot = await _client.ConnectAsync(
                _settings,
                localTelemetry?.MapName,
                activeMap?.CompatibilityId,
                compatibility);

            ApplySnapshot(snapshot);
            _publishTimer.Start();
            if (VoiceEnabledCheckBox.IsChecked == true)
            {
                _voiceChat.Start();
            }

            MultiplayerConnectionChanged?.Invoke(true, _settings.RoomId);
            await PublishLocalTelemetryAsync();
        }
        catch (Exception ex)
        {
            _publishTimer.Stop();
            _voiceChat.Stop();
            SetInputsEnabled(true);
            RenderConnectionState(HubConnectionState.Disconnected);
            StatusDetailText.Text = LocalizationService.Format(
                "MultiplayerConnectionError",
                ex.Message);
        }
    }

    private async Task DisconnectAsync()
    {
        _publishTimer.Stop();
        _voiceChat.Stop();
        await _client.DisconnectAsync();
        _players.Clear();
        _remoteTelemetry.Clear();
        SetInputsEnabled(true);
        RemotePlayersReset?.Invoke();
        MultiplayerConnectionChanged?.Invoke(false, null);
        RenderPlayers();
        RenderConnectionState(HubConnectionState.Disconnected);
    }

    private async void PublishTimer_Tick(object? sender, EventArgs e)
    {
        await PublishLocalTelemetryAsync();
        UpdateLocalMapText();
        RenderPlayers();
    }

    private async Task PublishLocalTelemetryAsync()
    {
        if (_publishing || !_client.IsConnected)
        {
            return;
        }

        var telemetry = _telemetrySource();
        if (telemetry is null)
        {
            return;
        }

        _publishing = true;
        try
        {
            var outgoing = telemetry with
            {
                PlayerId = _settings.PlayerId,
                Timestamp = DateTimeOffset.UtcNow
            };
            await _client.PublishTelemetryAsync(outgoing);
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = LocalizationService.Format(
                "MultiplayerTelemetryError",
                ex.Message);
        }
        finally
        {
            _publishing = false;
        }
    }

    private async Task PublishVoiceFrameSafeAsync(long sequence, byte[] payload)
    {
        if (!_client.IsConnected || VoiceEnabledCheckBox.IsChecked != true)
        {
            return;
        }

        if (!await _voiceSendGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            await _client.PublishVoiceFrameAsync(sequence, payload);
        }
        catch (Exception ex)
        {
            _ = Dispatcher.BeginInvoke(() => StatusDetailText.Text =
                LocalizationService.Format("MultiplayerVoiceError", ex.Message));
        }
        finally
        {
            _voiceSendGate.Release();
        }
    }

    private void ApplySnapshot(RoomSnapshot snapshot)
    {
        _players.Clear();
        foreach (var player in snapshot.Players)
        {
            _players[player.PlayerId] = player;
        }

        foreach (var stale in _remoteRoleplayCharacters.Keys
                     .Where(playerId => !_players.ContainsKey(playerId))
                     .ToArray())
        {
            _remoteRoleplayCharacters.Remove(stale);
        }

        RenderPlayers();
        RenderLiveSessionView();
    }

    private void UpsertPresence(PlayerPresence presence)
    {
        _players[presence.PlayerId] = presence;
        RenderPlayers();
    }

    private void ApplyRemoteTelemetry(PlayerTelemetryFrame frame)
    {
        _players[frame.Player.PlayerId] = frame.Player;
        _remoteTelemetry[frame.Player.PlayerId] = frame.Telemetry;
        RemoteTelemetryReceived?.Invoke(frame);
        RenderPlayers();
        RenderLiveSessionView();
    }

    private void ApplyRemoteRoleplayCharacter(RoleplayCharacterFrame frame)
    {
        _players[frame.Player.PlayerId] = frame.Player;
        _remoteRoleplayCharacters[frame.Player.PlayerId] = frame;
        RenderPlayers();
        RenderLiveSessionView();
    }

    private void RemoveRemoteRoleplayCharacter(string playerId)
    {
        _remoteRoleplayCharacters.Remove(playerId);
        RenderPlayers();
        RenderLiveSessionView();
    }

    internal void SetLocalRoleplayCharacterState(RoleplayCharacterState? state)
    {
        _localRoleplayCharacter = state;
        RenderPlayers();
        RenderLiveSessionView();
    }

    private void RemovePlayer(string playerId)
    {
        _players.Remove(playerId);
        _remoteTelemetry.Remove(playerId);
        _remoteRoleplayCharacters.Remove(playerId);
        _voiceChat.RemoveRemotePlayer(playerId);
        RemotePlayerLeft?.Invoke(playerId);
        RenderPlayers();
    }

    private void ApplyChatMessage(ChatMessage message)
    {
        _chatMessages.Add(message);
        if (_chatMessages.Count > 80)
        {
            _chatMessages.RemoveRange(0, _chatMessages.Count - 80);
        }

        RenderChat();
        ChatMessageReceived?.Invoke(message);
    }

    private void RenderChat()
    {
        ChatListBox.ItemsSource = _chatMessages
            .Select(message => $"[{message.TimestampUtc.ToLocalTime():HH:mm}] {message.DisplayName}: {message.Text}")
            .ToArray();

        if (ChatListBox.Items.Count > 0)
        {
            ChatListBox.ScrollIntoView(ChatListBox.Items[^1]);
        }
    }

    private async void SendChatButton_Click(object sender, RoutedEventArgs e)
    {
        await SendChatFromInputAsync();
    }

    private async void ChatTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        await SendChatFromInputAsync();
        e.Handled = true;
    }

    private async Task SendChatFromInputAsync()
    {
        var text = ChatTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text) || !_client.IsConnected)
        {
            return;
        }

        try
        {
            await _client.SendChatMessageAsync(text);
            ChatTextBox.Clear();
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = LocalizationService.Format("MultiplayerChatError", ex.Message);
        }
    }

    private void RenderPlayers()
    {
        var localTelemetry = _telemetrySource();
        var localMap = localTelemetry?.MapName;
        var localCompatibilityId = _activeMapSource()?.CompatibilityId;
        var rows = new List<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var player in _players.Values.OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var isLocal = string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase);
            _remoteTelemetry.TryGetValue(player.PlayerId, out var telemetry);

            RoleplayCharacterState? roleplay = null;
            if (isLocal)
            {
                roleplay = _localRoleplayCharacter;
            }
            else if (_remoteRoleplayCharacters.TryGetValue(player.PlayerId, out var rpFrame) &&
                     rpFrame.Character.IsActive &&
                     now - rpFrame.Character.Timestamp <= TimeSpan.FromSeconds(3d))
            {
                roleplay = rpFrame.Character;
            }

            var roleplayActive = roleplay?.IsActive == true;
            var mapNameValue = roleplayActive
                ? roleplay!.MapName
                : player.MapName;
            var mapName = string.IsNullOrWhiteSpace(mapNameValue)
                ? LocalizationService.Get("NotAvailable")
                : mapNameValue;

            var activity = roleplayActive
                ? PlayerModeText(roleplay!.Activity)
                : MultiplayerTabText("No ônibus", "In bus", "En autobús", "Im Bus", "Dans le bus");

            var speed = roleplayActive
                ? string.Format(
                    LocalizationService.CurrentCulture,
                    "{0,4:F1} m/s",
                    roleplay!.SpeedMps)
                : telemetry is null
                    ? "--.- km/h"
                    : string.Format(
                        LocalizationService.CurrentCulture,
                        "{0,5:F1} km/h",
                        telemetry.SpeedKph);

            var distance = roleplayActive
                ? GetRoleplayDistanceText(
                    localTelemetry,
                    _localRoleplayCharacter,
                    roleplay!,
                    localCompatibilityId)
                : GetDistanceText(
                    localTelemetry,
                    telemetry,
                    localMap,
                    player.MapName,
                    localCompatibilityId,
                    player.MapCompatibilityId);

            var localMarker = isLocal ? LocalizationService.Get("MultiplayerYouMarker") : "  ";
            rows.Add($"{localMarker} {player.DisplayName,-18} | {activity,-12} | {mapName,-18} | {speed} | {distance}");
        }

        if (rows.Count == 0)
        {
            rows.Add(LocalizationService.Get("MultiplayerNoPlayers"));
        }

        PlayersListBox.ItemsSource = rows;
        PlayersOverviewListBox.ItemsSource = rows.Take(6).ToArray();
        PlayerCountText.Text = LocalizationService.Format("MultiplayerPlayerCount", _players.Count);
    }

    private static string PlayerModeText(RoleplayCharacterActivity activity) =>
        activity switch
        {
            RoleplayCharacterActivity.Running => MultiplayerTabText(
                "RP • correndo",
                "RP • running",
                "RP • corriendo",
                "RP • läuft",
                "RP • course"),
            RoleplayCharacterActivity.Walking => MultiplayerTabText(
                "RP • a pé",
                "RP • on foot",
                "RP • a pie",
                "RP • zu Fuß",
                "RP • à pied"),
            _ => MultiplayerTabText(
                "RP • parado",
                "RP • idle",
                "RP • quieto",
                "RP • steht",
                "RP • immobile")
        };

    private string GetRoleplayDistanceText(
        VehicleTelemetry? localVehicle,
        RoleplayCharacterState? localRoleplay,
        RoleplayCharacterState remoteRoleplay,
        string? localCompatibilityId)
    {
        var localMapId = localRoleplay?.MapCompatibilityId ?? localCompatibilityId;
        if (!string.IsNullOrWhiteSpace(localMapId) &&
            !string.IsNullOrWhiteSpace(remoteRoleplay.MapCompatibilityId) &&
            !string.Equals(
                localMapId,
                remoteRoleplay.MapCompatibilityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationService.Get("MultiplayerDifferentMapBuild");
        }

        double? localX = localRoleplay?.IsActive == true
            ? localRoleplay.LocalX
            : localVehicle?.LocalX;
        double? localY = localRoleplay?.IsActive == true
            ? localRoleplay.LocalY
            : localVehicle?.LocalY;

        if (localX is not double x ||
            localY is not double y ||
            !double.IsFinite(x) ||
            !double.IsFinite(y))
        {
            return LocalizationService.Get("NotAvailable");
        }

        var dx = remoteRoleplay.LocalX - x;
        var dy = remoteRoleplay.LocalY - y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return LocalizationService.Format("MultiplayerDistance", distance);
    }

    private void ApplyTabLocalization()
    {
        OverviewTab.Header = MultiplayerTabText(
            "Visão geral",
            "Overview",
            "Resumen",
            "Übersicht",
            "Vue d’ensemble");
        RoomTab.Header = MultiplayerTabText(
            "Sala",
            "Room",
            "Sala",
            "Raum",
            "Salon");
        PlayersTab.Header = MultiplayerTabText(
            "Jogadores",
            "Players",
            "Jugadores",
            "Spieler",
            "Joueurs");
        ChatVoiceTab.Header = MultiplayerTabText(
            "Chat & Voz",
            "Chat & Voice",
            "Chat y voz",
            "Chat & Sprache",
            "Chat et voix");
        RoleplayTab.Header = MultiplayerTabText(
            "Personagem / RP",
            "Character / RP",
            "Personaje / RP",
            "Charakter / RP",
            "Personnage / RP");
        AdvancedTab.Header = MultiplayerTabText(
            "Avançado",
            "Advanced",
            "Avanzado",
            "Erweitert",
            "Avancé");
    }

    private static string MultiplayerTabText(
        string pt,
        string en,
        string es,
        string de,
        string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private string GetDistanceText(
        VehicleTelemetry? local,
        VehicleTelemetry? remote,
        string? localMap,
        string? remoteMap,
        string? localCompatibilityId,
        string? remoteCompatibilityId)
    {
        if (!string.IsNullOrWhiteSpace(localCompatibilityId) &&
            !string.IsNullOrWhiteSpace(remoteCompatibilityId) &&
            !string.Equals(localCompatibilityId, remoteCompatibilityId, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationService.Get("MultiplayerDifferentMapBuild");
        }

        if (local is null || remote is null ||
            string.IsNullOrWhiteSpace(localMap) ||
            !string.Equals(localMap, remoteMap, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationService.Get("MultiplayerDifferentMap");
        }

        var dx = remote.X - local.X;
        var dy = remote.Y - local.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return LocalizationService.Format("MultiplayerDistance", distance);
    }

    private void UpdateLocalMapText()
    {
        var mapName = _telemetrySource()?.MapName;
        var compatibilityId = _activeMapSource()?.CompatibilityId;
        var value = string.IsNullOrWhiteSpace(mapName)
            ? LocalizationService.Get("NotAvailable")
            : mapName;

        LocalMapValueText.Text = string.IsNullOrWhiteSpace(compatibilityId)
            ? value
            : $"{value} • {compatibilityId[..Math.Min(8, compatibilityId.Length)]}";
    }

    private void RenderConnectionState(HubConnectionState state)
    {
        switch (state)
        {
            case HubConnectionState.Connected:
                StatusDot.Fill = Brushes.LimeGreen;
                StatusText.Text = LocalizationService.Get("MultiplayerConnected");
                StatusDetailText.Text = LocalizationService.Format(
                    "MultiplayerConnectedDetail",
                    _settings.RoomId);
                SetInputsEnabled(false);
                break;

            case HubConnectionState.Connecting:
                StatusDot.Fill = Brushes.Goldenrod;
                StatusText.Text = LocalizationService.Get("MultiplayerConnecting");
                break;

            case HubConnectionState.Reconnecting:
                StatusDot.Fill = Brushes.Orange;
                StatusText.Text = LocalizationService.Get("MultiplayerReconnecting");
                StatusDetailText.Text = LocalizationService.Get("MultiplayerReconnectingDetail");
                break;

            default:
                StatusDot.Fill = Brushes.Gray;
                StatusText.Text = LocalizationService.Get("MultiplayerDisconnected");
                if (string.IsNullOrWhiteSpace(StatusDetailText.Text))
                {
                    StatusDetailText.Text = LocalizationService.Get("MultiplayerDisconnectedDetail");
                }
                break;
        }

        UpdateButtons();
    }

    private void SetInputsEnabled(bool enabled)
    {
        ServerTextBox.IsEnabled = enabled && !_host.IsRunning;
        RoomTextBox.IsEnabled = enabled;
        NicknameTextBox.IsEnabled = enabled;
    }

    private void UpdateButtons()
    {
        ConnectButton.Content = _client.State == HubConnectionState.Disconnected
            ? LocalizationService.Get("MultiplayerJoinRoom")
            : LocalizationService.Get("MultiplayerDisconnect");

        CreateRoomButton.Content = _host.IsRunning
            ? LocalizationService.Get("MultiplayerStopHosting")
            : LocalizationService.Get("MultiplayerCreateRoom");
    }

    private void RenderInviteAddresses()
    {
        var addresses = _host.GetLanJoinUrls();
        InviteAddressText.Text = addresses.Count == 0
            ? LocalizationService.Format("MultiplayerHostingPort", DefaultHostPort)
            : LocalizationService.Format(
                "MultiplayerInviteAddress",
                string.Join("  |  ", addresses),
                _settings.RoomId);
    }
}
