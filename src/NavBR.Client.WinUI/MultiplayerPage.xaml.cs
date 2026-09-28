using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class MultiplayerPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<NativePlayerRow> _players = new();
    private bool _applyingState;

    public MultiplayerPage()
    {
        InitializeComponent();
        PlayersList.ItemsSource = _players;
    }

    public void ApplyState(JsonElement state)
    {
        var multiplayer = JsonState.Property(state, "multiplayer");
        var connected = JsonState.Bool(multiplayer, "connected");

        ConnectionText.Text = connected
            ? JsonState.String(multiplayer, "connectionState") ?? "ONLINE"
            : "OFFLINE";
        PlayerCountText.Text = (JsonState.Int(multiplayer, "playerCount") ?? 0).ToString();
        SubtitleText.Text = connected
            ? $"Sala {JsonState.String(multiplayer, "roomId") ?? "NavBR"} · {JsonState.String(multiplayer, "transportMode") ?? "online"}"
            : "Configure o servidor e entre em uma sala.";

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
                ServerTextBox.Text = JsonState.String(multiplayer, "serverUrl") ?? string.Empty;
            }
            if (RoomTextBox.FocusState == FocusState.Unfocused)
            {
                RoomTextBox.Text = JsonState.String(multiplayer, "roomId") ?? string.Empty;
            }
            if (DisplayNameTextBox.FocusState == FocusState.Unfocused)
            {
                DisplayNameTextBox.Text = JsonState.String(multiplayer, "displayName") ?? string.Empty;
            }
            PhysicalVehiclesToggle.IsOn =
                JsonState.Bool(multiplayer, "physicalVehiclesEnabled");
        }
        finally
        {
            _applyingState = false;
        }

        ConnectButton.IsEnabled = !connected;
        DisconnectButton.IsEnabled = connected;

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

    private async void Connect_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            "connectRoom",
            new
            {
                serverUrl = ServerTextBox.Text.Trim(),
                roomId = RoomTextBox.Text.Trim(),
                displayName = DisplayNameTextBox.Text.Trim(),
                roomPassword = PasswordTextBox.Password
            });

    private async void Disconnect_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("disconnectRoom");

    private async void PhysicalVehicles_Toggled(object sender, RoutedEventArgs e)
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
