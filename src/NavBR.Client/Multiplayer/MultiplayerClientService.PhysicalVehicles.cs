using NavBR.Client.PluginInstaller;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    /// <summary>
    /// True when the user opted into the physical remote-bus test and
    /// the connected OMSI plugin reports the spawn/transform capabilities.
    /// </summary>
    public bool IsPhysicalMultiplayerAvailable =>
        _physicalVehicles.IsPhysicalMultiplayerAvailable ||
        (ExperimentalFeatureFlags.PhysicalVehiclesEnabled &&
         OpenOmsiLanGateway.Shared.IsClientConnected);

    public bool IsRemotePhysicalVehicleSpawned(string playerId)
    {
        if (_physicalVehicles.IsSpawned(playerId))
        {
            return true;
        }

        if (!OpenOmsiLanGateway.Shared.TryGetRemoteLanId(
                playerId,
                out var lanId))
        {
            return false;
        }

        var runtime =
            OpenOmsiLanRuntimeStatusReader.Read(
                OpenOmsiPluginInstallationService
                    .GetRunningProcessId());
        return runtime?.IsDrawn(lanId) == true;
    }

    internal RemotePhysicalVehicleStatus GetRemotePhysicalVehicleStatus(
        string playerId)
    {
        if (OpenOmsiLanGateway.Shared.TryGetRemoteLanId(
                playerId,
                out var lanId))
        {
            var runtime =
                OpenOmsiLanRuntimeStatusReader.Read(
                    OpenOmsiPluginInstallationService
                        .GetRunningProcessId());
            if (runtime?.IsDrawn(lanId) == true)
            {
                return new RemotePhysicalVehicleStatus(
                    "active-openomsi-drawn",
                    ErrorCode: null,
                    ErrorMessage: null,
                    PartCount: null,
                    ExpectedPartCount: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }

            return new RemotePhysicalVehicleStatus(
                runtime?.Fresh == true
                    ? "openomsi-sent-not-drawn"
                    : "openomsi-sent-unconfirmed",
                ErrorCode:
                    runtime?.Fresh == true
                        ? "openomsi-not-drawn"
                        : "openomsi-status-unavailable",
                ErrorMessage:
                    runtime?.Fresh == true
                        ? "INFO/STATE foram enviados, mas o openOMSI ainda não confirmou drawn=true para este ônibus."
                        : "INFO/STATE foram enviados, mas o status LAN oficial do openOMSI ainda não está disponível ou está desatualizado.",
                PartCount: null,
                ExpectedPartCount: null,
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }

        return _physicalVehicles.GetStatus(playerId);
    }

    /// <summary>
    /// Removes every NavBR-owned physical remote bus from OMSI without ending
    /// the multiplayer session. Despawn stays allowed after the opt-in is
    /// disabled so the test can always be shut down safely.
    /// </summary>
    public async Task ClearPhysicalVehiclesAsync(
        CancellationToken cancellationToken = default)
    {
        await _physicalVehicles.ClearAsync(cancellationToken);
        await OpenOmsiLanGateway.Shared.ClearRemotesAsync(
            cancellationToken);
    }
}
