using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using NavBR.Server;

namespace NavBR.Client.Multiplayer;

public sealed class RoomHostService : IAsyncDisposable
{
    private readonly UpnpPortMappingService _upnp = new();
    private WebApplication? _app;
    private UpnpGatewayInfo? _mappedGateway;
    private CancellationTokenSource? _upnpCts;
    private IReadOnlyList<string> _lanJoinUrls = Array.Empty<string>();

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
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw MultiplayerNetworkErrorClassifier.WrapHost(ex, port);
        }

        _app = app;
        Port = port;
        LastUpnpResult = null;
        _mappedGateway = null;
        _lanJoinUrls = BuildLanJoinUrls(port);

        var useUpnp = enableAutomaticUpnp ?? MultiplayerSettingsStore.Load().EnableAutomaticUpnp;
        if (!useUpnp)
        {
            return;
        }

        _upnpCts?.Cancel();
        _upnpCts?.Dispose();
        _upnpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _upnpCts.CancelAfter(TimeSpan.FromSeconds(8));

        // Do not block room creation while a router is being discovered/configured.
        // The UI can connect to the local server immediately and reports UPnP as
        // "checking" until this background task finishes.
        _ = ConfigureUpnpInBackgroundAsync(app, port, _upnpCts.Token);
    }

    public IReadOnlyList<string> GetLanJoinUrls()
    {
        if (!IsRunning)
        {
            return Array.Empty<string>();
        }

        var values = _lanJoinUrls.ToList();
        var internet = GetInternetInviteAddress();
        if (!string.IsNullOrWhiteSpace(internet) &&
            !values.Contains(internet, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(internet);
        }

        return values;
    }

    private static IReadOnlyList<string> BuildLanJoinUrls(int port)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                .Select(entry => entry.Address)
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => $"http://{address}:{port}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private async Task ConfigureUpnpInBackgroundAsync(
        WebApplication ownerApp,
        int port,
        CancellationToken cancellationToken)
    {
        UpnpMappingResult result;
        try
        {
            result = await _upnp.TryAddMappingAsync(port, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new UpnpMappingResult(
                false,
                MultiplayerNetworkErrorCode.UpnpUnavailable,
                "UPnP não respondeu dentro do limite de 8 segundos. O servidor local continua ativo.");
        }
        catch (Exception ex)
        {
            result = new UpnpMappingResult(
                false,
                MultiplayerNetworkErrorCode.UpnpMappingFailed,
                NetworkErrorText.Describe(MultiplayerNetworkErrorCode.UpnpMappingFailed, ex.Message));
        }

        if (!ReferenceEquals(_app, ownerApp) || Port != port)
        {
            if (result.Success && result.Gateway is not null)
            {
                try
                {
                    await _upnp.TryDeleteMappingAsync(result.Gateway, port).ConfigureAwait(false);
                }
                catch
                {
                    // Host stopped while discovery was finishing; cleanup is best-effort.
                }
            }
            return;
        }

        LastUpnpResult = result;
        if (result.Success)
        {
            _mappedGateway = result.Gateway;
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
        var upnpCts = _upnpCts;

        _app = null;
        Port = 0;
        _mappedGateway = null;
        _lanJoinUrls = Array.Empty<string>();
        _upnpCts = null;
        upnpCts?.Cancel();
        upnpCts?.Dispose();

        if (mappedGateway is not null && port > 0)
        {
            try
            {
                await _upnp.TryDeleteMappingAsync(mappedGateway, port, cancellationToken).ConfigureAwait(false);
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
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
            LastUpnpResult = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
