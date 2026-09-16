using System.Text.Json;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Hardware;

internal sealed record HardwareCockpitFrame(
    string Protocol,
    DateTimeOffset Timestamp,
    string? Map,
    string? Line,
    string? Route,
    string? Destination,
    string? CurrentStreet,
    string? NextStop,
    bool StopRequested,
    double SpeedKph,
    int? DelaySeconds,
    int Doors,
    string TurnSignal);

internal static class HardwareCockpitProtocol
{
    public const string Version = "NAVBR_HW_V1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static HardwareCockpitFrame CreateFrame(VehicleTelemetry telemetry) => new(
        Version,
        telemetry.Timestamp,
        telemetry.MapName,
        telemetry.Line,
        telemetry.Route,
        telemetry.DestinationName,
        telemetry.CurrentStreetName,
        telemetry.NextStopName,
        telemetry.StopRequested,
        Math.Round(telemetry.SpeedKph, 1),
        telemetry.DelaySeconds,
        (int)telemetry.Doors,
        telemetry.TurnSignal.ToString());

    public static string Serialize(VehicleTelemetry telemetry) =>
        JsonSerializer.Serialize(CreateFrame(telemetry), JsonOptions);
}
