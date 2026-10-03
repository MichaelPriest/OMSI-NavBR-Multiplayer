using NavBR.Client.OpenOmsi;
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
                OpenOmsiEnvironmentLocator.GetRunningProcessId());
        return runtime?.IsDrawn(lanId) == true;
    }

    internal RemotePhysicalVehicleStatus GetRemotePhysicalVehicleStatus(
        string playerId)
    {
        if (OpenOmsiLanGateway.Shared.TryGetRemoteLanId(
                playerId,
                out var lanId))
        {
            var gatewayRemote =
                OpenOmsiLanGateway.Shared
                    .GetStatus()
                    .Remotes
                    .FirstOrDefault(remote =>
                        string.Equals(
                            remote.PlayerId,
                            playerId,
                            StringComparison.OrdinalIgnoreCase));

            if (gatewayRemote is null ||
                !gatewayRemote.HasInfo ||
                !gatewayRemote.HasState ||
                string.IsNullOrWhiteSpace(
                    gatewayRemote.VehiclePath))
            {
                return new RemotePhysicalVehicleStatus(
                    "openomsi-identity-pending",
                    ErrorCode: "openomsi-vehicle-path-pending",
                    ErrorMessage:
                        "O jogador está online, mas o NavBR ainda não recebeu um caminho .bus/.ovh utilizável. INFO/STATE não são enviados ao openOMSI até a identidade resolver.",
                    PartCount: null,
                    ExpectedPartCount: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }

            var installedVehicle =
                OpenOmsiEnvironmentLocator.ResolveInstalledVehicleFile(
                        gatewayRemote.VehiclePath);
            if (installedVehicle is null)
            {
                return new RemotePhysicalVehicleStatus(
                    "openomsi-asset-missing",
                    ErrorCode: "openomsi-local-bus-missing",
                    ErrorMessage:
                        $"O ônibus remoto {gatewayRemote.VehiclePath} não foi encontrado nos content roots locais do openOMSI/OMSI 2. Instale o mesmo addon para permitir drawn=true.",
                    PartCount: null,
                    ExpectedPartCount: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }

            var runtime =
                OpenOmsiLanRuntimeStatusReader.Read(
                    OpenOmsiEnvironmentLocator.GetRunningProcessId());

            if (runtime?.Fresh != true)
            {
                return new RemotePhysicalVehicleStatus(
                    "openomsi-sent-unconfirmed",
                    ErrorCode: "openomsi-status-unavailable",
                    ErrorMessage:
                        "INFO/STATE foram enviados, mas o status LAN oficial do openOMSI ainda não está disponível ou está desatualizado.",
                    PartCount: null,
                    ExpectedPartCount: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }

            var runtimePeer =
                runtime.Players.FirstOrDefault(peer =>
                    peer.Id == lanId);
            if (runtimePeer is null)
            {
                return new RemotePhysicalVehicleStatus(
                    "openomsi-peer-missing",
                    ErrorCode: "openomsi-peer-missing",
                    ErrorMessage:
                        $"NavBR enviou INFO/STATE para LAN id {lanId}, mas o openOMSI ainda não listou esse peer no status oficial.",
                    PartCount: null,
                    ExpectedPartCount: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }

            if (!VehiclePathsEquivalent(
                    gatewayRemote?.VehiclePath,
                    runtimePeer.Bus))
            {
                return new RemotePhysicalVehicleStatus(
                    "openomsi-bus-path-mismatch",
                    ErrorCode: "openomsi-bus-path-mismatch",
                    ErrorMessage:
                        $"O openOMSI recebeu o peer {lanId}, mas o ônibus reportado não coincide com o enviado pelo NavBR. Enviado: {gatewayRemote?.VehiclePath ?? "—"}; openOMSI: {runtimePeer.Bus ?? "—"}.",
                    PartCount: null,
                    ExpectedPartCount: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }

            if (runtimePeer.Drawn)
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
                "openomsi-peer-not-drawn",
                ErrorCode: "openomsi-not-drawn",
                ErrorMessage:
                    $"O openOMSI listou o peer {lanId} ({runtimePeer.Name ?? playerId}) e reconheceu o ônibus {runtimePeer.Bus ?? gatewayRemote?.VehiclePath ?? "—"}, mas drawn=false." +
                    (string.IsNullOrWhiteSpace(
                        runtimePeer.RelativePosition)
                        ? string.Empty
                        : $" Posição relativa reportada: {runtimePeer.RelativePosition}.") +
                    (runtimePeer.PassengerCount is int passengers
                        ? $" Passageiros reportados: {passengers}."
                        : string.Empty),
                PartCount: null,
                ExpectedPartCount: null,
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }

        return _physicalVehicles.GetStatus(playerId);
    }

    private static bool VehiclePathsEquivalent(
        string? expected,
        string? actual)
    {
        static string? Normalize(string? value)
        {
            var normalized =
                value?.Trim()
                    .Replace('\\', '/')
                    .TrimStart('/');
            return string.IsNullOrWhiteSpace(normalized)
                ? null
                : normalized;
        }

        var left = Normalize(expected);
        var right = Normalize(actual);
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);
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
