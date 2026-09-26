using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;

namespace NavBR.Client.Mobile;

internal sealed class MobileCompanionHostService : IAsyncDisposable
{
    public const int DefaultPort = 27731;
    public const int DiscoveryPort = 27732;
    private const string DiscoveryRequest = "NAVBR_DISCOVER_V1";

    private readonly Func<Task<object>> _stateProvider;
    private readonly Func<MobileCompanionCommand, Task<object>> _commandHandler;
    private WebApplication? _app;
    private CancellationTokenSource? _discoveryCts;
    private Task? _discoveryTask;

    public MobileCompanionHostService(
        Func<Task<object>> stateProvider,
        Func<MobileCompanionCommand, Task<object>> commandHandler,
        int port = DefaultPort)
    {
        _stateProvider = stateProvider;
        _commandHandler = commandHandler;
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
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("mobile-companion", policy =>
                policy.WithOrigins(
                        "http://localhost",
                        "https://localhost",
                        "capacitor://localhost")
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });
        var app = builder.Build();
        app.UseCors("mobile-companion");

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

        app.MapPost("/api/mobile/command", async (HttpContext context) =>
        {
            if (!IsLocalNetworkClient(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!IsAuthorized(context))
            {
                return Results.Json(new { error = "pairing_required" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (context.Request.ContentLength is > 4096)
            {
                return Results.Json(
                    new { error = "command_too_large" },
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            MobileCompanionCommand? command;
            try
            {
                command = await context.Request.ReadFromJsonAsync<MobileCompanionCommand>();
            }
            catch
            {
                command = null;
            }

            if (command is null || string.IsNullOrWhiteSpace(command.Action))
            {
                return Results.Json(
                    new { error = "invalid_command" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(await _commandHandler(command));
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

        _discoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _discoveryTask = RunDiscoveryResponderAsync(_discoveryCts.Token);
    }

    private async Task RunDiscoveryResponderAsync(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.EnableBroadcast = true;
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(250, cancellationToken);
                }

                continue;
            }

            var remote = received.RemoteEndPoint.Address;
            if (remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }

            if (remote.AddressFamily != AddressFamily.InterNetwork ||
                !IsPrivateOrLoopbackIpv4(remote))
            {
                continue;
            }

            var request = Encoding.ASCII.GetString(received.Buffer).Trim();
            if (!string.Equals(request, DiscoveryRequest, StringComparison.Ordinal))
            {
                continue;
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                service = "NavBR.MobileCompanion",
                version = 1,
                httpPort = Port,
                pairingCode = PairingCode
            });

            try
            {
                await udp.SendAsync(payload, received.RemoteEndPoint, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Discovery is best-effort. Manual IP/code pairing remains available.
            }
        }
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

    private static bool IsPrivateOrLoopbackIpv4(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        return IsPrivateIpv4(address);
    }

    public async ValueTask DisposeAsync()
    {
        _discoveryCts?.Cancel();
        if (_discoveryTask is not null)
        {
            try
            {
                await _discoveryTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _discoveryCts?.Dispose();
        _discoveryCts = null;
        _discoveryTask = null;

        if (_app is null) return;
        var app = _app;
        _app = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await app.StopAsync(cts.Token); }
        catch (OperationCanceledException) { }
        await app.DisposeAsync();
    }
}

internal sealed record MobileCompanionCommand(
    string Action,
    bool? Enabled = null,
    bool? Active = null,
    string? Channel = null,
    double? ProximityMeters = null,
    bool? Deafened = null,
    string? PlayerId = null,
    bool? Muted = null,
    double? Gain = null,
    string? TriggerName = null,
    int? Theme = null,
    int? Size = null,
    bool? AutoDirection = null,
    string? Line = null,
    string? Direction = null);
