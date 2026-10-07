using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// Carries unchanged openOMSI v6 datagrams over WebSocket. This mirrors the
/// openOMSI omsi-net/ws design: the session still talks UDP and the bridge is
/// only a transport for networks where UDP is not reachable.
/// </summary>
public static class OpenOmsiWebSocketBridge
{
    public static string NormalizeWebSocketUrl(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var value = target.Trim();

        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "wss://" + value[8..];
        }
        else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            value = "ws://" + value[7..];
        }
        else if (!value.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                 !value.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Contains(".trycloudflare.com", StringComparison.OrdinalIgnoreCase)
                ? "wss://" + value
                : "ws://" + value;
        }

        value = value.TrimEnd('/');
        return value.EndsWith("/ws", StringComparison.OrdinalIgnoreCase)
            ? value
            : value + "/ws";
    }
}

public sealed class OpenOmsiWebSocketGateway : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly IPEndPoint _udpTarget;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;

    private OpenOmsiWebSocketGateway(
        HttpListener listener,
        IPEndPoint udpTarget,
        int port)
    {
        _listener = listener;
        _udpTarget = udpTarget;
        Port = port;
    }

    public int Port { get; }
    public bool IsRunning => _listener.IsListening;

    public static Task<OpenOmsiWebSocketGateway> StartAsync(
        int port,
        IPEndPoint udpTarget,
        CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var gateway = new OpenOmsiWebSocketGateway(listener, udpTarget, port);
        gateway._acceptLoop = gateway.AcceptLoopAsync(gateway._cts.Token);
        cancellationToken.Register(() => gateway._cts.Cancel());
        return Task.FromResult(gateway);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync()
                    .WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (!_listener.IsListening)
            {
                break;
            }

            _ = Task.Run(
                () => HandleContextAsync(context, cancellationToken),
                CancellationToken.None);
        }
    }

    private async Task HandleContextAsync(
        HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(
                    context.Request.Url?.AbsolutePath,
                    "/status",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                var body = System.Text.Encoding.UTF8.GetBytes(
                    $"{{\"protocol\":{OpenOmsiLanProtocol.ProtocolVersion},\"transport\":\"websocket\"}}");
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body, cancellationToken);
                context.Response.Close();
                return;
            }

            if (!string.Equals(
                    context.Request.Url?.AbsolutePath,
                    "/ws",
                    StringComparison.OrdinalIgnoreCase) ||
                !context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var upgraded = await context.AcceptWebSocketAsync(null);
            await PumpGatewayAsync(
                upgraded.WebSocket,
                _udpTarget,
                cancellationToken);
        }
        catch
        {
            try { context.Response.Abort(); } catch { }
        }
    }

    private static async Task PumpGatewayAsync(
        WebSocket socket,
        IPEndPoint target,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var wsToUdp = Task.Run(
            () => PumpWebSocketToUdpAsync(socket, udp, target, linked.Token),
            CancellationToken.None);
        var udpToWs = Task.Run(
            () => PumpUdpToWebSocketAsync(udp, socket, linked.Token),
            CancellationToken.None);

        await Task.WhenAny(wsToUdp, udpToWs);
        linked.Cancel();

        try { await Task.WhenAll(wsToUdp, udpToWs); } catch { }
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "closed",
                    CancellationToken.None);
            }
        }
        catch { }
        socket.Dispose();
    }

    private static async Task PumpWebSocketToUdpAsync(
        WebSocket socket,
        UdpClient udp,
        IPEndPoint target,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[OpenOmsiLanProtocol.MaxDatagramBytes];
        while (!cancellationToken.IsCancellationRequested &&
               socket.State == WebSocketState.Open)
        {
            var count = 0;
            WebSocketReceiveResult? result;
            do
            {
                if (count >= buffer.Length)
                {
                    result = null;
                    break;
                }

                result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, count, buffer.Length - count),
                    cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                count += result.Count;
            }
            while (result is not null && !result.EndOfMessage);

            if (result is null ||
                result.MessageType != WebSocketMessageType.Binary ||
                count == 0 ||
                count > OpenOmsiLanProtocol.MaxDatagramBytes)
            {
                continue;
            }

            await udp.SendAsync(
                buffer.AsMemory(0, count),
                target,
                cancellationToken);
        }
    }

    private static async Task PumpUdpToWebSocketAsync(
        UdpClient udp,
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested &&
               socket.State == WebSocketState.Open)
        {
            var packet = await udp.ReceiveAsync(cancellationToken);
            if (packet.Buffer.Length == 0 ||
                packet.Buffer.Length > OpenOmsiLanProtocol.MaxDatagramBytes)
            {
                continue;
            }

            await socket.SendAsync(
                packet.Buffer,
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; } catch { }
        }

        _cts.Dispose();
    }
}

