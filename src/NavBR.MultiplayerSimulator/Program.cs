using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

var options = SimulatorOptions.Parse(args);
if (options.ShowHelp)
{
    SimulatorOptions.PrintHelp();
    return;
}

Console.WriteLine($"NavBR Multiplayer Simulator");
Console.WriteLine($"Server : {options.ServerUrl}");
Console.WriteLine($"Room   : {options.RoomId}");
Console.WriteLine($"Players: {options.PlayerCount}");
Console.WriteLine($"Mode   : {options.Mode}");
Console.WriteLine($"Map    : {options.MapName ?? "(not forced)"}");
Console.WriteLine("Press Ctrl+C to stop.");

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

var bots = Enumerable.Range(1, options.PlayerCount)
    .Select(index => new SimulatedPlayer(index, options))
    .ToArray();

try
{
    await Task.WhenAll(bots.Select(bot => bot.ConnectAsync(shutdown.Token)));
    Console.WriteLine($"Connected {bots.Length} simulated players.");

    var started = DateTimeOffset.UtcNow;
    while (!shutdown.IsCancellationRequested &&
           (options.DurationSeconds <= 0 ||
            (DateTimeOffset.UtcNow - started).TotalSeconds < options.DurationSeconds))
    {
        var elapsed = (DateTimeOffset.UtcNow - started).TotalSeconds;
        await Task.WhenAll(bots.Select(bot => bot.PublishFrameAsync(elapsed, shutdown.Token)));
        await Task.Delay(options.IntervalMilliseconds, shutdown.Token);
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
}
finally
{
    await Task.WhenAll(bots.Select(bot => bot.DisposeAsync().AsTask()));
}

internal sealed class SimulatedPlayer : IAsyncDisposable
{
    private readonly int _index;
    private readonly SimulatorOptions _options;
    private readonly string _playerId;
    private readonly string _displayName;
    private readonly HubConnection _connection;
    private readonly double _phase;
    private bool _roleplayActive;

    public SimulatedPlayer(int index, SimulatorOptions options)
    {
        _index = index;
        _options = options;
        _playerId = $"sim-{index:00}-{Guid.NewGuid():N}"[..24];
        _displayName = $"{options.NamePrefix} {index:00}";
        _phase = index * Math.PI * 2d / Math.Max(1, options.PlayerCount);

        _connection = new HubConnectionBuilder()
            .WithUrl(NormalizeHubUrl(options.ServerUrl))
            .WithAutomaticReconnect()
            .Build();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _connection.StartAsync(cancellationToken);

        var manifest = new OmsiCompatibilityManifest(
            OmsiVersion: "simulator",
            NavBRVersion: "alpha.14-simulator",
            MapName: _options.MapName,
            MapCompatibilityId: _options.MapCompatibilityId,
            VehiclePath: _options.VehiclePath,
            VehicleCompatibilityId: _options.VehicleCompatibilityId,
            HofName: null,
            HofCompatibilityId: null,
            PluginProtocolVersion: 3,
            PluginDeployment: "simulator",
            Capabilities: ["telemetry", "roleplay-character"]);

        var request = new JoinRoomRequest(
            _options.RoomId,
            _playerId,
            _displayName,
            _options.MapName,
            _options.MapCompatibilityId,
            manifest);

        await _connection.InvokeAsync<RoomSnapshot>(
            "JoinRoom",
            request,
            cancellationToken);

        await _connection.SendAsync(
            "UpdateClientStatus",
            true,
            18 + _index * 3,
            cancellationToken);
    }

    public async Task PublishFrameAsync(double elapsedSeconds, CancellationToken cancellationToken)
    {
        var radius = _options.RadiusMeters + (_index % 4) * 8d;
        var angularSpeed = 0.035d + (_index % 3) * 0.008d;
        var angle = _phase + elapsedSeconds * angularSpeed;
        var x = _options.CenterX + Math.Cos(angle) * radius;
        var y = _options.CenterY + Math.Sin(angle) * radius;
        var z = _options.CenterZ;
        var heading = (angle * 180d / Math.PI + 90d) % 360d;

        var roleplayThisFrame =
            _options.Mode == SimulatorMode.Roleplay ||
            (_options.Mode == SimulatorMode.Mixed && _index % 3 == 0);

        if (roleplayThisFrame)
        {
            _roleplayActive = true;
            var segment = ((int)(elapsedSeconds / 6d) + _index) % 3;
            var activity = segment switch
            {
                0 => RoleplayCharacterActivity.Idle,
                1 => RoleplayCharacterActivity.Walking,
                _ => RoleplayCharacterActivity.Running
            };
            var speed = activity switch
            {
                RoleplayCharacterActivity.Idle => 0d,
                RoleplayCharacterActivity.Walking => 1.35d,
                _ => 3.7d
            };

            var character = new RoleplayCharacterState(
                _playerId,
                DateTimeOffset.UtcNow,
                _options.MapName,
                _options.MapCompatibilityId,
                x,
                y,
                z,
                heading,
                speed,
                activity,
                true,
                CharacterId: $"sim-character-{_index:00}",
                CharacterName: $"SIM Driver {_index:00}");

            await _connection.SendAsync(
                "PublishRoleplayCharacter",
                character,
                cancellationToken);
            return;
        }

        if (_roleplayActive)
        {
            await _connection.SendAsync("ReleaseRoleplayCharacter", cancellationToken);
            _roleplayActive = false;
        }

        var speedKph = 22d + (_index % 5) * 6d;
        var radians = heading * Math.PI / 180d;
        var half = radians / 2d;

        var telemetry = new VehicleTelemetry(
            PlayerId: _playerId,
            Timestamp: DateTimeOffset.UtcNow,
            MapName: _options.MapName,
            VehicleName: $"SIM Bus {_index:00}",
            Line: $"{100 + _index}",
            Route: "SIM",
            X: x,
            Y: y,
            Z: z,
            HeadingDegrees: heading,
            SpeedKph: speedKph,
            IsInGame: true,
            MapCompatibilityId: _options.MapCompatibilityId,
            VehiclePath: _options.VehiclePath,
            VehicleCompatibilityId: _options.VehicleCompatibilityId,
            LocalX: x,
            LocalY: y,
            LocalZ: z,
            RotationX: 0d,
            RotationY: 0d,
            RotationZ: Math.Sin(half),
            RotationW: Math.Cos(half));

        await _connection.SendAsync("PublishTelemetry", telemetry, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_roleplayActive && _connection.State == HubConnectionState.Connected)
            {
                await _connection.SendAsync("ReleaseRoleplayCharacter");
            }

            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("LeaveRoom");
            }
        }
        catch
        {
        }

        await _connection.DisposeAsync();
    }

    private static string NormalizeHubUrl(string serverUrl)
    {
        var value = serverUrl.Trim().TrimEnd('/');
        return value.EndsWith("/hubs/multiplayer", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{value}/hubs/multiplayer";
    }
}

