using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace NavBR.Client.WinUIBridge;

internal sealed class NativeShellBridgeServer : IAsyncDisposable
{
    public const string PipeName = "OMSI.NavBR.Multiplayer.NativeShell.v1";

    private readonly MainWindow _owner;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;

    public NativeShellBridgeServer(MainWindow owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void Start()
    {
        if (_serverTask is not null)
        {
            return;
        }

        _serverTask = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(
                    pipe,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 16 * 1024,
                    leaveOpen: true);
                using var writer = new StreamWriter(
                    pipe,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 16 * 1024,
                    leaveOpen: true)
                {
                    AutoFlush = true
                };

                var requestLine = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        ok = false,
                        error = "empty_request"
                    }));
                    continue;
                }

                NativeShellRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<NativeShellRequest>(
                        requestLine,
                        JsonOptions);
                }
                catch (Exception ex)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        ok = false,
                        error = $"invalid_json: {ex.Message}"
                    }));
                    continue;
                }

                if (request is null || string.IsNullOrWhiteSpace(request.Kind))
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        ok = false,
                        error = "invalid_request"
                    }));
                    continue;
                }

                var response = await HandleRequestAsync(request, cancellationToken);
                await writer.WriteLineAsync(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // A shell can close during a poll. Accept the next connection.
            }
            catch (Exception ex)
            {
                try
                {
                    if (pipe.IsConnected)
                    {
                        using var writer = new StreamWriter(
                            pipe,
                            new UTF8Encoding(false),
                            4096,
                            leaveOpen: true)
                        {
                            AutoFlush = true
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new
                        {
                            ok = false,
                            error = ex.Message
                        }));
                    }
                }
                catch
                {
                    // IPC diagnostics must not terminate the runtime host.
                }
            }
        }
    }

    private async Task<string> HandleRequestAsync(
        NativeShellRequest request,
        CancellationToken cancellationToken)
    {
        switch (request.Kind.Trim().ToLowerInvariant())
        {
            case "ping":
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    protocol = 1,
                    runtimePid = Environment.ProcessId
                });

            case "state":
            {
                var state = await _owner.Dispatcher.InvokeAsync(
                    _owner.BuildNativeShellState);
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    payload = state
                });
            }

            case "command":
            {
                if (string.IsNullOrWhiteSpace(request.Command))
                {
                    return JsonSerializer.Serialize(new
                    {
                        ok = false,
                        error = "missing_command"
                    });
                }

                await _owner.Dispatcher
                    .InvokeAsync(() =>
                        _owner.ExecuteNativeShellCommandAsync(
                            request.Command,
                            request.Payload))
                    .Task
                    .Unwrap();

                return JsonSerializer.Serialize(new
                {
                    ok = true
                });
            }

            case "shutdown":
                _ = _owner.Dispatcher.InvokeAsync(() =>
                    Application.Current?.Shutdown());
                return JsonSerializer.Serialize(new
                {
                    ok = true
                });

            default:
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = "unknown_request"
                });
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_serverTask is not null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record NativeShellRequest(
        string Kind,
        string? Command,
        JsonElement? Payload);
}
