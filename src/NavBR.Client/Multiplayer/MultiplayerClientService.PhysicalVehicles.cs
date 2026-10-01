using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    /// <summary>
    /// True when the user opted into the Alpha.12 physical remote-bus test and
    /// the connected OMSI plugin reports the spawn/transform capabilities.
    /// </summary>
    public bool IsPhysicalMultiplayerAvailable =>
        _physicalVehicles.IsPhysicalMultiplayerAvailable ||
        (ExperimentalFeatureFlags.PhysicalVehiclesEnabled &&
         OpenOmsiLanGateway.Shared.IsClientConnected);

    public bool IsRemotePhysicalVehicleSpawned(string playerId) =>
        _physicalVehicles.IsSpawned(playerId) ||
        OpenOmsiLanGateway.Shared.HasRemote(playerId);

    internal RemotePhysicalVehicleStatus GetRemotePhysicalVehicleStatus(
        string playerId) =>
        _physicalVehicles.GetStatus(playerId);

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
