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
    private readonly MultiplayerClientService _client;
    private readonly RoomHostService _host = new();
    private readonly VoiceChatService _voiceChat = new();
    private readonly DispatcherTimer _publishTimer;
    private readonly DispatcherTimer _latencyTimer;
    private readonly SemaphoreSlim _voiceSendGate = new(1, 1);
    private readonly Dictionary<string, PlayerPresence> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VehicleTelemetry> _remoteTelemetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoleplayCharacterFrame> _remoteRoleplayCharacters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _voiceActivity = new(StringComparer.OrdinalIgnoreCase);
    private RoleplayCharacterState? _localRoleplayCharacter;
    private readonly List<ChatMessage> _chatMessages = [];

    private MultiplayerSettings _settings;
    private bool _publishing;
    private bool _measuringLatency;
    private bool _controllerInitialized;
    private double? _lastLatencyMs;

    public event Action<PlayerTelemetryFrame>? RemoteTelemetryReceived;
    public event Action<string>? RemotePlayerLeft;
    public event Action? RemotePlayersReset;
    public event Action<ChatMessage>? ChatMessageReceived;
    public event Action<string, string>? RemoteSpeakerActive;
    public event Action<string>? VoiceError;
    public event Action<bool, string?>? MultiplayerConnectionChanged;
    public event Action<string>? LocalDisplayNameChanged;
    public event Action? RoleplayActionRequested;

    public bool IsConnected => _client.IsConnected;
    public string CurrentDisplayName => _settings.DisplayName;
    public string CurrentRoomId => _settings.RoomId;

    public MultiplayerWindow(
        Func<VehicleTelemetry?> telemetrySource,
        Func<OmsiMapInfo?> activeMapSource)
        : this(telemetrySource, activeMapSource, null, null)
    {
    }

    internal MultiplayerWindow(
        Func<VehicleTelemetry?> telemetrySource,
        Func<OmsiMapInfo?> activeMapSource,
        Func<IReadOnlyList<RoleplayCharacterOption>>? roleplayCharacterOptionsSource,
        Func<string?>? omsiInstallDirectorySource = null)
    {
        _telemetrySource = telemetrySource;
        _activeMapSource = activeMapSource;
        _client = new MultiplayerClientService(omsiInstallDirectorySource);
        _roleplayCharacterOptionsSource =
            roleplayCharacterOptionsSource ?? (() => Array.Empty<RoleplayCharacterOption>());
        _settings = MultiplayerSettingsStore.Load();

        InitializeComponent();

        _publishTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _publishTimer.Tick += PublishTimer_Tick;

        _latencyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2d)
        };
        _latencyTimer.Tick += LatencyTimer_Tick;

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
                _voiceActivity[playerId] = DateTimeOffset.UtcNow;
                var displayName = _players.TryGetValue(playerId, out var player)
                    ? player.DisplayName
                    : playerId;
                RenderPlayers();
                RemoteSpeakerActive?.Invoke(playerId, displayName);
            });
        _voiceChat.VoiceError += message => Dispatcher.BeginInvoke(() =>
        {
            VoiceError?.Invoke(message);
            StatusDetailText.Text = LocalizationService.Format("MultiplayerVoiceError", message);
        });

        Loaded += (_, _) => InitializeControllerForWebShell();

        Closed += async (_, _) =>
        {
            _publishTimer.Stop();
            _latencyTimer.Stop();
            _voiceChat.Dispose();
            await _client.DisposeAsync();
            await _host.DisposeAsync();
            _voiceSendGate.Dispose();
            RemotePlayersReset?.Invoke();
        };
    }

    internal void InitializeControllerForWebShell()
    {
        if (_controllerInitialized)
        {
            return;
        }

        _controllerInitialized = true;
        LoadSettingsIntoUi();
        ApplyLocalization();
        RenderConnectionState(HubConnectionState.Disconnected);
        RenderPlayers();
        RenderChat();
        InitializePhysicalVehiclesPublicTest();
        HookDiagnosticsLifecycle();
        InitializePersistentLifetime();
        InitializeRoleplayCharacterSelector();
        InitializeRelayUi();
        InitializePublicRoomBrowser();
        InitializeVoiceChannels();
        RefreshSessionSummary();
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
        RefreshPublicRoomBrowserLocalization();
        RefreshSessionSummary();
    }

    private async void CreateRoomButton_Click(object sender, RoutedEventArgs e)
    {
        if (_host.IsRunning)
        {
            await DisconnectAsync();
            await _host.StopAsync();
            InviteAddressText.Text = string.Empty;
            RoomInviteAddressText.Text = string.Empty;
            SetInputsEnabled(true);
            UpdateButtons();
            RefreshSessionSummary();
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
            RefreshSessionSummary();
            await ConnectToConfiguredServerAsync();
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = LocalizationService.Format("MultiplayerHostError", ex.Message);
            await _host.StopAsync();
            UpdateButtons();
            RefreshSessionSummary();
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
        RefreshSessionSummary();

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
            _latencyTimer.Start();
            if (VoiceEnabledCheckBox.IsChecked == true)
            {
                _voiceChat.Start();
            }

            MultiplayerConnectionChanged?.Invoke(true, _settings.RoomId);
            await PublishLocalTelemetryAsync();
            await RefreshLatencyAsync();
            RefreshSessionSummary();
        }
        catch (Exception ex)
        {
            _publishTimer.Stop();
            _latencyTimer.Stop();
            _voiceChat.Stop();
            _lastLatencyMs = null;
            SetInputsEnabled(true);
            RenderConnectionState(HubConnectionState.Disconnected);
            StatusDetailText.Text = LocalizationService.Format(
                "MultiplayerConnectionError",
                ex.Message);
            RefreshSessionSummary();
        }
    }

    private async Task DisconnectAsync()
    {
        _publishTimer.Stop();
        _latencyTimer.Stop();
        _voiceChat.Stop();
        _lastLatencyMs = null;
        await _client.DisconnectAsync();
        _players.Clear();
        _remoteTelemetry.Clear();
        _remoteRoleplayCharacters.Clear();
        _voiceActivity.Clear();
        SetInputsEnabled(true);
        RemotePlayersReset?.Invoke();
        MultiplayerConnectionChanged?.Invoke(false, null);
        RenderPlayers();
        RenderConnectionState(HubConnectionState.Disconnected);
        RefreshSessionSummary();
    }

    private async void PublishTimer_Tick(object? sender, EventArgs e)
    {
        await PublishLocalTelemetryAsync();
        UpdateLocalMapText();
        RenderPlayers();
    }

    private async void LatencyTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshLatencyAsync();
    }

    private async Task RefreshLatencyAsync()
    {
        if (_measuringLatency || !_client.IsConnected)
        {
            return;
        }

        _measuringLatency = true;
        try
        {
            var latency = await _client.MeasureAndPublishLatencyAsync(
                VoiceEnabledCheckBox.IsChecked == true);
            _lastLatencyMs = latency?.TotalMilliseconds;
            RefreshSessionSummary();
            RenderPlayers();
        }
        finally
        {
            _measuringLatency = false;
        }
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

    private async void VoiceEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var enabled = VoiceEnabledCheckBox.IsChecked == true;
        if (enabled && _client.IsConnected)
        {
            _voiceChat.Start();
        }
        else
        {
            _voiceChat.Stop();
        }

        if (_client.IsConnected)
        {
            await _client.PublishClientStatusAsync(
                enabled,
                _lastLatencyMs is double latency
                    ? (int?)Math.Clamp((int)Math.Round(latency), 0, 5000)
                    : null);
        }

        RenderPlayers();
        RefreshSessionSummary();
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
        RefreshSessionSummary();
    }

    private void UpsertPresence(PlayerPresence presence)
    {
        _players[presence.PlayerId] = presence;
        RenderPlayers();
        RefreshSessionSummary();
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
        RefreshSessionSummary();
    }

    private void RemovePlayer(string playerId)
    {
        _players.Remove(playerId);
        _remoteTelemetry.Remove(playerId);
        _remoteRoleplayCharacters.Remove(playerId);
        _voiceActivity.Remove(playerId);
        _voiceChat.RemoveRemotePlayer(playerId);
        RemotePlayerLeft?.Invoke(playerId);
        RenderPlayers();
        RefreshSessionSummary();
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
        var localPlayerId = _settings.PlayerId;
        ChatListBox.ItemsSource = _chatMessages
            .Select(message => new MultiplayerChatRow(
                message.TimestampUtc.ToLocalTime().ToString("HH:mm"),
                message.DisplayName,
                message.Text,
                string.Equals(message.PlayerId, localPlayerId, StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(113, 198, 255))
                    : new SolidColorBrush(Color.FromRgb(151, 171, 185))))
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
        var rows = new List<MultiplayerPlayerRow>();
        var now = DateTimeOffset.UtcNow;

        foreach (var player in _players.Values.OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var isLocal = string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase);
            VehicleTelemetry? telemetry;
            if (isLocal)
            {
                telemetry = localTelemetry;
            }
            else
            {
                _remoteTelemetry.TryGetValue(player.PlayerId, out telemetry);
            }

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
                : telemetry?.MapName ?? player.MapName;
            var mapName = string.IsNullOrWhiteSpace(mapNameValue)
                ? LocalizationService.Get("NotAvailable")
                : mapNameValue;

            var activity = roleplayActive
                ? PlayerModeText(roleplay!.Activity)
                : MultiplayerTabText("No ônibus", "In bus", "En autobús", "Im Bus", "Dans le bus");

            var speed = roleplayActive
                ? string.Format(
                    LocalizationService.CurrentCulture,
                    "{0:F1} m/s",
                    roleplay!.SpeedMps)
                : telemetry is null
                    ? "—"
                    : string.Format(
                        LocalizationService.CurrentCulture,
                        "{0:F1} km/h",
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
                    mapNameValue,
                    localCompatibilityId,
                    player.MapCompatibilityId);

            var bus = roleplayActive || string.IsNullOrWhiteSpace(telemetry?.VehicleName)
                ? "—"
                : telemetry.VehicleName.Trim();
            var line = roleplayActive || string.IsNullOrWhiteSpace(telemetry?.Line)
                ? "—"
                : telemetry.Line.Trim();
            var ping = player.LatencyMs is int latencyMs
                ? $"{latencyMs} ms"
                : isLocal && _lastLatencyMs is double localLatency
                    ? $"{Math.Round(localLatency):F0} ms"
                    : "—";

            var speaking = _voiceActivity.TryGetValue(player.PlayerId, out var lastVoice) &&
                           now - lastVoice <= TimeSpan.FromSeconds(1.5d);
            var voice = speaking
                ? MultiplayerTabText("Falando", "Speaking", "Hablando", "Spricht", "Parle")
                : player.VoiceEnabled switch
                {
                    true => MultiplayerTabText("Ativo", "On", "Activo", "Aktiv", "Actif"),
                    false => MultiplayerTabText("Desligado", "Off", "Apagado", "Aus", "Désactivé"),
                    _ => "—"
                };

            var physical = roleplayActive
                ? "—"
                : isLocal
                    ? MultiplayerTabText("Local", "Local", "Local", "Lokal", "Local")
                    : !ExperimentalFeatureFlags.PhysicalVehiclesEnabled
                        ? MultiplayerTabText("Desligado", "Off", "Apagado", "Aus", "Désactivé")
                        : _client.IsRemotePhysicalVehicleSpawned(player.PlayerId)
                            ? MultiplayerTabText("Ativo", "Active", "Activo", "Aktiv", "Actif")
                            : MultiplayerTabText("Aguardando", "Waiting", "Esperando", "Wartet", "En attente");

            rows.Add(new MultiplayerPlayerRow(
                player.DisplayName,
                isLocal ? LocalizationService.Get("MultiplayerYouMarker").Trim() : string.Empty,
                activity,
                bus,
                line,
                speed,
                ping,
                voice,
                physical,
                $"{mapName} • {distance}",
                roleplayActive
                    ? new SolidColorBrush(Color.FromRgb(113, 198, 255))
                    : new SolidColorBrush(Color.FromRgb(56, 201, 140))));
        }

        if (rows.Count == 0)
        {
            rows.Add(new MultiplayerPlayerRow(
                LocalizationService.Get("MultiplayerNoPlayers"),
                string.Empty,
                "—",
                "—",
                "—",
                "—",
                "—",
                "—",
                "—",
                MultiplayerTabText(
                    "Aguardando presença real da sala.",
                    "Waiting for real room presence.",
                    "Esperando presencia real de la sala.",
                    "Warte auf echte Raumpräsenz.",
                    "En attente de présence réelle dans le salon."),
                new SolidColorBrush(Color.FromRgb(151, 171, 185))));
        }

        PlayersListBox.ItemsSource = rows;
        PlayersOverviewListBox.ItemsSource = rows.Take(6).ToArray();
        PlayerCountText.Text = LocalizationService.Format("MultiplayerPlayerCount", _players.Count);
        RefreshSessionSummary();
    }

    private static string PlayerModeText(RoleplayCharacterActivity activity) =>
        activity switch
        {
            RoleplayCharacterActivity.Running => MultiplayerTabText(
                "Correndo",
                "Running",
                "Corriendo",
                "Läuft",
                "Course"),
            RoleplayCharacterActivity.Walking => MultiplayerTabText(
                "A pé",
                "On foot",
                "A pie",
                "Zu Fuß",
                "À pied"),
            _ => MultiplayerTabText(
                "Parado",
                "Idle",
                "Quieto",
                "Steht",
                "Immobile")
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
                if (!_latencyTimer.IsEnabled)
                {
                    _latencyTimer.Start();
                }
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
                _latencyTimer.Stop();
                _lastLatencyMs = null;
                if (string.IsNullOrWhiteSpace(StatusDetailText.Text))
                {
                    StatusDetailText.Text = LocalizationService.Get("MultiplayerDisconnectedDetail");
                }
                break;
        }

        UpdateButtons();
        RefreshSessionSummary();
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
        var text = addresses.Count == 0
            ? LocalizationService.Format("MultiplayerHostingPort", DefaultHostPort)
            : LocalizationService.Format(
                "MultiplayerInviteAddress",
                string.Join("  |  ", addresses),
                _settings.RoomId);

        InviteAddressText.Text = text;
        RoomInviteAddressText.Text = text;
        RefreshSessionSummary();
    }

    private void RefreshSessionSummary()
    {
        if (!IsInitialized)
        {
            return;
        }

        SessionRoomValueText.Text = _client.IsConnected
            ? _settings.RoomId
            : string.IsNullOrWhiteSpace(RoomTextBox.Text)
                ? "—"
                : RoomTextBox.Text.Trim();

        LatencyValueText.Text = _client.IsConnected && _lastLatencyMs is double latency
            ? $"{Math.Round(latency):F0} ms"
            : "—";

        HostStateValueText.Text = _host.IsRunning
            ? MultiplayerTabText("Local ativo", "Local active", "Local activo", "Lokal aktiv", "Local actif")
            : _client.IsConnected
                ? MultiplayerTabText("Host remoto", "Remote host", "Host remoto", "Remote-Host", "Hôte distant")
                : MultiplayerTabText("Inativo", "Inactive", "Inactivo", "Inaktiv", "Inactif");

        OverviewConnectionText.Text = _client.IsConnected
            ? MultiplayerTabText("Sincronização ativa", "Sync active", "Sincronización activa", "Synchronisierung aktiv", "Synchronisation active")
            : MultiplayerTabText("Sessão desconectada", "Session disconnected", "Sesión desconectada", "Sitzung getrennt", "Session déconnectée");

        OverviewHostDetailText.Text = _host.IsRunning
            ? MultiplayerTabText(
                $"Host local ativo em TCP {_host.Port}.",
                $"Local host active on TCP {_host.Port}.",
                $"Host local activo en TCP {_host.Port}.",
                $"Lokaler Host aktiv auf TCP {_host.Port}.",
                $"Hôte local actif sur TCP {_host.Port}.")
            : _client.IsConnected
                ? MultiplayerTabText(
                    "Conectado a um host externo; o servidor local permanece desligado.",
                    "Connected to an external host; the local server remains off.",
                    "Conectado a un host externo; el servidor local sigue apagado.",
                    "Mit externem Host verbunden; lokaler Server bleibt aus.",
                    "Connecté à un hôte externe ; le serveur local reste arrêté.")
                : MultiplayerTabText(
                    "Nenhum host local ou remoto ativo.",
                    "No local or remote host active.",
                    "No hay host local ni remoto activo.",
                    "Kein lokaler oder Remote-Host aktiv.",
                    "Aucun hôte local ou distant actif.");

        var upnpEnabled = _settings.EnableAutomaticUpnp;
        RoomUpnpStateText.Text = !upnpEnabled
            ? "UPnP: " + MultiplayerTabText("desligado", "off", "apagado", "aus", "désactivé")
            : !_host.IsRunning
                ? "UPnP: " + MultiplayerTabText("ativado", "enabled", "activado", "aktiviert", "activé")
                : _host.LastUpnpResult switch
                {
                    { Success: true } => "UPnP: " + MultiplayerTabText("mapeado", "mapped", "mapeado", "zugeordnet", "mappé"),
                    { Success: false } => "UPnP: " + MultiplayerTabText("falhou", "failed", "falló", "fehlgeschlagen", "échec"),
                    _ => "UPnP: " + MultiplayerTabText("aguardando", "waiting", "esperando", "wartet", "en attente")
                };

        VoiceSessionStatusText.Text = !_client.IsConnected
            ? MultiplayerTabText(
                "Voz pronta quando a sala conectar",
                "Voice ready when the room connects",
                "Voz lista al conectar la sala",
                "Sprache bereit nach Raumverbindung",
                "Voix prête à la connexion")
            : VoiceEnabledCheckBox.IsChecked == true
                ? MultiplayerTabText("Voz ativa • PTT", "Voice active • PTT", "Voz activa • PTT", "Sprache aktiv • PTT", "Voix active • PTT")
                : MultiplayerTabText("Voz desligada", "Voice off", "Voz apagada", "Sprache aus", "Voix désactivée");

        AdvancedBridgeStatusText.Text =
            Application.Current is App app && app.PluginBridge.IsConnected
                ? MultiplayerTabText(
                    "Plugin NavBR conectado ao OMSI.",
                    "NavBR plugin connected to OMSI.",
                    "Plugin NavBR conectado a OMSI.",
                    "NavBR-Plugin mit OMSI verbunden.",
                    "Plugin NavBR connecté à OMSI.")
                : MultiplayerTabText(
                    "Plugin NavBR desconectado ou OMSI ainda não disponível.",
                    "NavBR plugin disconnected or OMSI not available yet.",
                    "Plugin NavBR desconectado u OMSI aún no disponible.",
                    "NavBR-Plugin getrennt oder OMSI noch nicht verfügbar.",
                    "Plugin NavBR déconnecté ou OMSI pas encore disponible.");

        RefreshRoleplayTechnicalStatus();
    }

    private void OpenRoomTab_Click(object sender, RoutedEventArgs e) =>
        MultiplayerTabs.SelectedItem = RoomTab;

    private void OpenPlayersTab_Click(object sender, RoutedEventArgs e) =>
        MultiplayerTabs.SelectedItem = PlayersTab;

    private void OpenChatTab_Click(object sender, RoutedEventArgs e) =>
        MultiplayerTabs.SelectedItem = ChatVoiceTab;

    private void OpenRoleplayTab_Click(object sender, RoutedEventArgs e) =>
        MultiplayerTabs.SelectedItem = RoleplayTab;
}

internal sealed record MultiplayerPlayerRow(
    string Name,
    string YouLabel,
    string Mode,
    string Bus,
    string Line,
    string Speed,
    string Ping,
    string Voice,
    string Physical,
    string Secondary,
    Brush AccentBrush);

internal sealed record MultiplayerChatRow(
    string Time,
    string DisplayName,
    string Text,
    Brush AccentBrush);
