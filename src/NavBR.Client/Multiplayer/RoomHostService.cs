using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using NavBR.Server;

namespace NavBR.Client.Multiplayer;

public sealed class RoomHostService : IAsyncDisposable
{
    private readonly UpnpPortMappingService _upnp = new();
    private WebApplication? _app;
    private UpnpGatewayInfo? _mappedGateway;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; }
    internal UpnpMappingResult? LastUpnpResult { get; private set; }

    public string LocalServerUrl => $"http://127.0.0.1:{Port}";

    public async Task StartAsync(
        int port = 27730,
        bool? enableAutomaticUpnp = null,
        CancellationToken cancellationToken = default)
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

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await app.DisposeAsync();
            throw MultiplayerNetworkErrorClassifier.WrapHost(ex, port);
        }

        _app = app;
        Port = port;
        LastUpnpResult = null;
        _mappedGateway = null;

        var useUpnp = enableAutomaticUpnp ?? MultiplayerSettingsStore.Load().EnableAutomaticUpnp;
        if (!useUpnp)
        {
            return;
        }

        try
        {
            LastUpnpResult = await _upnp.TryAddMappingAsync(port, cancellationToken);
            if (LastUpnpResult.Success)
            {
                _mappedGateway = LastUpnpResult.Gateway;
            }
        }
        catch (Exception ex)
        {
            LastUpnpResult = new UpnpMappingResult(
                false,
                MultiplayerNetworkErrorCode.UpnpMappingFailed,
                NetworkErrorText.Describe(MultiplayerNetworkErrorCode.UpnpMappingFailed, ex.Message));
        }
    }

    public IReadOnlyList<string> GetLanJoinUrls()
    {
        if (!IsRunning)
        {
            return Array.Empty<string>();
        }

        try
        {
            var values = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => $"http://{address}:{Port}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var internet = GetInternetInviteAddress();
            if (!string.IsNullOrWhiteSpace(internet) &&
                !values.Contains(internet, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(internet);
            }

            return values;
        }
        catch
        {
            var internet = GetInternetInviteAddress();
            return string.IsNullOrWhiteSpace(internet) ? Array.Empty<string>() : [internet];
        }
    }

    public string? GetInternetInviteAddress()
    {
        if (!IsRunning || LastUpnpResult?.Success != true)
        {
            return null;
        }

        var external = LastUpnpResult.Gateway?.ExternalAddress;
        return string.IsNullOrWhiteSpace(external)
            ? null
            : $"http://{external}:{Port}";
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        var port = Port;
        var mappedGateway = _mappedGateway;

        _app = null;
        Port = 0;
        _mappedGateway = null;

        if (mappedGateway is not null && port > 0)
        {
            try
            {
                await _upnp.TryDeleteMappingAsync(mappedGateway, port, cancellationToken);
            }
            catch
            {
                // UPnP cleanup is best-effort. Routers commonly remove leases on their own.
            }
        }

        if (app is null)
        {
            LastUpnpResult = null;
            return;
        }

        try
        {
            await app.StopAsync(cancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
            LastUpnpResult = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
