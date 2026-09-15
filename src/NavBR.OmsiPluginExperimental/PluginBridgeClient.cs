using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class PluginBridgeClient
{
    private static readonly object StateSync = new();
    private static readonly Dictionary<string, PluginBridgeMessage> RemoteStates =
        new(StringComparer.OrdinalIgnoreCase);

    private static CancellationTokenSource? _lifetimeCts;
    private static Task? _loopTask;
    private static PluginBridgeMessage? _latestRemoteState;
    private static Action<string>? _log;

    public static PluginBridgeMessage? LatestRemoteState
    {
        get
        {
            lock (StateSync)
            {
                return _latestRemoteState;
            }
        }
    }

    public static int RemoteVehicleCount
    {
        get
        {
            lock (StateSync)
            {
                return RemoteStates.Count;
            }
        }
    }

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
        ClearRemoteStates();
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
                    ComponentVersion: typeof(PluginBridgeClient).Assembly.GetName().Version?.ToString());

                await writer.WriteLineAsync(JsonSerializer.Serialize(hello));

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

                Log($"bridge conectado clientPid={response.ProcessId} protocol={response.ProtocolVersion}");

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

                    ApplyMessage(message);
                }

                ClearRemoteStates();
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
                ClearRemoteStates();
                Log($"bridge io: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ClearRemoteStates();
                Log($"bridge acesso negado: {ex.Message}");
            }
            catch (Exception ex)
            {
                ClearRemoteStates();
                Log($"bridge erro: {ex.GetType().Name}: {ex.Message}");
            }

            await DelayBeforeRetryAsync(cancellationToken);
        }
    }

    private static void ApplyMessage(PluginBridgeMessage message)
    {
        if (string.Equals(message.Type, PluginBridgeProtocol.ClearRemoteVehicles, StringComparison.Ordinal))
        {
            ClearRemoteStates();
            return;
        }

        if (string.IsNullOrWhiteSpace(message.PlayerId))
        {
            return;
        }

        lock (StateSync)
        {
            if (string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleRemoved, StringComparison.Ordinal))
            {
                RemoteStates.Remove(message.PlayerId);
                if (string.Equals(
                        _latestRemoteState?.PlayerId,
                        message.PlayerId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _latestRemoteState = RemoteStates.Values
                        .OrderByDescending(state => state.TimestampUnixMilliseconds ?? 0L)
                        .FirstOrDefault();
                }

                return;
            }

            if (!string.Equals(message.Type, PluginBridgeProtocol.RemoteVehicleState, StringComparison.Ordinal))
            {
                return;
            }

            RemoteStates[message.PlayerId] = message;
            _latestRemoteState = message;
        }
    }

    private static void ClearRemoteStates()
    {
        lock (StateSync)
        {
            RemoteStates.Clear();
            _latestRemoteState = null;
        }
    }

    private static bool TryParseMessage(string? json, out PluginBridgeMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > PluginBridgeProtocol.MaxMessageChars)
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<PluginBridgeMessage>(json);
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
