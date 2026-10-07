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
    private static readonly RemoteVehicleRegistry RemoteVehicles = new();
    private static readonly TrafficVehicleRegistry TrafficVehicles = new();
    private static readonly ConcurrentQueue<PluginBridgeMessage> OutboundCommandResults = new();
    private const long SuccessfulCommandLogIntervalMs = 5_000;

    private static CancellationTokenSource? _lifetimeCts;
    private static Task? _loopTask;
    private static Action<string>? _log;
    private static PluginBridgeMessage? _localState;
    private static PluginBridgeMessage? _pendingStatus;
    private static string[]? _cachedCapabilities;
    private static long _lastSuccessfulCommandLogTickMs;
    private static long _successfulCommandResultsSinceLog;

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

    public static PluginRegistryStatus PruneAndSnapshotRemoteStates()
    {
        var remote =
            RemoteVehicles.PruneAndCount(GetLocalState());
        var trafficRemoved = TrafficVehicles.PruneStale();
        return new PluginRegistryStatus(
            remote.Removed + trafficRemoved,
            remote.Total,
            remote.Compatible);
    }

    public static int PruneStaleRemoteStates() =>
        PruneAndSnapshotRemoteStates().StaleRemoved;

    public static void QueueCommandResult(PluginBridgeMessage result)
    {
        if (!string.Equals(result.Type, PluginBridgeProtocol.CommandResult, StringComparison.Ordinal))
        {
            return;
        }

        OutboundCommandResults.Enqueue(result);

        // Successful pose/visual updates are high-frequency state, not useful
        // as one log line per command. Keep every IPC result, but batch only the
        // diagnostic log so string formatting/file-queue pressure stays off the
        // OMSI callback thread. Errors remain fully logged below.
        if (result.Success == true)
        {
            Interlocked.Increment(ref _successfulCommandResultsSinceLog);
            var now = Environment.TickCount64;
            var previous = Interlocked.Read(ref _lastSuccessfulCommandLogTickMs);
            if (previous > 0 &&
                now >= previous &&
                now - previous < SuccessfulCommandLogIntervalMs)
            {
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _lastSuccessfulCommandLogTickMs,
                    now,
                    previous) != previous)
            {
                return;
            }

            var batched = Interlocked.Exchange(
                ref _successfulCommandResultsSinceLog,
                0);
            Log(
                $"command-results success={batched} latest={result.CharacterInstanceId ?? result.VehicleInstanceId ?? result.PlayerId ?? "-"}");
            return;
        }

        var detail = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? "-"
            : result.ErrorMessage
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        if (detail.Length > 240)
        {
            detail = detail[..240];
        }

        Log(
            $"command-result id={result.CharacterInstanceId ?? result.VehicleInstanceId ?? result.PlayerId ?? "-"} " +
            $"success={result.Success} error={result.ErrorCode ?? "-"} detail={detail}");
    }

    public static void ReportRuntimeStatus(
        long systemVariableCallbacks,
        int lastSystemVariableIndex,
        int staleRemovedCount,
        int remoteVehicleCount,
        int compatibleRemoteVehicleCount,
        double? speedKph = null,
        bool? stopRequested = null,
        double? cabinTemperatureC = null,
        int? passengerCount = null,
        bool? scheduleActive = null,
        double? simulationTime = null,
        int? simulationDay = null,
        int? simulationMonth = null,
        int? simulationYear = null,
        bool? simulationPaused = null,
        string? ibisLineCourse = null,
        string? ibisRouteCode = null,
        string? ibisTerminusName = null,
        string? ibisDelayMinutes = null,
        string? ibisDelaySeconds = null,
        string? ibisDelayState = null,
        int? pluginPressureLevel = null,
        double? pluginWorkMilliseconds = null,
        double? pluginAverageWorkMilliseconds = null,
        double? pluginAverageFrameIntervalMilliseconds = null,
        long? pluginMinimumWorkIntervalMilliseconds = null,
        int? pluginMaxCommandsPerSlice = null,
        double? pluginLastFrameIntervalMilliseconds = null,
        double? pluginPeakFrameIntervalMilliseconds = null,
        long? pluginFrameStallCount = null,
        string? performanceProfile = null)
    {
        LocalVehicleVarsSampler.Sample();

        int? physicalGridX = null;
        int? physicalGridY = null;
        int? physicalMapTileIndex = null;
        try
        {
            // The grid helper already resolves PlayerVehicle, Kachel and the
            // tile index. Use it as the normal single native call. Only fall
            // back to the direct tile read when OMSI exposes a Kachel that can
            // be indexed but whose grid cannot currently be resolved.
            if (OmsiNativeInterop.ReadPlayerVehicleGrid(
                    out var gridX,
                    out var gridY,
                    out var mapTileIndex) == 1)
            {
                physicalGridX = gridX;
                physicalGridY = gridY;
                if (mapTileIndex >= 0)
                {
                    physicalMapTileIndex = mapTileIndex;
                }
            }
            else
            {
                var playerVehicle =
                    OmsiNativeInterop.GetPlayerVehiclePointer();
                if (playerVehicle != 0)
                {
                    var directTileIndex =
                        OmsiNativeInterop.ReadRoadVehicleTileIndex(
                            playerVehicle);
                    if (directTileIndex >= 0)
                    {
                        physicalMapTileIndex = directTileIndex;
                    }
                }
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (BadImageFormatException)
        {
        }

        var status = new PluginBridgeMessage(
            PluginBridgeProtocol.PluginStatus,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            ComponentVersion: ComponentVersion,
            TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            SpeedKph: speedKph,
            SystemVariableCallbacks: systemVariableCallbacks,
            RemoteVehicleCount: remoteVehicleCount,
            CompatibleRemoteVehicleCount: compatibleRemoteVehicleCount,
            StaleRemovedCount: staleRemovedCount,
            LastSystemVariableIndex: lastSystemVariableIndex,
            StopRequested: stopRequested,
            CabinTemperatureC: cabinTemperatureC,
            PassengerCount: passengerCount,
            ScheduleActive: scheduleActive,
            SimulationTime: simulationTime,
            SimulationDay: simulationDay,
            SimulationMonth: simulationMonth,
            SimulationYear: simulationYear,
            SimulationPaused: simulationPaused,
            IbisLineCourse: ibisLineCourse,
            IbisRouteCode: ibisRouteCode,
            IbisTerminusName: ibisTerminusName,
            IbisDelayMinutes: ibisDelayMinutes,
            IbisDelaySeconds: ibisDelaySeconds,
            IbisDelayState: ibisDelayState,
            GridX: physicalGridX,
            GridY: physicalGridY,
            MapTileIndex: physicalMapTileIndex,
            PluginPressureLevel: pluginPressureLevel,
            PluginWorkMilliseconds: pluginWorkMilliseconds,
            PluginAverageWorkMilliseconds: pluginAverageWorkMilliseconds,
            PluginAverageFrameIntervalMilliseconds: pluginAverageFrameIntervalMilliseconds,
            PluginMinimumWorkIntervalMilliseconds: pluginMinimumWorkIntervalMilliseconds,
            PluginMaxCommandsPerSlice: pluginMaxCommandsPerSlice,
            PluginLastFrameIntervalMilliseconds: pluginLastFrameIntervalMilliseconds,
            PluginPeakFrameIntervalMilliseconds: pluginPeakFrameIntervalMilliseconds,
            PluginFrameStallCount: pluginFrameStallCount,
            PerformanceProfile:
                performanceProfile ??
                OmsiPerformanceGovernor.CurrentProfile,
            ExperimentalWritesEnabled:
                ExperimentalVehicleCommandProcessor.ExperimentalWritesEnabled ||
                RoleplayCharacterCommandProcessor.ExperimentalWritesEnabled ||
                LocalVehicleCommandProcessor.ExperimentalWritesEnabled,
            Capabilities: GetCachedCapabilities());

        Interlocked.Exchange(ref _pendingStatus, status);
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
                    Capabilities: GetCachedCapabilities());
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

            var localVars = LocalVehicleVarsSampler.TakePending();
            if (localVars is not null)
            {
                var varsJson = SerializeMessage(localVars);
                if (varsJson.Length <= PluginBridgeProtocol.MaxMessageChars)
                {
                    await writer.WriteLineAsync(varsJson.AsMemory(), cancellationToken);
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

    private static PluginBridgeMessage? TakePendingStatus() =>
        Interlocked.Exchange(ref _pendingStatus, null);

    private static void ClearPendingStatus() =>
        Interlocked.Exchange(ref _pendingStatus, null);

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

        if (string.Equals(
                message.Type,
                PluginBridgeProtocol.ConfigureLocalVehicleVars,
                StringComparison.Ordinal))
        {
            if (!LocalVehicleVarsSampler.Configure(
                    message,
                    out var localVarsRejection))
            {
                Log(
                    $"local-vars-config rejeitado reason={localVarsRejection ?? "unknown"}");
            }
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.ClearRemoteVehicles, StringComparison.Ordinal))
        {
            RemoteVehicles.Clear();
            PhysicalVehicleLifecycleSupervisor.RequestClearRemoteVehicles();
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.ClearTrafficVehicles, StringComparison.Ordinal))
        {
            TrafficVehicles.Clear();
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.SetPerformanceProfile, StringComparison.Ordinal))
        {
            OmsiPerformanceGovernor.SetProfile(message.PerformanceProfile);
            Log($"performance-profile={OmsiPerformanceGovernor.CurrentProfile}");
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
            RemoteVehicleVarsRegistry.Remove(message.PlayerId);
            PhysicalVehicleLifecycleSupervisor.RequestRemoteRemoval(message.PlayerId);
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleVars, StringComparison.Ordinal))
        {
            if (!RemoteVehicleVarsRegistry.TryApply(message, out var rejectionReason))
            {
                Log($"remote-vars rejeitado player={message.PlayerId ?? "-"} reason={rejectionReason ?? "unknown"}");
            }
            return null;
        }

        if (string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleState, StringComparison.Ordinal))
        {
            var admittedIdentity =
                !string.IsNullOrWhiteSpace(message.PlayerId) &&
                !string.IsNullOrWhiteSpace(message.VehicleInstanceId) &&
                string.Equals(
                    message.PlayerId,
                    message.VehicleInstanceId,
                    StringComparison.OrdinalIgnoreCase);
            if (admittedIdentity)
            {
                RemoteVehicleVarsRegistry.ObserveAdmittedVehicleIdentity(
                    message.PlayerId,
                    message.VehiclePath);
            }

            if (message.SyncTableHash is uint)
            {
                if (!RemoteVehicleVarsRegistry.TryApplyVisualState(
                        message,
                        out var visualRejection))
                {
                    RemoteVehicleVarsRegistry.ClearVisualState(
                        message.PlayerId);
                    Log(
                        $"remote-visual-sync rejeitado player={message.PlayerId ?? "-"} " +
                        $"reason={visualRejection ?? "unknown"}");
                }
            }
            else
            {
                RemoteVehicleVarsRegistry.ClearVisualState(
                    message.PlayerId);
            }

            if (RemoteVehicles.Upsert(message))
            {
                // The state stream already contains the exact bus path, local
                // pose, quaternion and Kachel. Feed it directly into the OMSI-
                // side lifecycle so physical buses no longer depend on a
                // separate desktop spawn command successfully crossing WPF.
                PhysicalVehicleLifecycleSupervisor.ObserveRemoteState(
                    message,
                    GetLocalState());
            }
            return null;
        }

        if (LocalVehicleCommandProcessor.IsCommandType(message.Type))
        {
            if (LocalVehicleCommandProcessor.TryRejectBeforeOmsiThread(message, out var rejection))
            {
                return rejection;
            }

            if (!OmsiThreadCommandQueue.TryEnqueue(message))
            {
                return LocalVehicleCommandProcessor.Result(
                    message,
                    false,
                    "command-queue-full",
                    "The OMSI local vehicle command queue is full.");
            }

            Log(
                $"local-vehicle-command queued trigger={message.TriggerName ?? "-"} " +
                $"pending={OmsiThreadCommandQueue.Count}");
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

    private static PluginBridgeMessage? GetLocalState() =>
        Volatile.Read(ref _localState);

    private static void SetLocalState(PluginBridgeMessage? state) =>
        Interlocked.Exchange(ref _localState, state);

    private static void ClearAllState()
    {
        SetLocalState(null);
        RemoteVehicles.Clear();
        RemoteVehicleVarsRegistry.Clear();
        LocalVehicleVarsSampler.Clear();
        TrafficVehicles.Clear();
        OmsiThreadCommandQueue.Clear();
        // The pipe worker cannot touch OMSI objects directly. Ask the callback
        // thread to clean up plugin-owned physical lifecycle state safely.
        PhysicalVehicleLifecycleSupervisor.RequestReset();
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

    private static string[] GetCachedCapabilities()
    {
        var cached = Volatile.Read(ref _cachedCapabilities);
        if (cached is not null)
        {
            return cached;
        }

        var built = ExperimentalVehicleCommandProcessor
            .GetCapabilities()
            .Append(PluginBridgeProtocol.CapabilityPerformanceGovernor)
            .Append(PluginBridgeProtocol.CapabilityRemoteScriptVars)
            .Append(PluginBridgeProtocol.CapabilityLocalScriptVars)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Interlocked.CompareExchange(
            ref _cachedCapabilities,
            built,
            null);
        return _cachedCapabilities ?? built;
    }

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

    public readonly record struct PluginRegistryStatus(
        int StaleRemoved,
        int RemoteTotal,
        int RemoteCompatible);

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
