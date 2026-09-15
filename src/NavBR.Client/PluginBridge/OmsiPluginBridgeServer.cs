using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client.PluginBridge;

public sealed class OmsiPluginBridgeServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _connectionSync = new();
    private Task? _acceptLoop;
    private StreamWriter? _writer;

    public bool IsConnected
    {
        get
        {
            lock (_connectionSync)
            {
                return _writer is not null;
            }
        }
    }

    public event Action<bool>? ConnectionStateChanged;

    public void Start()
    {
        if (_acceptLoop is not null)
        {
            return;
        }

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_lifetimeCts.Token));
    }

    public async Task SendMessageAsync(
        PluginBridgeMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message.ProtocolVersion != PluginBridgeProtocol.Version ||
            !IsClientMessageType(message.Type))
        {
            throw new ArgumentException("Unsupported NavBR plugin bridge message.", nameof(message));
        }

        StreamWriter? writer;
        lock (_connectionSync)
        {
            writer = _writer;
        }

        if (writer is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(message);
        if (json.Length > PluginBridgeProtocol.MaxMessageChars)
        {
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
            // The accept loop will observe the disconnect and allow a reconnect.
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PluginBridgeProtocol.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.WaitForConnectionAsync(cancellationToken);

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

                var helloLine = await reader.ReadLineAsync(cancellationToken);
                if (!TryParseMessage(helloLine, out var hello) ||
                    hello is null ||
                    !string.Equals(hello.Type, PluginBridgeProtocol.PluginHello, StringComparison.Ordinal) ||
                    hello.ProtocolVersion != PluginBridgeProtocol.Version)
                {
                    continue;
                }

                var response = new PluginBridgeMessage(
                    PluginBridgeProtocol.ClientHello,
                    PluginBridgeProtocol.Version,
                    ProcessId: Environment.ProcessId,
                    ComponentVersion: typeof(OmsiPluginBridgeServer).Assembly.GetName().Version?.ToString());

                await writer.WriteLineAsync(JsonSerializer.Serialize(response));

                SetConnectedWriter(writer);
                try
                {
                    while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                        {
                            break;
                        }

                        if (line.Length > PluginBridgeProtocol.MaxMessageChars)
                        {
                            break;
                        }

                        // Phase 1 only needs to keep the duplex channel alive.
                        // Future plugin acknowledgements/diagnostics can be handled here.
                    }
                }
                finally
                {
                    ClearConnectedWriter(writer);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
        }
    }

    private static bool IsClientMessageType(string type) =>
        string.Equals(type, PluginBridgeProtocol.RemoteVehicleState, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.RemoteVehicleRemoved, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.ClearRemoteVehicles, StringComparison.Ordinal);

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

    private void SetConnectedWriter(StreamWriter writer)
    {
        lock (_connectionSync)
        {
            _writer = writer;
        }

        ConnectionStateChanged?.Invoke(true);
    }

    private void ClearConnectedWriter(StreamWriter writer)
    {
        var changed = false;
        lock (_connectionSync)
        {
            if (ReferenceEquals(_writer, writer))
            {
                _writer = null;
                changed = true;
            }
        }

        if (changed)
        {
            ConnectionStateChanged?.Invoke(false);
        }
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetimeCts.Cancel();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _writeLock.Dispose();
        _lifetimeCts.Dispose();
    }
}
