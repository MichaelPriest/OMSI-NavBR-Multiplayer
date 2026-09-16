using System.Text.Json;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Hardware;

internal sealed record HardwareCockpitFrame(
    string Protocol,
    DateTimeOffset Timestamp,
    string? Map,
    string? Vehicle,
    string? Line,
    string? Route,
    string? Destination,
    string? CurrentStreet,
    string? NextStop,
    int? CurrentStopIndex,
    bool StopRequested,
    double SpeedKph,
    int? DelaySeconds,
    double? ThrottlePercent,
    double? BrakePercent,
    int Doors,
    int Lights,
    string TurnSignal,
    bool HornActive,
    bool WipersActive,
    bool ParkingBrakeActive,
    bool ReverseGear);

internal static class HardwareCockpitProtocol
{
    public const string Version = "NAVBR_HW_V1";

    private static readonly JsonSerializerOptions PreviewJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static HardwareCockpitFrame CreateFrame(VehicleTelemetry telemetry) => new(
        Version,
        telemetry.Timestamp,
        telemetry.MapName,
        telemetry.VehicleName,
        telemetry.Line,
        telemetry.Route,
        telemetry.DestinationName,
        telemetry.CurrentStreetName,
        telemetry.NextStopName,
        telemetry.CurrentStopIndex,
        telemetry.StopRequested,
        Math.Round(telemetry.SpeedKph, 1),
        telemetry.DelaySeconds,
        RoundNullable(telemetry.ThrottlePercent),
        RoundNullable(telemetry.BrakePercent),
        (int)telemetry.Doors,
        (int)telemetry.Lights,
        telemetry.TurnSignal.ToString(),
        telemetry.HornActive,
        telemetry.WipersActive,
        telemetry.ParkingBrakeActive,
        telemetry.ReverseGear);

    public static string Serialize(VehicleTelemetry telemetry) =>
        JsonSerializer.Serialize(CreateFrame(telemetry), PreviewJsonOptions);

    public static string SerializeCompact(VehicleTelemetry telemetry) =>
        JsonSerializer.Serialize(CreateFrame(telemetry), WireJsonOptions);

    private static double? RoundNullable(double? value) =>
        value.HasValue ? Math.Round(value.Value, 1) : null;
}
