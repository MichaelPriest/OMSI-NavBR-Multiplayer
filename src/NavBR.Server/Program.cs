using NavBR.Server.Hubs;
using NavBR.Server.Multiplayer;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000");
}

builder.Services.AddSingleton<MultiplayerRoomRegistry>();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 64 * 1024;
    options.EnableDetailedErrors = false;
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
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "NavBR.Server",
    multiplayer = "signalr"
}));
app.MapHub<MultiplayerHub>("/hubs/multiplayer");

app.Run();
