using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NavBR.Shared.Multiplayer;

namespace NavBR.Server.Multiplayer;

internal sealed class ExternalPortProbeService
{
    public const int ProbePort = 27730;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task<ExternalPortProbeResult> ProbeAsync(
        IPAddress? remoteAddress,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (remoteAddress is null)
        {
            return Result(false, "missing_remote_address", checkedAt, 0);
        }

        var normalized = Normalize(remoteAddress);
        if (normalized is null || !IsPublicAddress(normalized))
        {
            return Result(false, "source_not_public", checkedAt, 0);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient(normalized.AddressFamily);
            await client.ConnectAsync(normalized, ProbePort, timeout.Token);
            stopwatch.Stop();
            return Result(true, "reachable", checkedAt, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return Result(false, "timeout", checkedAt, stopwatch.ElapsedMilliseconds);
        }
        catch (SocketException)
        {
            stopwatch.Stop();
            return Result(false, "unreachable", checkedAt, stopwatch.ElapsedMilliseconds);
        }
        catch
        {
            stopwatch.Stop();
            return Result(false, "probe_failed", checkedAt, stopwatch.ElapsedMilliseconds);
        }
    }

    private static ExternalPortProbeResult Result(
        bool reachable,
        string status,
        DateTimeOffset checkedAt,
        long durationMilliseconds) =>
        new(
            Reachable: reachable,
            Port: ProbePort,
            Status: status,
            CheckedAtUtc: checkedAt,
            DurationMilliseconds: Math.Max(0, durationMilliseconds));

    private static IPAddress? Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6
            ? address
            : null;
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return !(
                bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                bytes[0] >= 224 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113));
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal ||
                     address.IsIPv6Multicast ||
                     address.IsIPv6SiteLocal ||
                     address.IsIPv6UniqueLocal);
        }

        return false;
    }
}
