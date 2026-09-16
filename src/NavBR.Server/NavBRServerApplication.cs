using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using NavBR.Server.Diagnostics;
using NavBR.Server.Hubs;
using NavBR.Server.Multiplayer;
using NavBR.Shared.Diagnostics;

namespace NavBR.Server;

public static class NavBRServerApplication
{
    public static WebApplication Build(string[]? args = null, string? listenUrl = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        if (!string.IsNullOrWhiteSpace(listenUrl))
        {
            builder.WebHost.UseUrls(listenUrl);
        }
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
        {
            builder.WebHost.UseUrls("http://0.0.0.0:5000");
        }

        builder.Services.AddSingleton<MultiplayerRoomRegistry>();
        builder.Services.AddSingleton<DiagnosticsIngestStore>();
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 64 * 1024;
            options.EnableDetailedErrors = false;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("diagnostics", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .SetIsOriginAllowed(_ => true)
                      .AllowCredentials());
        });

        var app = builder.Build();

        app.UseCors();
        app.UseRateLimiter();
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "NavBR.Server",
            multiplayer = "signalr",
            hosting = "peer-host",
            diagnostics = "available"
        }));

        app.MapPost(
                "/api/diagnostics",
                async (
                    HttpContext httpContext,
                    DiagnosticEventBatch batch,
                    DiagnosticsIngestStore store,
                    CancellationToken cancellationToken) =>
                {
                    if (httpContext.Request.ContentLength is > 131_072)
                    {
                        return Results.StatusCode(413);
                    }

                    if (!DiagnosticsIngestStore.TryValidate(batch, out var validationError))
                    {
                        return Results.BadRequest(new { error = validationError });
                    }

                    await store.AppendAsync(batch, cancellationToken);
                    return Results.Accepted();
                })
            .RequireRateLimiting("diagnostics");

        app.MapHub<MultiplayerHub>("/hubs/multiplayer");

        return app;
    }
}
