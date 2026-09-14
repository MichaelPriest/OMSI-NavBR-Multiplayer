using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using NavBR.Server;

namespace NavBR.Client.Multiplayer;

public sealed class RoomHostService : IAsyncDisposable
{
    private WebApplication? _app;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; }

    public string LocalServerUrl => $"http://127.0.0.1:{Port}";

    public async Task StartAsync(int port = 27730, CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        if (port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var app = NavBRServerApplication.Build(
            listenUrl: $"http://0.0.0.0:{port}");

        await app.StartAsync(cancellationToken);
        _app = app;
        Port = port;
    }

    public IReadOnlyList<string> GetLanJoinUrls()
    {
        if (!IsRunning)
        {
            return Array.Empty<string>();
        }

        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => $"http://{address}:{Port}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        _app = null;
        Port = 0;

        if (app is null)
        {
            return;
        }

        try
        {
            await app.StopAsync(cancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
