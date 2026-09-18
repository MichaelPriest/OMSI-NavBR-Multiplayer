namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    /// <summary>
    /// True when the user opted into the Alpha.12 physical remote-bus test and
    /// the connected OMSI plugin reports the spawn/transform capabilities.
    /// </summary>
    public bool IsPhysicalMultiplayerAvailable => _physicalVehicles.IsPhysicalMultiplayerAvailable;

    public bool IsRemotePhysicalVehicleSpawned(string playerId) =>
        _physicalVehicles.IsSpawned(playerId);

    internal RemotePhysicalVehicleStatus GetRemotePhysicalVehicleStatus(
        string playerId) =>
        _physicalVehicles.GetStatus(playerId);

    /// <summary>
    /// Removes every NavBR-owned physical remote bus from OMSI without ending
    /// the multiplayer session. Despawn stays allowed after the opt-in is
    /// disabled so the test can always be shut down safely.
    /// </summary>
    public Task ClearPhysicalVehiclesAsync(CancellationToken cancellationToken = default) =>
        _physicalVehicles.ClearAsync(cancellationToken);
}
