using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class HardwarePage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    public HardwarePage()
    {
        InitializeComponent();
        BaudComboBox.ItemsSource = new[]
        {
            "9600", "19200", "38400", "57600", "115200", "230400", "460800"
        };
        BaudComboBox.SelectedItem = "115200";
    }

    public void ApplyState(JsonElement state)
    {
        var hardware = JsonState.Property(state, "hardware");
        var connected = JsonState.Bool(hardware, "connected");

        StateText.Text = connected ? "CONECTADO" : "OFFLINE";
        ProtocolText.Text = JsonState.String(hardware, "protocol") ?? "—";
        LastFrameText.Text = JsonState.String(hardware, "lastFrameSentAtUtc") ?? "—";
        PayloadText.Text = JsonState.String(hardware, "payloadPreview") ?? "—";

        var ports = JsonState.Array(hardware, "availablePorts")
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();
        PortComboBox.ItemsSource = ports;

        var portName = JsonState.String(hardware, "portName");
        if (!string.IsNullOrWhiteSpace(portName))
        {
            PortComboBox.SelectedItem = ports.FirstOrDefault(p =>
                string.Equals(p, portName, StringComparison.OrdinalIgnoreCase));
        }

        var baudRate = JsonState.Int(hardware, "baudRate");
        if (baudRate is int baud)
        {
            BaudComboBox.SelectedItem = baud.ToString();
        }
        AutoReconnectCheckBox.IsChecked =
            JsonState.Bool(hardware, "autoReconnect");

        var telemetry = JsonState.Property(hardware, "telemetry");
        TelemetryText.Text = JsonState.IsObject(telemetry)
            ? $"Linha {JsonState.String(telemetry, "line") ?? "—"} / {JsonState.String(telemetry, "route") ?? "—"} · " +
              $"{JsonState.String(telemetry, "destination") ?? "sem destino"}\n" +
              $"Velocidade {JsonState.Double(telemetry, "speedKph") ?? 0d:0.0} km/h · " +
              $"Acelerador {JsonState.Double(telemetry, "throttlePercent") ?? 0d:0}% · " +
              $"Freio {JsonState.Double(telemetry, "brakePercent") ?? 0d:0}%\n" +
              $"Portas {JsonState.String(telemetry, "doors") ?? "—"} · Luzes {JsonState.String(telemetry, "lights") ?? "—"}"
            : "Aguardando OMSI.";

        ConnectButton.IsEnabled = !connected;
        DisconnectButton.IsEnabled = connected;

        var error = JsonState.String(hardware, "lastError");
        if (!string.IsNullOrWhiteSpace(error))
        {
            NoticeBar.Message = error;
            NoticeBar.Severity = InfoBarSeverity.Warning;
            NoticeBar.IsOpen = true;
        }
    }

    private object BuildPayload()
    {
        _ = int.TryParse(
            BaudComboBox.SelectedItem?.ToString(),
            out var baudRate);
        return new
        {
            portName = PortComboBox.SelectedItem?.ToString() ?? string.Empty,
            baudRate = baudRate > 0 ? baudRate : 115200,
            autoReconnect = AutoReconnectCheckBox.IsChecked == true
        };
    }

    private async Task RunAsync(string command)
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }
            await CommandHandler(command, BuildPayload());
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("connectHardware");

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (CommandHandler is not null)
        {
            await CommandHandler("disconnectHardware", null);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("saveHardwareSelection");
}
