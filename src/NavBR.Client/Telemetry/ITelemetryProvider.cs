using NavBR.Client.Omsi;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Telemetry;

public interface ITelemetryProvider : IDisposable
{
    bool IsAttached { get; }
    int? AttachedProcessId { get; }
    TelemetryErrorCode LastErrorCode { get; }

    Task<bool> AttachAsync(
        OmsiProcessInfo processInfo,
        CancellationToken cancellationToken = default);

    VehicleTelemetry? Read(string playerId);
}

public enum TelemetryErrorCode
{
    None = 0,
    UnsupportedVersion,
    AttachFailed,
    NoVehicle,
    ReadFailed,
    ProcessExited
}
