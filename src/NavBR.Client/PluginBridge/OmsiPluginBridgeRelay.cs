using System.Windows;
using NavBR.Client.Diagnostics;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.PluginBridge;

public static class OmsiPluginBridgeRelay
{
    public static string? ResolveCurrentMapCompatibilityId(string? fallback = null)
    {
        var normalizedFallback =
            string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();

        // Remote physical-vehicle processing runs from SignalR/telemetry
        // worker threads. MainWindow is a WPF DispatcherObject and must never
        // be queried from those threads. The telemetry already carries the
        // authoritative map fingerprint; only enrich it from MainWindow when
        // this call is actually executing on the UI dispatcher.
        if (Application.Current?.MainWindow is MainWindow mainWindow &&
            mainWindow.Dispatcher.CheckAccess())
        {
            var current = mainWindow.GetCurrentMapCompatibilityIdForPlugin();
            if (!string.IsNullOrWhiteSpace(current))
            {
                return current.Trim();
            }
        }

        return normalizedFallback;
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

    public static Task<PluginBridgeMessage?> AcquireRoleplayCharacterAsync(
        string characterInstanceId,
        string? playerId,
        string? displayName,
        double localX,
        double localY,
        double localZ,
        double headingDegrees,
        int characterDefinitionPointer,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.AcquireRoleplayCharacter,
                PluginBridgeProtocol.Version,
                PlayerId: playerId,
                DisplayName: displayName,
                CharacterInstanceId: characterInstanceId,
                CharacterDefinitionPointer: characterDefinitionPointer,
                LocalX: localX,
                LocalY: localY,
                LocalZ: localZ,
                HeadingDegrees: headingDegrees,
                CharacterActive: true),
            cancellationToken);

    public static Task<PluginBridgeMessage?> UpdateRoleplayCharacterAsync(
        string characterInstanceId,
        RoleplayCharacterState state,
        string? displayName = null,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.UpdateRoleplayCharacter,
                PluginBridgeProtocol.Version,
                PlayerId: state.PlayerId,
                DisplayName: displayName,
                MapName: state.MapName,
                MapCompatibilityId: state.MapCompatibilityId,
                TimestampUnixMilliseconds: state.Timestamp.ToUnixTimeMilliseconds(),
                CharacterInstanceId: characterInstanceId,
                CharacterActivity: state.Activity.ToString(),
                CharacterActive: state.IsActive,
                LocalX: state.LocalX,
                LocalY: state.LocalY,
                LocalZ: state.LocalZ,
                HeadingDegrees: state.HeadingDegrees,
                SpeedMps: state.SpeedMps),
            cancellationToken);

    public static Task<PluginBridgeMessage?> ReleaseRoleplayCharacterAsync(
        string characterInstanceId,
        string? playerId = null,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.ReleaseRoleplayCharacter,
                PluginBridgeProtocol.Version,
                PlayerId: playerId,
                CharacterInstanceId: characterInstanceId,
                CharacterActive: false),
            cancellationToken);

    public static Task<PluginBridgeMessage?> SetRoleplayVehicleTriggerAsync(
        string characterInstanceId,
        string? playerId,
        string triggerName,
        bool active,
        CancellationToken cancellationToken = default) =>
        SendCommandBestEffortAsync(
            new PluginBridgeMessage(
                PluginBridgeProtocol.TriggerRoleplayVehicle,
                PluginBridgeProtocol.Version,
                PlayerId: playerId,
                CharacterInstanceId: characterInstanceId,
                CharacterActive: true,
                TriggerName: triggerName,
                TriggerActive: active),
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
            RotationW: telemetry.RotationW,
            MapTileIndex: telemetry.MapTileIndex);
    }

    private static async Task<PluginBridgeMessage?> SendCommandBestEffortAsync(
        PluginBridgeMessage command,
        CancellationToken cancellationToken)
    {
        var commandId = string.IsNullOrWhiteSpace(command.CommandId)
            ? Guid.NewGuid().ToString("N")
            : command.CommandId;
        var normalized = command with { CommandId = commandId };
        var instanceId =
            normalized.VehicleInstanceId ??
            normalized.CharacterInstanceId ??
            normalized.PlayerId ??
            "-";
        var trace = ShouldTraceCommand(normalized.Type);

        if (Application.Current is not App app || !app.PluginBridge.IsConnected)
        {
            if (trace)
            {
                NavBRAppLog.Info(
                    "plugin-bridge-command-skipped",
                    $"type={normalized.Type} id={instanceId} command={commandId} reason=disconnected");
            }
            return null;
        }

        try
        {
            if (trace)
            {
                NavBRAppLog.Info(
                    "plugin-bridge-command-send",
                    $"type={normalized.Type} id={instanceId} command={commandId}");
            }

            var result = await app.PluginBridge.SendCommandAsync(
                normalized,
                TimeSpan.FromSeconds(5),
                cancellationToken);

            if (trace || result.Success != true)
            {
                NavBRAppLog.Info(
                    "plugin-bridge-command-result",
                    $"type={normalized.Type} id={instanceId} command={commandId} " +
                    $"success={result.Success?.ToString() ?? "-"} error={result.ErrorCode ?? "-"} " +
                    $"parts={result.RemoteVehicleCount?.ToString() ?? "-"} detail={SanitizeBridgeDetail(result.ErrorMessage)}");
            }

            return result;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            if (trace)
            {
                NavBRAppLog.Info(
                    "plugin-bridge-command-cancelled",
                    $"type={normalized.Type} id={instanceId} command={commandId} detail={SanitizeBridgeDetail(ex.Message)}");
            }
            return null;
        }
        catch (Exception ex)
        {
            NavBRAppLog.Info(
                "plugin-bridge-command-exception",
                $"type={normalized.Type} id={instanceId} command={commandId} " +
                $"exception={ex.GetType().Name} detail={SanitizeBridgeDetail(ex.Message)}");
            return null;
        }
    }

    private static bool ShouldTraceCommand(string type) =>
        string.Equals(type, PluginBridgeProtocol.SpawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.SpawnGhostVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnGhostVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.AcquireRoleplayCharacter, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.ReleaseRoleplayCharacter, StringComparison.Ordinal);

    private static string SanitizeBridgeDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var detail = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return detail.Length <= 320 ? detail : detail[..320];
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
