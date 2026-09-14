using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow : Window
{
    private readonly Func<VehicleTelemetry?> _telemetrySource;
    private readonly MultiplayerClientService _client = new();
    private readonly DispatcherTimer _publishTimer;
    private readonly Dictionary<string, PlayerPresence> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VehicleTelemetry> _remoteTelemetry = new(StringComparer.OrdinalIgnoreCase);

    private MultiplayerSettings _settings;
    private bool _publishing;

    public event Action<PlayerTelemetryFrame>? RemoteTelemetryReceived;
    public event Action<string>? RemotePlayerLeft;
    public event Action? RemotePlayersReset;

    public MultiplayerWindow(Func<VehicleTelemetry?> telemetrySource)
    {
        _telemetrySource = telemetrySource;
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

        Loaded += (_, _) =>
        {
            LoadSettingsIntoUi();
            ApplyLocalization();
            RenderConnectionState(HubConnectionState.Disconnected);
            RenderPlayers();
        };

        Closed += async (_, _) =>
        {
            _publishTimer.Stop();
            await _client.DisposeAsync();
            RemotePlayersReset?.Invoke();
        };
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
        DescriptionText.Text = LocalizationService.Get("MultiplayerDescription");
        ServerLabelText.Text = LocalizationService.Get("MultiplayerServer");
        RoomLabelText.Text = LocalizationService.Get("MultiplayerRoom");
        NicknameLabelText.Text = LocalizationService.Get("MultiplayerNickname");
        LocalMapLabelText.Text = LocalizationService.Get("MultiplayerLocalMap");
        PlayersHeadingText.Text = LocalizationService.Get("MultiplayerPlayers");
        FooterText.Text = LocalizationService.Get("MultiplayerFooter");
        UpdateLocalMapText();
        UpdateConnectButton();
        RenderPlayers();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client.State != HubConnectionState.Disconnected)
        {
            await DisconnectAsync();
            return;
        }

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

        SetInputsEnabled(false);
        StatusDetailText.Text = LocalizationService.Get("MultiplayerConnectingDetail");

        try
        {
            var localTelemetry = _telemetrySource();
            var snapshot = await _client.ConnectAsync(_settings, localTelemetry?.MapName);
            ApplySnapshot(snapshot);
            _publishTimer.Start();
            await PublishLocalTelemetryAsync();
        }
        catch (Exception ex)
        {
            _publishTimer.Stop();
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
        await _client.DisconnectAsync();
        _players.Clear();
        _remoteTelemetry.Clear();
        SetInputsEnabled(true);
        RemotePlayersReset?.Invoke();
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

    private void ApplySnapshot(RoomSnapshot snapshot)
    {
        _players.Clear();
        foreach (var player in snapshot.Players)
        {
            _players[player.PlayerId] = player;
        }

        RenderPlayers();
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
    }

    private void RemovePlayer(string playerId)
    {
        _players.Remove(playerId);
        _remoteTelemetry.Remove(playerId);
        RemotePlayerLeft?.Invoke(playerId);
        RenderPlayers();
    }

    private void RenderPlayers()
    {
        var localTelemetry = _telemetrySource();
        var localMap = localTelemetry?.MapName;
        var rows = new List<string>();

        foreach (var player in _players.Values.OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var isLocal = string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase);
            _remoteTelemetry.TryGetValue(player.PlayerId, out var telemetry);

            var mapName = string.IsNullOrWhiteSpace(player.MapName)
                ? LocalizationService.Get("NotAvailable")
                : player.MapName;

            var speed = telemetry is null
                ? "--.- km/h"
                : string.Format(LocalizationService.CurrentCulture, "{0,5:F1} km/h", telemetry.SpeedKph);

            var distance = GetDistanceText(localTelemetry, telemetry, localMap, player.MapName);
            var localMarker = isLocal ? LocalizationService.Get("MultiplayerYouMarker") : "  ";
            rows.Add($"{localMarker} {player.DisplayName,-20} | {mapName,-24} | {speed} | {distance}");
        }

        if (rows.Count == 0)
        {
            rows.Add(LocalizationService.Get("MultiplayerNoPlayers"));
        }

        PlayersListBox.ItemsSource = rows;
        PlayerCountText.Text = LocalizationService.Format("MultiplayerPlayerCount", _players.Count);
    }

    private string GetDistanceText(
        VehicleTelemetry? local,
        VehicleTelemetry? remote,
        string? localMap,
        string? remoteMap)
    {
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
        var map = _telemetrySource()?.MapName;
        LocalMapValueText.Text = string.IsNullOrWhiteSpace(map)
            ? LocalizationService.Get("NotAvailable")
            : map;
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

        UpdateConnectButton();
    }

    private void SetInputsEnabled(bool enabled)
    {
        ServerTextBox.IsEnabled = enabled;
        RoomTextBox.IsEnabled = enabled;
        NicknameTextBox.IsEnabled = enabled;
    }

    private void UpdateConnectButton()
    {
        ConnectButton.Content = _client.State == HubConnectionState.Disconnected
            ? LocalizationService.Get("MultiplayerConnect")
            : LocalizationService.Get("MultiplayerDisconnect");
    }
}
