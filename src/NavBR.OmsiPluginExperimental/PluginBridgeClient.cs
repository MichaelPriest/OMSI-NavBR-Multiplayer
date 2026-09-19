using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class PluginBridgeClient
{
    private static readonly object LocalStateSync = new();
    private static readonly object StatusSync = new();
    private static readonly RemoteVehicleRegistry RemoteVehicles = new();
    private static readonly TrafficVehicleRegistry TrafficVehicles = new();
    private static readonly ConcurrentQueue<PluginBridgeMessage> OutboundCommandResults = new();

    private static CancellationTokenSource? _lifetimeCts;
    private static Task? _loopTask;
    private static Action<string>? _log;
    private static PluginBridgeMessage? _localState;
    private static PluginBridgeMessage? _pendingStatus;

    private static readonly string? ComponentVersion =
        typeof(PluginBridgeClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? typeof(PluginBridgeClient).Assembly.GetName().Version?.ToString();

    public static PluginBridgeMessage? LatestRemoteState =>
        RemoteVehicles.LatestCompatible(GetLocalState());

    public static PluginBridgeMessage? LatestTrafficSnapshot =>
        TrafficVehicles.LatestCompatible(GetLocalState());

    public static int RemoteVehicleCount => RemoteVehicles.Count;

    public static int CompatibleRemoteVehicleCount =>
        RemoteVehicles.CountCompatible(GetLocalState());

    public static int TrafficVehicleCount => TrafficVehicles.Count;

    public static string? TrafficAuthorityPlayerId => TrafficVehicles.AuthorityPlayerId;

    public static long? TrafficSequence => TrafficVehicles.Sequence;

    public static void Start(Action<string> log)
    {
        if (_loopTask is not null)
        {
            return;
        }

        _log = log;
        _lifetimeCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_lifetimeCts.Token));
    }

    public static void Stop()
    {
        _lifetimeCts?.Cancel();
        _lifetimeCts = null;
        _loopTask = null;
        ClearAllState();
        ClearPendingStatus();
        ClearOutboundCommandResults();
    }

    public static int PruneStaleRemoteStates() =>
        RemoteVehicles.PruneStale() + TrafficVehicles.PruneStale();

    public static void QueueCommandResult(PluginBridgeMessage result)
    {
        if (!string.Equals(result.Type, PluginBridgeProtocol.CommandResult, StringComparison.Ordinal))
        {
            return;
        }

        OutboundCommandResults.Enqueue(result);
        Log(
            $"command-result id={result.CharacterInstanceId ?? result.VehicleInstanceId ?? result.PlayerId ?? "-"} " +
            $"success={result.Success} error={result.ErrorCode ?? "-"}");
    }

    public static void ReportRuntimeStatus(
        long systemVariableCallbacks,
        int lastSystemVariableIndex,
        int staleRemovedCount,
        double? speedKph = null,
        bool? stopRequested = null)
    {
        var status = new PluginBridgeMessage(
            PluginBridgeProtocol.PluginStatus,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            ComponentVersion: ComponentVersion,
            TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SpeedKph: speedKph,
            SystemVariableCallbacks: systemVariableCallbacks,
            RemoteVehicleCount: RemoteVehicleCount,
            CompatibleRemoteVehicleCount: CompatibleRemoteVehicleCount,
            StaleRemovedCount: staleRemovedCount,
            LastSystemVariableIndex: lastSystemVariableIndex,
            StopRequested: stopRequested,
            ExperimentalWritesEnabled:
                ExperimentalVehicleCommandProcessor.ExperimentalWritesEnabled ||
                RoleplayCharacterCommandProcessor.ExperimentalWritesEnabled,
            Capabilities: ExperimentalVehicleCommandProcessor.GetCapabilities());

        lock (StatusSync)
        {
            _pendingStatus = status;
        }
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    PluginBridgeProtocol.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.ConnectAsync(1_000, cancellationToken);

                using var reader = new StreamReader(
                    pipe,
                    new UTF8Encoding(false),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 4096,
                    leaveOpen: true);
                using var writer = new StreamWriter(
                    pipe,
                    new UTF8Encoding(false),
                    bufferSize: 4096,
                    leaveOpen: true)
                {
                    AutoFlush = true
                };

                var hello = new PluginBridgeMessage(
                    PluginBridgeProtocol.PluginHello,
                    PluginBridgeProtocol.Version,
                    ProcessId: Environment.ProcessId,
                    ComponentVersion: ComponentVersion);

                await writer.WriteLineAsync(SerializeMessage(hello));

                var responseLine = await reader.ReadLineAsync(cancellationToken);
                if (!TryParseMessage(responseLine, out var response) ||
                    response is null ||
                    !string.Equals(response.Type, PluginBridgeProtocol.ClientHello, StringComparison.Ordinal) ||
                    response.ProtocolVersion != PluginBridgeProtocol.Version)
                {
                    Log("bridge handshake rejeitado");
                    await DelayBeforeRetryAsync(cancellationToken);
                    continue;
                }

                ClearOutboundCommandResults();
                Log($"bridge conectado clientPid={response.ProcessId} protocol={response.ProtocolVersion}");

                var capabilities = new PluginBridgeMessage(
                    PluginBridgeProtocol.PluginCapabilities,
                    PluginBridgeProtocol.Version,
                    ProcessId: Environment.ProcessId,
                    ComponentVersion: ComponentVersion,
                    TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExperimentalWritesEnabled:
                        ExperimentalVehicleCommandProcessor.ExperimentalWritesEnabled ||
                        RoleplayCharacterCommandProcessor.ExperimentalWritesEnabled,
                    Capabilities: ExperimentalVehicleCommandProcessor.GetCapabilities());
                await writer.WriteLineAsync(SerializeMessage(capabilities));
                await writer.FlushAsync(cancellationToken);

                using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var outboundSender = Task.Run(
                    () => SendPendingOutboundLoopAsync(writer, connectionCts.Token),
                    connectionCts.Token);

                try
                {
                    while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                        {
                            break;
                        }

                        if (!TryParseMessage(line, out var message) ||
                            message is null ||
                            message.ProtocolVersion != PluginBridgeProtocol.Version)
                        {
                            continue;
                        }

                        var result = ApplyMessage(message);
                        if (result is not null)
                        {
                            QueueCommandResult(result);
                        }
                    }
                }
                finally
                {
                    connectionCts.Cancel();
                    try
                    {
                        await outboundSender;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                ClearAllState();
                ClearOutboundCommandResults();
                Log("bridge desconectado");
            }
            catch (TimeoutException)
            {
                // O cliente NavBR pode não estar aberto. Tenta novamente sem afetar o OMSI.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex)
            {
                ClearAllState();
                ClearOutboundCommandResults();
                Log($"bridge io: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ClearAllState();
                ClearOutboundCommandResults();
                Log($"bridge acesso negado: {ex.Message}");
            }
            catch (Exception ex)
            {
                ClearAllState();
                ClearOutboundCommandResults();
                Log($"bridge erro: {ex.GetType().Name}: {ex.Message}");
            }

            await DelayBeforeRetryAsync(cancellationToken);
        }
    }

    private static async Task SendPendingOutboundLoopAsync(
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var wroteMessage = false;
            var resultCount = 0;
            while (resultCount < 16 && OutboundCommandResults.TryDequeue(out var result))
            {
                var resultJson = SerializeMessage(result);
                if (resultJson.Length <= PluginBridgeProtocol.MaxMessageChars)
                {
                    await writer.WriteLineAsync(resultJson.AsMemory(), cancellationToken);
                    wroteMessage = true;
                }

                resultCount++;
            }

            var pendingStatus = TakePendingStatus();
            if (pendingStatus is not null)
            {
                var statusJson = SerializeMessage(pendingStatus);
                if (statusJson.Length <= PluginBridgeProtocol.MaxMessageChars)
                {
                    await writer.WriteLineAsync(statusJson.AsMemory(), cancellationToken);
                    wroteMessage = true;
                }
            }

            if (wroteMessage)
            {
                await writer.FlushAsync(cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private static PluginBridgeMessage? TakePendingStatus()
    {
        lock (StatusSync)
        {
            var pending = _pendingStatus;
            _pendingStatus = null;
            return pending;
        }
    }

    private static void ClearPendingStatus()
    {
        lock (StatusSync)
        {
            _pendingStatus = null;
        }
    }

    private static void ClearOutboundCommandResults()
    {
        while (OutboundCommandResults.TryDequeue(out _))
        {
        }
    }

    private static PluginBridgeMessage? ApplyMessage(PluginBridgeMessage message)
    {
        if (string.Equals(message.Type, PluginBridgeProtocol.LocalVehicleState, StringComparison.Ordinal))
        {
            SetLocalState(IsValidLocalState(message) ? message : null);
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.ClearRemoteVehicles, StringComparison.Ordinal))
        {
            RemoteVehicles.Clear();
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.ClearTrafficVehicles, StringComparison.Ordinal))
        {
            TrafficVehicles.Clear();
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.TrafficSnapshotState, StringComparison.Ordinal))
        {
            if (!TrafficVehicles.TryApply(message, GetLocalState(), out var rejectionReason) &&
                !string.Equals(rejectionReason, "stale-sequence", StringComparison.Ordinal))
            {
                Log($"traffic-snapshot rejeitado reason={rejectionReason ?? "unknown"}");
            }

            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleRemoved, StringComparison.Ordinal))
        {
            RemoteVehicles.Remove(message.PlayerId);
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleState, StringComparison.Ordinal))
        {
            RemoteVehicles.Upsert(message);
            return null;
        }

        if (IsVehicleCommand(message.Type))
        {
            if (ExperimentalVehicleCommandProcessor.TryRejectBeforeOmsiThread(message, out var rejection))
            {
                return rejection;
            }

            if (!OmsiThreadCommandQueue.TryEnqueue(message))
            {
                return ExperimentalVehicleCommandProcessor.Result(
                    message,
                    false,
                    "command-queue-full",
                    "The OMSI physical command queue is full.");
            }

            Log(
                $"vehicle-command queued type={message.Type} " +
                $"id={message.VehicleInstanceId ?? message.PlayerId ?? "-"} " +
                $"pending={OmsiThreadCommandQueue.Count}");
            return null;
        }

        if (RoleplayCharacterCommandProcessor.IsCharacterCommandType(message.Type))
        {
            if (RoleplayCharacterCommandProcessor.TryRejectBeforeOmsiThread(message, out var rejection))
            {
                return rejection;
            }

            if (!OmsiThreadCommandQueue.TryEnqueue(message))
            {
                return RoleplayCharacterCommandProcessor.Result(
                    message,
                    false,
                    "command-queue-full",
                    "The OMSI roleplay command queue is full.");
            }

            Log(
                $"character-command queued type={message.Type} " +
                $"id={message.CharacterInstanceId ?? message.PlayerId ?? "-"} " +
                $"pending={OmsiThreadCommandQueue.Count}");
            return null;
        }

        return null;
    }

    private static bool IsVehicleCommand(string type) =>
        string.Equals(type, PluginBridgeProtocol.SpawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.SpawnGhostVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateGhostVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnGhostVehicle, StringComparison.Ordinal);

    private static PluginBridgeMessage? GetLocalState()
    {
        lock (LocalStateSync)
        {
            return _localState;
        }
    }

    private static void SetLocalState(PluginBridgeMessage? state)
    {
        lock (LocalStateSync)
        {
            _localState = state;
        }
    }

    private static void ClearAllState()
    {
        SetLocalState(null);
        RemoteVehicles.Clear();
        TrafficVehicles.Clear();
        OmsiThreadCommandQueue.Clear();
    }

    private static bool IsValidLocalState(PluginBridgeMessage message)
    {
        if (message.ProtocolVersion != PluginBridgeProtocol.Version ||
            !string.Equals(message.Type, PluginBridgeProtocol.LocalVehicleState, StringComparison.Ordinal) ||
            (message.MapName?.Length ?? 0) > 256 ||
            (message.MapCompatibilityId?.Length ?? 0) > 256)
        {
            return false;
        }

        return IsFinite(message.X) &&
               IsFinite(message.Y) &&
               IsFinite(message.Z) &&
               IsFinite(message.HeadingDegrees) &&
               IsFinite(message.SpeedKph);
    }

    private static bool IsFinite(double? value) =>
        value is double number && double.IsFinite(number);

    private static string SerializeMessage(PluginBridgeMessage message) =>
        JsonSerializer.Serialize(
            message,
            PluginBridgeJsonContext.Default.PluginBridgeMessage);

    private static bool TryParseMessage(string? json, out PluginBridgeMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > PluginBridgeProtocol.MaxMessageChars)
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize(
                json,
                PluginBridgeJsonContext.Default.PluginBridgeMessage);
            return message is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void Log(string message)
    {
        try
        {
            _log?.Invoke(message);
        }
        catch
        {
        }
    }
}