internal enum SimulatorMode
{
    Vehicles,
    Roleplay,
    Mixed
}

internal sealed record SimulatorOptions(
    string ServerUrl,
    string RoomId,
    int PlayerCount,
    string? MapName,
    string? MapCompatibilityId,
    string? VehiclePath,
    string? VehicleCompatibilityId,
    double CenterX,
    double CenterY,
    double CenterZ,
    double RadiusMeters,
    int IntervalMilliseconds,
    int DurationSeconds,
    string NamePrefix,
    SimulatorMode Mode,
    bool ShowHelp)
{
    public static SimulatorOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (key is "-h" or "--help")
            {
                values["help"] = "true";
                continue;
            }

            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = key[2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            values[name] = value;
        }

        var mode = values.GetValueOrDefault("mode")?.ToLowerInvariant() switch
        {
            "rp" or "roleplay" => SimulatorMode.Roleplay,
            "mixed" => SimulatorMode.Mixed,
            _ => SimulatorMode.Vehicles
        };

        return new SimulatorOptions(
            ServerUrl: values.GetValueOrDefault("server") ?? "http://127.0.0.1:27730",
            RoomId: values.GetValueOrDefault("room") ?? "navbr-sim",
            PlayerCount: ClampInt(values.GetValueOrDefault("players"), 6, 1, 32),
            MapName: NullIfEmpty(values.GetValueOrDefault("map")),
            MapCompatibilityId: NullIfEmpty(values.GetValueOrDefault("map-id")),
            VehiclePath: NullIfEmpty(values.GetValueOrDefault("vehicle-path")),
            VehicleCompatibilityId: NullIfEmpty(values.GetValueOrDefault("vehicle-id")),
            CenterX: ParseDouble(values.GetValueOrDefault("x"), 0d),
            CenterY: ParseDouble(values.GetValueOrDefault("y"), 0d),
            CenterZ: ParseDouble(values.GetValueOrDefault("z"), 0d),
            RadiusMeters: Math.Clamp(ParseDouble(values.GetValueOrDefault("radius"), 90d), 10d, 2000d),
            IntervalMilliseconds: ClampInt(values.GetValueOrDefault("interval"), 250, 100, 5000),
            DurationSeconds: Math.Max(0, ClampInt(values.GetValueOrDefault("duration"), 0, 0, 86400)),
            NamePrefix: NullIfEmpty(values.GetValueOrDefault("prefix")) ?? "SIM",
            Mode: mode,
            ShowHelp: values.ContainsKey("help"));
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
NavBR Multiplayer Simulator

Usage:
  dotnet run --project src/NavBR.MultiplayerSimulator -- [options]

Options:
  --server URL         Host NavBR (default http://127.0.0.1:27730)
  --room ID            Room id (default navbr-sim)
  --players N          Simulated players, 1..32 (default 6)
  --mode vehicles|rp|mixed
  --map NAME           OMSI map name. Use the real loaded map name to test HUD/minimap compatibility.
  --map-id ID          Optional real map compatibility id.
  --x N --y N --z N   Movement center in OMSI local coordinates.
  --radius N           Movement radius in meters (default 90)
  --interval MS        Publish interval, 100..5000 (default 250)
  --duration SEC       0 = until Ctrl+C
  --prefix TEXT        Display-name prefix (default SIM)
  --vehicle-path PATH  Optional real .bus path for physical-vehicle testing.
  --vehicle-id ID      Optional real vehicle compatibility id.
""");
    }

    private static int ClampInt(string? value, int fallback, int min, int max) =>
        int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