public sealed class OpenOmsiWebSocketClient : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly UdpClient _localUdp;
    private readonly CancellationTokenSource _cts = new();
    private readonly Uri _uri;
    private readonly object _sync = new();
    private IPEndPoint? _lastLocalSender;
    private Task? _pumpTask;

    private OpenOmsiWebSocketClient(
        ClientWebSocket webSocket,
        UdpClient localUdp,
        Uri uri)
    {
        _webSocket = webSocket;
        _localUdp = localUdp;
        _uri = uri;
    }

    public IPEndPoint LocalEndpoint =>
        (IPEndPoint)_localUdp.Client.LocalEndPoint!;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public static async Task<OpenOmsiWebSocketClient> ConnectAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        var url = OpenOmsiWebSocketBridge.NormalizeWebSocketUrl(target);
        var uri = new Uri(url, UriKind.Absolute);
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        if (uri.IsLoopback)
        {
            socket.Options.Proxy = null;
        }
        await socket.ConnectAsync(uri, cancellationToken);

        var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var client = new OpenOmsiWebSocketClient(socket, udp, uri);
        client._pumpTask = client.RunAsync(client._cts.Token);
        return client;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var udpToWs = Task.Run(
            () => PumpLocalUdpToWebSocketAsync(linked.Token),
            CancellationToken.None);
        var wsToUdp = Task.Run(
            () => PumpWebSocketToLocalUdpAsync(linked.Token),
            CancellationToken.None);

        await Task.WhenAny(udpToWs, wsToUdp);
        linked.Cancel();
        try { await Task.WhenAll(udpToWs, wsToUdp); } catch { }
    }

    private async Task PumpLocalUdpToWebSocketAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested &&
               _webSocket.State == WebSocketState.Open)
        {
            var packet = await _localUdp.ReceiveAsync(cancellationToken);
            if (packet.Buffer.Length == 0 ||
                packet.Buffer.Length > OpenOmsiLanProtocol.MaxDatagramBytes)
            {
                continue;
            }

            lock (_sync)
            {
                _lastLocalSender = packet.RemoteEndPoint;
            }

            await _webSocket.SendAsync(
                packet.Buffer,
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }
    }

    private async Task PumpWebSocketToLocalUdpAsync(
        CancellationToken cancellationToken)
    {
        var buffer = new byte[OpenOmsiLanProtocol.MaxDatagramBytes];
        while (!cancellationToken.IsCancellationRequested &&
               _webSocket.State == WebSocketState.Open)
        {
            var count = 0;
            WebSocketReceiveResult? result;
            do
            {
                if (count >= buffer.Length)
                {
                    result = null;
                    break;
                }

                result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, count, buffer.Length - count),
                    cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                count += result.Count;
            }
            while (result is not null && !result.EndOfMessage);

            IPEndPoint? destination;
            lock (_sync)
            {
                destination = _lastLocalSender;
            }

            if (destination is null ||
                result is null ||
                result.MessageType != WebSocketMessageType.Binary ||
                count == 0)
            {
                continue;
            }

            await _localUdp.SendAsync(
                buffer.AsMemory(0, count),
                destination,
                cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "closed",
                    CancellationToken.None);
            }
        }
        catch { }

        if (_pumpTask is not null)
        {
            try { await _pumpTask; } catch { }
        }

        _localUdp.Dispose();
        _webSocket.Dispose();
        _cts.Dispose();
    }
}
