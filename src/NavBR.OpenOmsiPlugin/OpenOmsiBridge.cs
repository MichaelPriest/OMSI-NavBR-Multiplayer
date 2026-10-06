using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal static class OpenOmsiBridge
{
    private static CancellationTokenSource? _lifetime;
    private static Task? _worker;
    private static PluginBridgeMessage? _pendingStatus;

    public static void Start()
    {
        if (_worker is not null)
        {
            return;
        }

        _lifetime = new CancellationTokenSource();
        _worker = Task.Run(() => RunAsync(_lifetime.Token));
    }

    public static void Stop()
    {
        _lifetime?.Cancel();
        _lifetime = null;
        _worker = null;
        Interlocked.Exchange(ref _pendingStatus, null);
    }

    public static void QueueStatus(PluginBridgeMessage status) =>
        Interlocked.Exchange(ref _pendingStatus, status);

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
                    PipeOptions.Asynchronous);
                await pipe.ConnectAsync(1000, cancellationToken);

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

                var version =
                    typeof(OpenOmsiBridge).Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                        .InformationalVersion
                    ?? typeof(OpenOmsiBridge).Assembly.GetName().Version?.ToString();

                await writer.WriteLineAsync(Serialize(new PluginBridgeMessage(
                    PluginBridgeProtocol.PluginHello,
                    PluginBridgeProtocol.Version,
                    ProcessId: Environment.ProcessId,
                    ComponentVersion: version)));

                var hello = await reader.ReadLineAsync(cancellationToken);
                if (!TryParse(hello, out var response) ||
                    response is null ||
                    !string.Equals(
                        response.Type,
                        PluginBridgeProtocol.ClientHello,
                        StringComparison.Ordinal))
                {
                    await DelayRetryAsync(cancellationToken);
                    continue;
                }

                await writer.WriteLineAsync(Serialize(new PluginBridgeMessage(
                    PluginBridgeProtocol.PluginCapabilities,
                    PluginBridgeProtocol.Version,
                    ProcessId: Environment.ProcessId,
                    ComponentVersion: version,
                    TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExperimentalWritesEnabled: false,
                    Capabilities:
                    [
                        PluginBridgeProtocol.CapabilityAdvancedTelemetry,
                        PluginBridgeProtocol.CapabilityPerformanceGovernor,
                        PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin,
                        PluginBridgeProtocol.CapabilityOpenOmsiLuaSnapshot,
                        PluginBridgeProtocol.CapabilityOpenOmsiNearbyVehicles,
                        PluginBridgeProtocol.CapabilityOpenOmsiTimetableContext,
                        PluginBridgeProtocol.CapabilityOpenOmsiNativeOnFoot,
                        PluginBridgeProtocol.CapabilityOpenOmsiNavigationRuntime,
                        PluginBridgeProtocol.CapabilityOpenOmsiHudConfiguration,
                        PluginBridgeProtocol.CapabilityOpenOmsiRouteRejoin,
                        PluginBridgeProtocol.CapabilityOpenOmsiTimetableResolver,
                        PluginBridgeProtocol.CapabilityOpenOmsiRouteSteps
                    ])));

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var sender = Task.Run(
                    () => SendStatusLoopAsync(writer, linked.Token),
                    linked.Token);
                try
                {
                    while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null)
                        {
                            break;
                        }

                        if (!TryParse(line, out var message) || message is null)
                        {
                            continue;
                        }

                        if (string.Equals(
                                message.Type,
                                PluginBridgeProtocol.SetPerformanceProfile,
                                StringComparison.Ordinal))
                        {
                            PluginExports.SetPerformanceProfile(message.PerformanceProfile);
                            QueueStatus(PluginExports.BuildStatus());
                            continue;
                        }

                        if (string.Equals(
                                message.Type,
                                PluginBridgeProtocol.SetOpenOmsiHudConfiguration,
                                StringComparison.Ordinal))
                        {
                            OpenOmsiHudState.Apply(message);
                            QueueStatus(PluginExports.BuildStatus());
                            continue;
                        }

                        if (string.Equals(
                                message.Type,
                                PluginBridgeProtocol.SetOpenOmsiRoutePolyline,
                                StringComparison.Ordinal))
                        {
                            OpenOmsiRouteRuntime.SetRoute(message);
                            QueueStatus(PluginExports.BuildStatus());
                        }
                    }
                }
                finally
                {
                    linked.Cancel();
                    try
                    {
                        await sender;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
            }

            await DelayRetryAsync(cancellationToken);
        }
    }

    private static async Task SendStatusLoopAsync(
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        var lastSent = 0L;
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = Environment.TickCount64;
            var interval = PluginExports.StatusIntervalMilliseconds;
            if (lastSent <= 0 || now - lastSent >= interval)
            {
                var status =
                    Interlocked.Exchange(ref _pendingStatus, null)
                    ?? PluginExports.BuildStatus();
                await writer.WriteLineAsync(Serialize(status).AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                lastSent = now;
            }

            await Task.Delay(50, cancellationToken);
        }
    }

    private static string Serialize(PluginBridgeMessage message) =>
        JsonSerializer.Serialize(
            message,
            OpenOmsiPluginJsonContext.Default.PluginBridgeMessage);

    private static bool TryParse(
        string? json,
        out PluginBridgeMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(json) ||
            json.Length > PluginBridgeProtocol.MaxMessageChars)
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize(
                json,
                OpenOmsiPluginJsonContext.Default.PluginBridgeMessage);
            return message is not null &&
                   message.ProtocolVersion == PluginBridgeProtocol.Version;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task DelayRetryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(1500, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
