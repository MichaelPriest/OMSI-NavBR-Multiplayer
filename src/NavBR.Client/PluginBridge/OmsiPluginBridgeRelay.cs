using System.Windows;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.PluginBridge;

public static class OmsiPluginBridgeRelay
{
    public static string? ResolveCurrentMapCompatibilityId(string? fallback = null)
    {
        if (Application.Current?.MainWindow is MainWindow mainWindow)
        {
            var current = mainWindow.GetCurrentMapCompatibilityIdForPlugin();
            if (!string.IsNullOrWhiteSpace(current))
            {
                return current;
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    public static Task ForwardLocalTelemetryAsync(
        VehicleTelemetry telemetry,
        string? mapCompatibilityId = null,
        CancellationToken cancellationToken = default)
    {
        var currentCompatibilityId = ResolveCurrentMapCompatibilityId(
            telemetry.MapCompatibilityId ?? mapCompatibilityId);

        return SendBestEffortAsync(
            CreateStateMessage(
                PluginBridgeProtocol.LocalVehicleState,
                telemetry,
                telemetry.PlayerId,
                displayName: null,
                currentCompatibilityId),
            cancellationToken);
    }

    public static Task ForwardRemoteTelemetryAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default)
    {
        var telemetry = frame.Telemetry;
        var message = CreateStateMessage(
            PluginBridgeProtocol.RemoteVehicleState,
            telemetry,
            frame.Player.PlayerId,
            frame.Player.DisplayName,
            telemetry.MapCompatibilityId ?? frame.Player.MapCompatibilityId);

        return SendBestEffortAsync(message, cancellationToken);
    }

    public static Task RemoveRemotePlayerAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Task.CompletedTask;
        }

        var message = new PluginBridgeMessage(
            PluginBridgeProtocol.RemoteVehicleRemoved,
            PluginBridgeProtocol.Version,
            PlayerId: playerId);

        return SendBestEffortAsync(message, cancellationToken);
    }

    public static Task ClearRemotePlayersAsync(CancellationToken cancellationToken = default)
    {
        var message = new PluginBridgeMessage(
            PluginBridgeProtocol.ClearRemoteVehicles,
            PluginBridgeProtocol.Version);

        return SendBestEffortAsync(message, cancellationToken);
    }

    public static Task<PluginBridgeMessage?> SpawnGhostVehicleAsync(
        string ghostId,
        VehicleTelemetry initialState,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            CreateCommandMessage(
                PluginBridgeProtocol.SpawnGhostVehicle,
                ghostId,
                initialState),
            cancellationToken);

    public static Task<PluginBridgeMessage?> UpdateGhostVehicleAsync(
        string ghostId,
        VehicleTelemetry state,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            CreateCommandMessage(
                PluginBridgeProtocol.UpdateGhostVehicle,
                ghostId,
                state),
            cancellationToken);

