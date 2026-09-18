using NavBR.Client.Hardware;

namespace NavBR.Client;

public partial class MainWindow
{
    private object BuildWebHardwareState()
    {
        var telemetry = GetCurrentTelemetryForAlpha11();
        var serial = HardwareCockpitBridgeController.Shared.Snapshot(telemetry);

        return new
        {
            protocol = serial.Protocol,
            connected = serial.Connected,
            portName = serial.PortName,
            baudRate = serial.BaudRate,
            autoReconnect = serial.AutoReconnect,
            availablePorts = serial.AvailablePorts,
            lastError = serial.LastError,
            lastFrameSentAtUtc = serial.LastFrameSentAtUtc,
            payloadPreview = serial.PayloadPreview,
            telemetry = telemetry is null
                ? null
                : new
                {
                    line = telemetry.Line,
                    route = telemetry.Route,
                    destination = telemetry.DestinationName,
                    currentStreet = telemetry.CurrentStreetName,
                    nextStop = telemetry.NextStopName,
                    currentStopIndex = telemetry.CurrentStopIndex,
                    stopRequested = telemetry.StopRequested,
                    speedKph = telemetry.SpeedKph,
                    delaySeconds = telemetry.DelaySeconds,
                    throttlePercent = telemetry.ThrottlePercent,
                    brakePercent = telemetry.BrakePercent,
                    doors = telemetry.Doors.ToString(),
                    lights = telemetry.Lights.ToString(),
                    turnSignal = telemetry.TurnSignal.ToString(),
                    hornActive = telemetry.HornActive,
                    wipersActive = telemetry.WipersActive,
                    parkingBrakeActive = telemetry.ParkingBrakeActive,
                    reverseGear = telemetry.ReverseGear
                }
        };
    }

    private static void ConnectHardwareFromWeb(
        string? portName,
        int baudRate,
        bool autoReconnect)
    {
        HardwareCockpitBridgeController.Shared.Connect(
            portName ?? string.Empty,
            baudRate,
            autoReconnect);
    }

    private static void DisconnectHardwareFromWeb() =>
        HardwareCockpitBridgeController.Shared.Disconnect(disableAutoReconnect: true);

    private static void SaveHardwareSelectionFromWeb(
        string? portName,
        int baudRate,
        bool autoReconnect)
    {
        HardwareCockpitBridgeController.Shared.SaveSelection(
            portName,
            baudRate,
            autoReconnect);
    }
}
