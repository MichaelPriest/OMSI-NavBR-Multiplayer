using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace NavBR.Client.Mobile;

internal sealed class MobileCompanionHostService : IAsyncDisposable
{
    public const int DefaultPort = 27731;
    private readonly Func<Task<object>> _stateProvider;
    private WebApplication? _app;

    public MobileCompanionHostService(Func<Task<object>> stateProvider, int port = DefaultPort)
    {
        _stateProvider = stateProvider;
        Port = port;
        PairingCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    }

    public int Port { get; }
    public string PairingCode { get; }
    public bool IsRunning => _app is not null;
    public IReadOnlyList<string> AccessUrls => ResolveAccessUrls();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null) return;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls($"http://0.0.0.0:{Port}");
        var app = builder.Build();

        var mobileRoot = Path.Combine(AppContext.BaseDirectory, "MobileUI", "dist");
        var mobileRootExists = Directory.Exists(mobileRoot);
        PhysicalFileProvider? provider = mobileRootExists ? new PhysicalFileProvider(mobileRoot) : null;

        if (provider is not null)
        {
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
        }

        app.MapGet("/health", (HttpContext context) =>
        {
            if (!IsLocalNetworkClient(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(new
            {
                status = "ok",
                service = "NavBR.MobileCompanion",
                port = Port,
                pairingRequired = true,
                uiAvailable = mobileRootExists
            });
        });

        app.MapGet("/api/mobile/state", async (HttpContext context) =>
        {
            if (!IsLocalNetworkClient(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!IsAuthorized(context))
            {
                return Results.Json(new { error = "pairing_required" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(await _stateProvider());
        });

        if (provider is not null)
        {
            app.MapFallback(async context =>
            {
                if (!IsLocalNetworkClient(context))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(mobileRoot, "index.html"));
            });
        }

        await app.StartAsync(cancellationToken);
        _app = app;
    }

    private bool IsAuthorized(HttpContext context)
    {
        var supplied = context.Request.Headers["X-NavBR-Mobile-Code"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(supplied))
        {
            supplied = context.Request.Query["pairing"].FirstOrDefault();
        }

        return string.Equals(supplied?.Trim(), PairingCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalNetworkClient(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null) return false;
        if (IPAddress.IsLoopback(address)) return true;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10 ||
                   b[0] == 127 ||
                   b[0] == 192 && b[1] == 168 ||
                   b[0] == 172 && b[1] >= 16 && b[1] <= 31;
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
               (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal);
    }

    private IReadOnlyList<string> ResolveAccessUrls()
    {
        var urls = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"http://127.0.0.1:{Port}"
        };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var item in nic.GetIPProperties().UnicastAddresses)
                {
                    var address = item.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork || !IsPrivateIpv4(address)) continue;
                    urls.Add($"http://{address}:{Port}");
                }
            }
        }
        catch { }

        return urls.ToArray();
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b.Length == 4 &&
               (b[0] == 10 ||
                b[0] == 192 && b[1] == 168 ||
                b[0] == 172 && b[1] >= 16 && b[1] <= 31);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;
        var app = _app;
        _app = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await app.StopAsync(cts.Token); }
        catch (OperationCanceledException) { }
        await app.DisposeAsync();
    }
}