    public static Task<PluginBridgeMessage?> DespawnGhostVehicleAsync(
        string ghostId,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.DespawnGhostVehicle,
                PluginBridgeProtocol.Version,
                VehicleInstanceId: ghostId),
            cancellationToken);

    public static Task<PluginBridgeMessage?> SpawnRemoteVehicleAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            CreateCommandMessage(
                PluginBridgeProtocol.SpawnRemoteVehicle,
                frame.Player.PlayerId,
                frame.Telemetry,
                frame.Player.DisplayName),
            cancellationToken);

    public static Task<PluginBridgeMessage?> UpdateRemoteVehicleAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            CreateCommandMessage(
                PluginBridgeProtocol.UpdateRemoteVehicle,
                frame.Player.PlayerId,
                frame.Telemetry,
                frame.Player.DisplayName),
            cancellationToken);

    public static Task<PluginBridgeMessage?> DespawnRemoteVehicleAsync(
        string playerId,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.DespawnRemoteVehicle,
                PluginBridgeProtocol.Version,
                PlayerId: playerId,
                VehicleInstanceId: playerId),
            cancellationToken);

    private static PluginBridgeMessage CreateCommandMessage(
        string type,
        string vehicleInstanceId,
        VehicleTelemetry telemetry,
        string? displayName = null)
    {
        var state = CreateStateMessage(
            type,
            telemetry,
            telemetry.PlayerId,
            displayName,
            ResolveCurrentMapCompatibilityId(telemetry.MapCompatibilityId));

        return state with { VehicleInstanceId = vehicleInstanceId };
    }

    private static PluginBridgeMessage CreateStateMessage(
        string type,
        VehicleTelemetry telemetry,
        string? playerId,
        string? displayName,
        string? mapCompatibilityId)
    {
        return new PluginBridgeMessage(
            type,
            PluginBridgeProtocol.Version,
            PlayerId: playerId,
            DisplayName: displayName,
            MapName: telemetry.MapName,
            MapCompatibilityId: mapCompatibilityId,
            TimestampUnixMilliseconds: telemetry.Timestamp.ToUnixTimeMilliseconds(),
            X: telemetry.X,
            Y: telemetry.Y,
            Z: telemetry.Z,
            GridX: telemetry.GridX,
            GridY: telemetry.GridY,
            TileX: telemetry.TileX,
            TileY: telemetry.TileY,
            HeadingDegrees: telemetry.HeadingDegrees,
            SpeedKph: telemetry.SpeedKph,
            IsInGame: telemetry.IsInGame,
            VehiclePath: telemetry.VehiclePath,
            VehicleName: telemetry.VehicleName,
            VehicleCompatibilityId: telemetry.VehicleCompatibilityId,
            HofName: telemetry.HofName,
            HofCompatibilityId: telemetry.HofCompatibilityId,
            Line: telemetry.Line,
            Route: telemetry.Route,
            NextStopName: telemetry.NextStopName,
            DestinationName: telemetry.DestinationName,
            AccelerationMps2: telemetry.AccelerationMps2,
            FuelPercent: telemetry.FuelPercent,
            ThrottlePercent: telemetry.ThrottlePercent,
            BrakePercent: telemetry.BrakePercent,
            SteeringDegrees: telemetry.SteeringDegrees,
            DelaySeconds: telemetry.DelaySeconds,
            CurrentStopIndex: telemetry.CurrentStopIndex,
            DoorFlags: (int)telemetry.Doors,
            LightFlags: (int)telemetry.Lights,
            TurnSignal: (int)telemetry.TurnSignal,
            HornActive: telemetry.HornActive,
            WipersActive: telemetry.WipersActive,
            ParkingBrakeActive: telemetry.ParkingBrakeActive,
            ReverseGear: telemetry.ReverseGear,
            LocalX: telemetry.LocalX,
            LocalY: telemetry.LocalY,
            LocalZ: telemetry.LocalZ,
            RotationX: telemetry.RotationX,
            RotationY: telemetry.RotationY,
            RotationZ: telemetry.RotationZ,
            RotationW: telemetry.RotationW);
    }

    private static async Task<PluginBridgeMessage?> SendCommandBestEffortAsync(
        PluginBridgeMessage command,
        CancellationToken cancellationToken)
    {
        if (Application.Current is not App app || !app.PluginBridge.IsConnected)
        {
            return null;
        }

        try
        {
            return await app.PluginBridge.SendCommandAsync(
                command,
                TimeSpan.FromSeconds(5),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task SendBestEffortAsync(
        PluginBridgeMessage message,
        CancellationToken cancellationToken)
    {
        if (Application.Current is not App app || !app.PluginBridge.IsConnected)
        {
            return;
        }

        try
        {
            await app.PluginBridge.SendMessageAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // O bridge é experimental e opcional. Falhas locais nunca devem
            // afetar a sessão multiplayer, o HUD ou a conexão SignalR.
        }
    }
}
