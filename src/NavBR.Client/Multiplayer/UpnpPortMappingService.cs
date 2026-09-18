using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace NavBR.Client.Multiplayer;

internal sealed record UpnpGatewayInfo(
    Uri DescriptionUri,
    Uri ControlUri,
    string ServiceType,
    IPAddress LocalAddress,
    string? ExternalAddress = null);

internal sealed record UpnpMappingResult(
    bool Success,
    MultiplayerNetworkErrorCode ErrorCode,
    string Message,
    UpnpGatewayInfo? Gateway = null);

internal sealed class UpnpPortMappingService
{
    private static readonly IPEndPoint SsdpEndpoint = new(IPAddress.Parse("239.255.255.250"), 1900);
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    public async Task<UpnpGatewayInfo?> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var locations = await DiscoverLocationsAsync(cancellationToken);
        foreach (var location in locations)
        {
            try
            {
                var gateway = await ResolveGatewayAsync(location, cancellationToken);
                if (gateway is not null)
                {
                    return gateway;
                }
            }
            catch
            {
                // Routers frequently publish stale SSDP endpoints; keep trying the remaining responses.
            }
        }
        return null;
    }

    public async Task<UpnpMappingResult> TryAddMappingAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        if (port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var gateway = await DiscoverAsync(cancellationToken);
        if (gateway is null)
        {
            return new UpnpMappingResult(
                false,
                MultiplayerNetworkErrorCode.UpnpUnavailable,
                NetworkErrorText.Describe(MultiplayerNetworkErrorCode.UpnpUnavailable));
        }

        try
        {
            var body = $"""
                <u:AddPortMapping xmlns:u="{gateway.ServiceType}">
                  <NewRemoteHost></NewRemoteHost>
                  <NewExternalPort>{port}</NewExternalPort>
                  <NewProtocol>TCP</NewProtocol>
                  <NewInternalPort>{port}</NewInternalPort>
                  <NewInternalClient>{gateway.LocalAddress}</NewInternalClient>
                  <NewEnabled>1</NewEnabled>
                  <NewPortMappingDescription>OMSI NavBR Multiplayer</NewPortMappingDescription>
                  <NewLeaseDuration>0</NewLeaseDuration>
                </u:AddPortMapping>
                """;

            await SendSoapAsync(gateway, "AddPortMapping", body, cancellationToken);
            var external = await TryGetExternalAddressAsync(gateway, cancellationToken);
            return new UpnpMappingResult(
                true,
                MultiplayerNetworkErrorCode.Unknown,
                string.IsNullOrWhiteSpace(external)
                    ? $"TCP {port} mapped by UPnP."
                    : $"{external}:{port}",
                gateway with { ExternalAddress = external });
        }
        catch (Exception ex)
        {
            return new UpnpMappingResult(
                false,
                MultiplayerNetworkErrorCode.UpnpMappingFailed,
                NetworkErrorText.Describe(MultiplayerNetworkErrorCode.UpnpMappingFailed, ex.Message),
                gateway);
        }
    }

    public async Task<bool> TryDeleteMappingAsync(
        UpnpGatewayInfo gateway,
        int port,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = $"""
                <u:DeletePortMapping xmlns:u="{gateway.ServiceType}">
                  <NewRemoteHost></NewRemoteHost>
                  <NewExternalPort>{port}</NewExternalPort>
                  <NewProtocol>TCP</NewProtocol>
                </u:DeletePortMapping>
                """;
            await SendSoapAsync(gateway, "DeletePortMapping", body, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> TryGetExternalAddressAsync(
        UpnpGatewayInfo gateway,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var body = $"""
                <u:GetExternalIPAddress xmlns:u="{gateway.ServiceType}"></u:GetExternalIPAddress>
                """;
            var response = await SendSoapAsync(gateway, "GetExternalIPAddress", body, cancellationToken);
            var document = XDocument.Parse(response);
            return document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "NewExternalIPAddress")?
                .Value
                .Trim();
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<Uri>> DiscoverLocationsAsync(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        var request = string.Join("\r\n",
            "M-SEARCH * HTTP/1.1",
            "HOST: 239.255.255.250:1900",
            "MAN: \"ssdp:discover\"",
            "MX: 2",
            "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1",
            string.Empty,
            string.Empty);
        var payload = Encoding.ASCII.GetBytes(request);
        await udp.SendAsync(payload, SsdpEndpoint, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var locations = new HashSet<Uri>();

        while (!timeout.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(timeout.Token);
                var text = Encoding.UTF8.GetString(result.Buffer);
                foreach (var line in text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
                {
                    var separator = line.IndexOf(':');
                    if (separator <= 0 || !line[..separator].Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (Uri.TryCreate(line[(separator + 1)..].Trim(), UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        locations.Add(uri);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
        }

        return locations.ToArray();
    }

    private async Task<UpnpGatewayInfo?> ResolveGatewayAsync(Uri descriptionUri, CancellationToken cancellationToken)
    {
        var xml = await _http.GetStringAsync(descriptionUri, cancellationToken);
        var document = XDocument.Parse(xml);

        foreach (var service in document.Descendants().Where(element => element.Name.LocalName == "service"))
        {
            var serviceType = service.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "serviceType")?
                .Value
                .Trim();
            if (string.IsNullOrWhiteSpace(serviceType) ||
                (!serviceType.Contains("WANIPConnection", StringComparison.OrdinalIgnoreCase) &&
                 !serviceType.Contains("WANPPPConnection", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var controlValue = service.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "controlURL")?
                .Value
                .Trim();
            if (string.IsNullOrWhiteSpace(controlValue))
            {
                continue;
            }

            var controlUri = Uri.TryCreate(controlValue, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(descriptionUri, controlValue);
            var localAddress = ResolveLocalAddress(controlUri);
            if (localAddress is null)
            {
                continue;
            }

            var gateway = new UpnpGatewayInfo(
                descriptionUri,
                controlUri,
                serviceType,
                localAddress);
            var external = await TryGetExternalAddressAsync(gateway, cancellationToken);
            return gateway with { ExternalAddress = external };
        }

        return null;
    }

    private async Task<string> SendSoapAsync(
        UpnpGatewayInfo gateway,
        string action,
        string actionBody,
        CancellationToken cancellationToken)
    {
        var envelope = $"""
            <?xml version="1.0"?>
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
              <s:Body>{actionBody}</s:Body>
            </s:Envelope>
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, gateway.ControlUri)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{gateway.ServiceType}#{action}\"");

        using var response = await _http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"UPnP SOAP {action} failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }
        return responseBody;
    }

    private static IPAddress? ResolveLocalAddress(Uri controlUri)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(controlUri.Host)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();
            foreach (var address in addresses)
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(new IPEndPoint(address, controlUri.Port > 0 ? controlUri.Port : 80));
                if (socket.LocalEndPoint is IPEndPoint local && !IPAddress.IsLoopback(local.Address))
                {
                    return local.Address;
                }
            }
        }
        catch
        {
            // UPnP is optional. Discovery failure must not prevent local hosting.
        }
        return null;
    }
}
