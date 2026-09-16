using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NavBR.Client.Multiplayer;

internal enum NatEnvironmentKind
{
    Unknown,
    PublicWan,
    CarrierGradeNat,
    PrivateWan,
    ReservedWan
}

internal sealed record NatDiagnosticsSnapshot(
    IReadOnlyList<string> LocalIpv4Addresses,
    bool LocalPortListening,
    bool FirewallRulePresent,
    bool AutomaticUpnpEnabled,
    bool UpnpGatewayFound,
    string? GatewayLocalAddress,
    string? GatewayExternalAddress,
    NatEnvironmentKind EnvironmentKind,
    bool ExternalPortVerified,
    string TechnicalNote);

internal sealed class NatDiagnosticsService
{
    public const int HostPort = 27730;

    private readonly UpnpPortMappingService _upnp = new();

    public async Task<NatDiagnosticsSnapshot> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var localAddresses = GetLocalIpv4Addresses();
        var listening = IsTcpPortListening(HostPort);
        var firewall = await WindowsFirewallService.IsInboundRulePresentAsync(HostPort);
        var automaticUpnp = MultiplayerSettingsStore.Load().EnableAutomaticUpnp;

        UpnpGatewayInfo? gateway = null;
        try
        {
            gateway = await _upnp.DiscoverAsync(cancellationToken);
        }
        catch
        {
            // UPnP discovery is optional. A router that does not answer must not break diagnostics.
        }

        var external = NormalizeAddress(gateway?.ExternalAddress);
        if (gateway is not null && string.IsNullOrWhiteSpace(external))
        {
            try
            {
                external = NormalizeAddress(await _upnp.TryGetExternalAddressAsync(gateway, cancellationToken));
            }
            catch
            {
            }
        }

        var kind = ClassifyExternalAddress(external);
        var note = BuildTechnicalNote(kind, gateway is not null, external);

        return new NatDiagnosticsSnapshot(
            LocalIpv4Addresses: localAddresses,
            LocalPortListening: listening,
            FirewallRulePresent: firewall,
            AutomaticUpnpEnabled: automaticUpnp,
            UpnpGatewayFound: gateway is not null,
            GatewayLocalAddress: gateway?.LocalAddress.ToString(),
            GatewayExternalAddress: external,
            EnvironmentKind: kind,
            ExternalPortVerified: false,
            TechnicalNote: note);
    }

    internal static NatEnvironmentKind ClassifyExternalAddress(string? value)
    {
        if (!IPAddress.TryParse(value, out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork)
        {
            return NatEnvironmentKind.Unknown;
        }

        var bytes = address.GetAddressBytes();

        if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
        {
            return NatEnvironmentKind.CarrierGradeNat;
        }

        if (IsPrivateIpv4(bytes) ||
            bytes[0] == 127 ||
            (bytes[0] == 169 && bytes[1] == 254))
        {
            return NatEnvironmentKind.PrivateWan;
        }

        if (IsReservedOrDocumentationIpv4(bytes))
        {
            return NatEnvironmentKind.ReservedWan;
        }

        return NatEnvironmentKind.PublicWan;
    }

    private static IReadOnlyList<string> GetLocalIpv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                                  network.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  network.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Select(address => address.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork &&
                                  !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsTcpPortListening(int port)
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeAddress(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized == "0.0.0.0")
        {
            return null;
        }
        return normalized;
    }

    private static bool IsPrivateIpv4(byte[] bytes) =>
        bytes[0] == 10 ||
        (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
        (bytes[0] == 192 && bytes[1] == 168);

    private static bool IsReservedOrDocumentationIpv4(byte[] bytes) =>
        bytes[0] == 0 ||
        bytes[0] >= 224 ||
        (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
        (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
        (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) ||
        (bytes[0] == 198 && bytes[1] is 18 or 19);

    private static string BuildTechnicalNote(
        NatEnvironmentKind kind,
        bool gatewayFound,
        string? external)
    {
        if (!gatewayFound)
        {
            return "UPnP gateway was not discovered. This does not prove that the Internet connection is unavailable.";
        }

        return kind switch
        {
            NatEnvironmentKind.PublicWan =>
                $"Gateway reports public IPv4 {external}. External TCP {HostPort} still requires an outside callback test before it can be considered reachable.",
            NatEnvironmentKind.CarrierGradeNat =>
                $"Gateway reports {external}, inside 100.64.0.0/10. CGNAT is likely and direct inbound hosting may require ISP support or a future relay/fallback.",
            NatEnvironmentKind.PrivateWan =>
                $"Gateway reports private IPv4 {external}. Double NAT or another upstream router is likely.",
            NatEnvironmentKind.ReservedWan =>
                $"Gateway reports non-public/reserved IPv4 {external}. Direct Internet reachability cannot be assumed.",
            _ =>
                "Gateway was found, but it did not provide a usable public IPv4 address."
        };
    }
}
