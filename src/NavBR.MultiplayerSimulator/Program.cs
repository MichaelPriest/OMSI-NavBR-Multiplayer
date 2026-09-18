using System.Diagnostics;
using System.Net.Http;
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

await using var localServer = await LocalServerBootstrap.EnsureAsync(options, shutdown.Token);
if (!localServer.Ready)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("NavBR Simulator: servidor indisponível.");
    Console.Error.WriteLine(localServer.ErrorMessage);
    Console.Error.WriteLine("Crie uma sala no NavBR, inicie o servidor dedicado ou use a build do simulador que inclui a pasta 'server'.");
    Environment.ExitCode = 3;
    return;
}

if (localServer.StartedServer)
{
    Console.WriteLine($"Local NavBR.Server started automatically at {localServer.ServerUrl}.");
    Console.WriteLine($"No app: Central Multiplayer > Sala > Entrar em sala > servidor {localServer.ServerUrl} > sala {options.RoomId}.");
}

Console.WriteLine(
    string.IsNullOrWhiteSpace(options.MapName)
        ? "Map    : aguardando mapa real da sala..."
        : $"Map    : {options.MapName} (fallback; a sala real tem prioridade)");

var synchronized = await RoomSimulationContextResolver.ResolveAsync(options, shutdown.Token);
if (!synchronized.Success || synchronized.Options is null)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("NavBR Simulator: não foi possível sincronizar o mapa da sala.");
    Console.Error.WriteLine(synchronized.ErrorMessage);
    Console.Error.WriteLine("Entre na sala pelo NavBR com o OMSI carregado no mapa e execute o simulador novamente.");
    Environment.ExitCode = 5;
    return;
}

options = synchronized.Options;
Console.WriteLine(
    synchronized.InheritedFromRoom
        ? $"Map    : {options.MapName} (sincronizado com a sala)"
        : $"Map    : {options.MapName} (fallback explícito; nenhuma sala real disponível)");

if (!string.IsNullOrWhiteSpace(options.MapCompatibilityId))
{
    Console.WriteLine($"Map ID : {options.MapCompatibilityId}");
}

if (!string.IsNullOrWhiteSpace(options.ActiveLine) ||
    !string.IsNullOrWhiteSpace(options.ActiveRoute))
{
    Console.WriteLine(
        $"Route  : linha {options.ActiveLine ?? "—"} • rota {options.ActiveRoute ?? "—"} • destino {options.ActiveDestination ?? "—"}");
}

Console.WriteLine(
    $"Seed   : X={options.CenterX:F1} Y={options.CenterY:F1} Z={options.CenterZ:F1} • raio {options.RadiusMeters:F0} m" +
    (options.GridX is int gx && options.GridY is int gy
        ? $" • grid {gx},{gy}" +
          (options.TileX is double tx && options.TileY is double ty
              ? $" • tile {tx:F1},{ty:F1}"
              : string.Empty)
        : string.Empty));

var bots = Enumerable.Range(1, options.PlayerCount)
    .Select(index => new SimulatedPlayer(index, options))
    .ToArray();
var probe = options.Verify ? new SimulationProbe(options) : null;
var connected = false;

try
{
    if (probe is not null)
    {
        await probe.ConnectAsync(shutdown.Token);
    }

    await Task.WhenAll(bots.Select(bot => bot.ConnectAsync(shutdown.Token)));
    connected = true;
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
catch (HttpRequestException ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"NavBR Simulator: não foi possível conectar em {options.ServerUrl}.");
    Console.Error.WriteLine($"Detalhe: {ex.Message}");
    Console.Error.WriteLine("Confirme que o host/sala está ativo e que a porta configurada está correta.");
    Environment.ExitCode = 3;
}
finally
{
    await Task.WhenAll(bots.Select(bot => bot.DisposeAsync().AsTask()));

    if (probe is not null)
    {
        if (connected)
        {
            var result = probe.Verify(bots.Select(bot => bot.PlayerId).ToArray());
            if (!result.Success)
            {
                Console.Error.WriteLine($"Movement verification failed: {result.Message}");
                Environment.ExitCode = 2;
            }
            else
            {
                Console.WriteLine($"Movement verification passed: {result.Message}");
            }
        }

        await probe.DisposeAsync();
    }
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
    private double _roleplayX;
    private double _roleplayY;
    private double _roleplayHeading;
    private double _lastElapsedSeconds;

    public string PlayerId => _playerId;

    public SimulatedPlayer(int index, SimulatorOptions options)
    {
        _index = index;
        _options = options;
        _playerId = $"sim-{index:00}-{Guid.NewGuid():N}"[..24];
        _displayName = $"{options.NamePrefix} {index:00}";
        _phase = index * Math.PI * 2d / Math.Max(1, options.PlayerCount);
        var initialRadius = Math.Max(4d, options.RadiusMeters * 0.45d);
        _roleplayX = options.CenterX + Math.Cos(_phase) * initialRadius;
        _roleplayY = options.CenterY + Math.Sin(_phase) * initialRadius;
        _roleplayHeading = (_phase * 180d / Math.PI + 90d) % 360d;

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
            manifest,
            _options.RoomPassword);

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
        var radius = Math.Max(
            4d,
            _options.RadiusMeters * (0.55d + (_index % 4) * 0.12d));
        var angularSpeed = 0.035d + (_index % 3) * 0.008d;
        var angle = _phase + elapsedSeconds * angularSpeed;
        var x = _options.CenterX + Math.Cos(angle) * radius;
        var y = _options.CenterY + Math.Sin(angle) * radius;
        var z = _options.CenterZ;
        var heading = (angle * 180d / Math.PI + 90d) % 360d;

        var roleplayThisFrame =
            _options.Mode == SimulatorMode.Roleplay ||
            (_options.Mode == SimulatorMode.Mixed && _index % 2 == 0);

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

            var deltaSeconds = Math.Clamp(elapsedSeconds - _lastElapsedSeconds, 0d, 0.5d);
            _lastElapsedSeconds = elapsedSeconds;
            _roleplayHeading = (_roleplayHeading + (8d + _index) * deltaSeconds) % 360d;
            if (speed > 0d)
            {
                var rpRadians = _roleplayHeading * Math.PI / 180d;
                _roleplayX += Math.Sin(rpRadians) * speed * deltaSeconds;
                _roleplayY += Math.Cos(rpRadians) * speed * deltaSeconds;
            }

            var character = new RoleplayCharacterState(
                _playerId,
                DateTimeOffset.UtcNow,
                _options.MapName,
                _options.MapCompatibilityId,
                _roleplayX,
                _roleplayY,
                z,
                _roleplayHeading,
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
            Line: _options.ActiveLine,
            Route: _options.ActiveRoute,
            X: x,
            Y: y,
            Z: z,
            HeadingDegrees: heading,
            SpeedKph: speedKph,
            IsInGame: true,
            GridX: _options.GridX,
            GridY: _options.GridY,
            TileX: _options.TileX is double baseTileX ? baseTileX + (x - _options.CenterX) : null,
            TileY: _options.TileY is double baseTileY ? baseTileY + (y - _options.CenterY) : null,
            MapCompatibilityId: _options.MapCompatibilityId,
            NextStopName: _options.ActiveNextStop,
            DestinationName: _options.ActiveDestination,
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

internal sealed class SimulationProbe : IAsyncDisposable
{
    private readonly SimulatorOptions _options;
    private readonly HubConnection _connection;
    private readonly Dictionary<string, MovementSample> _samples =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly string _playerId = $"sim-probe-{Guid.NewGuid():N}"[..28];

    public SimulationProbe(SimulatorOptions options)
    {
        _options = options;
        _connection = new HubConnectionBuilder()
            .WithUrl(NormalizeHubUrl(options.ServerUrl))
            .Build();

        _connection.On<PlayerTelemetryFrame>("telemetry", frame =>
            Record(
                frame.Player.PlayerId,
                frame.Telemetry.LocalX ?? frame.Telemetry.X,
                frame.Telemetry.LocalY ?? frame.Telemetry.Y,
                frame.Telemetry.HeadingDegrees,
                frame.Telemetry.MapName,
                MovementKind.Vehicle,
                null));
        _connection.On<RoleplayCharacterFrame>("roleplayCharacter", frame =>
            Record(
                frame.Player.PlayerId,
                frame.Character.LocalX,
                frame.Character.LocalY,
                frame.Character.HeadingDegrees,
                frame.Character.MapName,
                MovementKind.Roleplay,
                frame.Character.Activity));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync<RoomSnapshot>(
            "JoinRoom",
            new JoinRoomRequest(
                _options.RoomId,
                _playerId,
                "SIM Probe",
                _options.MapName,
                _options.MapCompatibilityId,
                Compatibility: null,
                RoomPassword: _options.RoomPassword),
            cancellationToken);
    }

    public VerificationResult Verify(IReadOnlyList<string> expectedPlayerIds)
    {
        lock (_sync)
        {
            var missing = expectedPlayerIds
                .Where(id => !_samples.TryGetValue(id, out var sample) ||
                             sample.Count < 2 ||
                             sample.DistanceMeters < 0.25d)
                .ToArray();

            if (missing.Length > 0)
            {
                return new VerificationResult(
                    false,
                    $"{missing.Length}/{expectedPlayerIds.Count} players did not produce verifiable movement.");
            }

            var samples = expectedPlayerIds.Select(id => _samples[id]).ToArray();

            if (!string.IsNullOrWhiteSpace(_options.MapName))
            {
                var wrongMap = samples
                    .Where(sample =>
                        !string.Equals(
                            sample.MapName,
                            _options.MapName,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (wrongMap.Length > 0)
                {
                    return new VerificationResult(
                        false,
                        $"{wrongMap.Length}/{expectedPlayerIds.Count} simulated players published on a map different from '{_options.MapName}'.");
                }
            }

            if (_options.Mode == SimulatorMode.Mixed &&
                (!samples.Any(sample => sample.Kind == MovementKind.Vehicle) ||
                 !samples.Any(sample => sample.Kind == MovementKind.Roleplay)))
            {
                return new VerificationResult(
                    false,
                    "Mixed simulation did not deliver both vehicle and RP frames.");
            }

            var minimum = samples.Min(sample => sample.DistanceMeters);
            var rpStates = samples
                .Where(sample => sample.Kind == MovementKind.Roleplay)
                .SelectMany(sample => sample.Activities)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var rpSummary = rpStates.Length == 0
                ? "no RP states"
                : $"RP states: {string.Join(", ", rpStates)}";
            return new VerificationResult(
                true,
                $"{expectedPlayerIds.Count} players forwarded movement; minimum displacement {minimum:F2} m; {rpSummary}.");
        }
    }

    private void Record(
        string playerId,
        double x,
        double y,
        double heading,
        string? mapName,
        MovementKind kind,
        RoleplayCharacterActivity? activity)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(heading))
        {
            return;
        }

        lock (_sync)
        {
            if (!_samples.TryGetValue(playerId, out var sample))
            {
                _samples[playerId] = new MovementSample(
                    x,
                    y,
                    x,
                    y,
                    heading,
                    heading,
                    mapName,
                    1,
                    kind,
                    activity is null ? [] : [activity.Value]);
                return;
            }

            var activities = sample.Activities;
            if (activity is RoleplayCharacterActivity value && !activities.Contains(value))
            {
                activities = [.. activities, value];
            }

            _samples[playerId] = sample with
            {
                LastX = x,
                LastY = y,
                LastHeading = heading,
                Count = sample.Count + 1,
                MapName = mapName ?? sample.MapName,
                Kind = kind,
                Activities = activities
            };
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
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

    private sealed record MovementSample(
        double FirstX,
        double FirstY,
        double LastX,
        double LastY,
        double FirstHeading,
        double LastHeading,
        string? MapName,
        int Count,
        MovementKind Kind,
        IReadOnlyList<RoleplayCharacterActivity> Activities)
    {
        public double DistanceMeters =>
            Math.Sqrt(
                (LastX - FirstX) * (LastX - FirstX) +
                (LastY - FirstY) * (LastY - FirstY));
    }
}

internal sealed record VerificationResult(bool Success, string Message);

internal enum MovementKind
{
    Vehicle,
    Roleplay
}

internal sealed record RoomSimulationContextResolution(
    bool Success,
    SimulatorOptions? Options,
    bool InheritedFromRoom,
    string? ErrorMessage = null);

internal sealed class RoomSimulationContextResolver : IAsyncDisposable
{
    private readonly SimulatorOptions _options;
    private readonly HubConnection _connection;
    private readonly TaskCompletionSource<VehicleTelemetry> _telemetrySeed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _referencePlayerId;

    private RoomSimulationContextResolver(SimulatorOptions options)
    {
        _options = options;
        _connection = new HubConnectionBuilder()
            .WithUrl(NormalizeHubUrl(options.ServerUrl))
            .Build();

        _connection.On<PlayerTelemetryFrame>("telemetry", frame =>
        {
            if (IsRealPlayer(frame.Player) &&
                (_referencePlayerId is null ||
                 string.Equals(frame.Player.PlayerId, _referencePlayerId, StringComparison.OrdinalIgnoreCase)))
            {
                _telemetrySeed.TrySetResult(frame.Telemetry);
            }
        });
    }

    public static async Task<RoomSimulationContextResolution> ResolveAsync(
        SimulatorOptions options,
        CancellationToken cancellationToken)
    {
        var fallbackMap = !string.IsNullOrWhiteSpace(options.MapName);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(fallbackMap ? 5 : 30);
        var announcedWaiting = false;

        while (!cancellationToken.IsCancellationRequested &&
               DateTimeOffset.UtcNow < deadline)
        {
            await using var resolver = new RoomSimulationContextResolver(options);
            RoomSnapshot snapshot;
            try
            {
                snapshot = await resolver.JoinForInspectionAsync(cancellationToken);
            }
            catch (Microsoft.AspNetCore.SignalR.HubException ex)
            {
                return new RoomSimulationContextResolution(
                    false,
                    null,
                    false,
                    $"A sala recusou a entrada do simulador: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return fallbackMap
                    ? new RoomSimulationContextResolution(true, options, false)
                    : new RoomSimulationContextResolution(
                        false,
                        null,
                        false,
                        $"Falha ao consultar a sala: {ex.Message}");
            }

            var reference = SelectReferencePlayer(snapshot);
            if (reference is not null && !string.IsNullOrWhiteSpace(reference.MapName))
            {
                resolver._referencePlayerId = reference.PlayerId;
                var telemetry = await resolver.TryWaitForTelemetryAsync(
                    reference.PlayerId,
                    TimeSpan.FromSeconds(8),
                    cancellationToken);

                if (telemetry is null && !options.PositionExplicit)
                {
                    if (!announcedWaiting)
                    {
                        Console.WriteLine(
                            $"Mapa '{reference.MapName}' encontrado. Aguardando posição real do host para posicionar os simulados próximos...");
                        announcedWaiting = true;
                    }

                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                var operationalState = await resolver.TryGetOperationalStateAsync(cancellationToken);
                var activeLine = operationalState?.Line ?? telemetry?.Line ?? options.ActiveLine;
                var activeRoute = operationalState?.Route ?? telemetry?.Route ?? options.ActiveRoute;
                var activeDestination =
                    operationalState?.DestinationName ??
                    telemetry?.DestinationName ??
                    options.ActiveDestination;
                var activeNextStop =
                    operationalState?.NextStopName ??
                    telemetry?.NextStopName ??
                    options.ActiveNextStop;

                if (string.IsNullOrWhiteSpace(activeLine) &&
                    string.IsNullOrWhiteSpace(activeRoute))
                {
                    if (!announcedWaiting)
                    {
                        Console.WriteLine(
                            "Mapa e posição encontrados. Aguardando linha/rota ativa da autoridade da sala...");
                        announcedWaiting = true;
                    }

                    await Task.Delay(750, cancellationToken);
                    continue;
                }

                var resolved = options with
                {
                    MapName = reference.MapName,
                    MapCompatibilityId =
                        reference.MapCompatibilityId ??
                        reference.Compatibility?.MapCompatibilityId ??
                        telemetry?.MapCompatibilityId,
                    CenterX = options.PositionExplicit
                        ? options.CenterX
                        : telemetry?.LocalX ?? telemetry?.X ?? options.CenterX,
                    CenterY = options.PositionExplicit
                        ? options.CenterY
                        : telemetry?.LocalY ?? telemetry?.Y ?? options.CenterY,
                    CenterZ = options.PositionExplicit
                        ? options.CenterZ
                        : telemetry?.LocalZ ?? telemetry?.Z ?? options.CenterZ,
                    GridX = options.NavigationSeedExplicit
                        ? options.GridX
                        : telemetry?.GridX ?? options.GridX,
                    GridY = options.NavigationSeedExplicit
                        ? options.GridY
                        : telemetry?.GridY ?? options.GridY,
                    TileX = options.NavigationSeedExplicit
                        ? options.TileX
                        : telemetry?.TileX ?? options.TileX,
                    TileY = options.NavigationSeedExplicit
                        ? options.TileY
                        : telemetry?.TileY ?? options.TileY,
                    VehiclePath = options.VehiclePath ?? telemetry?.VehiclePath,
                    VehicleCompatibilityId =
                        options.VehicleCompatibilityId ??
                        telemetry?.VehicleCompatibilityId,
                    ActiveLine = activeLine,
                    ActiveRoute = activeRoute,
                    ActiveDestination = activeDestination,
                    ActiveNextStop = activeNextStop,
                    RadiusMeters = options.RadiusExplicit
                        ? options.RadiusMeters
                        : 18d
                };

                if (!string.IsNullOrWhiteSpace(options.MapName) &&
                    !string.Equals(options.MapName, resolved.MapName, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        $"Map sync: ignorando --map '{options.MapName}' porque a sala está em '{resolved.MapName}'.");
                }

                return new RoomSimulationContextResolution(true, resolved, true);
            }

            if (!announcedWaiting)
            {
                Console.WriteLine(
                    "Waiting for a real NavBR player with an OMSI map in this room (up to 30 s)...");
                announcedWaiting = true;
            }

            await Task.Delay(1000, cancellationToken);
        }

        if (fallbackMap)
        {
            return new RoomSimulationContextResolution(true, options, false);
        }

        return new RoomSimulationContextResolution(
            false,
            null,
            false,
            "A sala não apresentou nenhum jogador real com mapa OMSI válido dentro de 30 segundos.");
    }

    private async Task<RoomSnapshot> JoinForInspectionAsync(CancellationToken cancellationToken)
    {
        await _connection.StartAsync(cancellationToken);
        return await _connection.InvokeAsync<RoomSnapshot>(
            "JoinRoom",
            new JoinRoomRequest(
                _options.RoomId,
                $"sim-map-sync-{Guid.NewGuid():N}"[..30],
                "SIM Map Sync",
                null,
                null,
                Compatibility: null,
                RoomPassword: _options.RoomPassword),
            cancellationToken);
    }

    private async Task<SessionOperationalState?> TryGetOperationalStateAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await _connection.InvokeAsync<SessionOperationalState?>(
                "GetSessionOperationalState",
                cancellationToken);

            if (state is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_referencePlayerId) &&
                !string.Equals(
                    state.AuthorityPlayerId,
                    _referencePlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    private async Task<VehicleTelemetry?> TryWaitForTelemetryAsync(
        string referencePlayerId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _referencePlayerId = referencePlayerId;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await _telemetrySeed.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static PlayerPresence? SelectReferencePlayer(RoomSnapshot snapshot)
    {
        var realPlayers = snapshot.Players
            .Where(IsRealPlayer)
            .Where(player => !string.IsNullOrWhiteSpace(player.MapName))
            .ToArray();

        if (realPlayers.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.TrafficAuthorityPlayerId))
        {
            var authority = realPlayers.FirstOrDefault(player =>
                string.Equals(
                    player.PlayerId,
                    snapshot.TrafficAuthorityPlayerId,
                    StringComparison.OrdinalIgnoreCase));
            if (authority is not null)
            {
                return authority;
            }
        }

        return realPlayers
            .OrderBy(player => player.ConnectedAtUtc)
            .First();
    }

    private static bool IsRealPlayer(PlayerPresence player) =>
        !player.PlayerId.StartsWith("sim-", StringComparison.OrdinalIgnoreCase) &&
        !player.DisplayName.StartsWith("SIM ", StringComparison.OrdinalIgnoreCase);

    public async ValueTask DisposeAsync()
    {
        try
        {
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

internal sealed class LocalServerBootstrap : IAsyncDisposable
{
    private readonly Process? _process;

    private LocalServerBootstrap(bool ready, string serverUrl, bool startedServer, string? errorMessage, Process? process)
    {
        Ready = ready;
        ServerUrl = serverUrl;
        StartedServer = startedServer;
        ErrorMessage = errorMessage;
        _process = process;
    }

    public bool Ready { get; }
    public string ServerUrl { get; }
    public bool StartedServer { get; }
    public string? ErrorMessage { get; }

    public static async Task<LocalServerBootstrap> EnsureAsync(
        SimulatorOptions options,
        CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(options.ServerUrl);
        if (await IsHealthyAsync(baseUrl, cancellationToken))
        {
            return new LocalServerBootstrap(true, baseUrl, false, null, null);
        }

        if (!options.AutoStartLocalServer || !IsLoopback(baseUrl))
        {
            return new LocalServerBootstrap(
                false,
                baseUrl,
                false,
                $"Nenhum NavBR.Server respondeu em {baseUrl}.",
                null);
        }

        var startInfo = BuildStartInfo(baseUrl);
        if (startInfo is null)
        {
            return new LocalServerBootstrap(
                false,
                baseUrl,
                false,
                "O servidor local não está em execução e o simulador não encontrou NavBR.Server.exe nem o projeto do servidor para iniciá-lo.",
                null);
        }

        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process is null)
            {
                return new LocalServerBootstrap(false, baseUrl, false, "Falha ao iniciar o NavBR.Server local.", null);
            }

            for (var attempt = 0; attempt < 30; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    return new LocalServerBootstrap(
                        false,
                        baseUrl,
                        false,
                        $"NavBR.Server encerrou durante a inicialização (exit code {process.ExitCode}).",
                        process);
                }

                if (await IsHealthyAsync(baseUrl, cancellationToken))
                {
                    return new LocalServerBootstrap(true, baseUrl, true, null, process);
                }

                await Task.Delay(500, cancellationToken);
            }

            return new LocalServerBootstrap(
                false,
                baseUrl,
                false,
                $"NavBR.Server foi iniciado, mas /health não respondeu em {baseUrl} dentro do tempo esperado.",
                process);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new LocalServerBootstrap(
                false,
                baseUrl,
                false,
                $"Falha ao iniciar o NavBR.Server local: {ex.Message}",
                process);
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (_process is not null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
        }
        finally
        {
            _process?.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static async Task<bool> IsHealthyAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            using var response = await client.GetAsync($"{baseUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo? BuildStartInfo(string baseUrl)
    {
        var packagedCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "server", "NavBR.Server.exe"),
            Path.Combine(AppContext.BaseDirectory, "NavBR.Server.exe")
        };

        foreach (var executable in packagedCandidates)
        {
            if (!File.Exists(executable))
            {
                continue;
            }

            var info = new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.Environment["ASPNETCORE_URLS"] = baseUrl;
            return info;
        }

        var project = FindServerProject();
        if (project is null)
        {
            return null;
        }

        var sourceInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Directory.GetParent(Path.GetDirectoryName(project)!)?.Parent?.FullName
                               ?? Path.GetDirectoryName(project)!,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        sourceInfo.ArgumentList.Add("run");
        sourceInfo.ArgumentList.Add("--project");
        sourceInfo.ArgumentList.Add(project);
        sourceInfo.ArgumentList.Add("-c");
        sourceInfo.ArgumentList.Add("Release");
        sourceInfo.ArgumentList.Add("--no-launch-profile");
        sourceInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
        return sourceInfo;
    }

    private static string? FindServerProject()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (var depth = 0; current is not null && depth < 10; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "src", "NavBR.Server", "NavBR.Server.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsLoopback(string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBaseUrl(string serverUrl)
    {
        var value = serverUrl.Trim().TrimEnd('/');
        const string hubPath = "/hubs/multiplayer";
        if (value.EndsWith(hubPath, StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^hubPath.Length];
        }

        return value;
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
    string? RoomPassword,
    int PlayerCount,
    string? MapName,
    string? MapCompatibilityId,
    string? VehiclePath,
    string? VehicleCompatibilityId,
    string? ActiveLine,
    string? ActiveRoute,
    string? ActiveDestination,
    string? ActiveNextStop,
    int? GridX,
    int? GridY,
    double? TileX,
    double? TileY,
    double CenterX,
    double CenterY,
    double CenterZ,
    double RadiusMeters,
    int IntervalMilliseconds,
    int DurationSeconds,
    string NamePrefix,
    SimulatorMode Mode,
    bool Verify,
    bool AutoStartLocalServer,
    bool PositionExplicit,
    bool NavigationSeedExplicit,
    bool RadiusExplicit,
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
            RoomPassword: NullIfEmpty(values.GetValueOrDefault("password")),
            PlayerCount: ClampInt(values.GetValueOrDefault("players"), 6, 1, 32),
            MapName: NullIfEmpty(values.GetValueOrDefault("map")),
            MapCompatibilityId: NullIfEmpty(values.GetValueOrDefault("map-id")),
            VehiclePath: NullIfEmpty(values.GetValueOrDefault("vehicle-path")),
            VehicleCompatibilityId: NullIfEmpty(values.GetValueOrDefault("vehicle-id")),
            ActiveLine: NullIfEmpty(values.GetValueOrDefault("line")),
            ActiveRoute: NullIfEmpty(values.GetValueOrDefault("route")),
            ActiveDestination: NullIfEmpty(values.GetValueOrDefault("destination")),
            ActiveNextStop: NullIfEmpty(values.GetValueOrDefault("next-stop")),
            GridX: ParseNullableInt(values.GetValueOrDefault("grid-x")),
            GridY: ParseNullableInt(values.GetValueOrDefault("grid-y")),
            TileX: ParseNullableDouble(values.GetValueOrDefault("tile-x")),
            TileY: ParseNullableDouble(values.GetValueOrDefault("tile-y")),
            CenterX: ParseDouble(values.GetValueOrDefault("x"), 0d),
            CenterY: ParseDouble(values.GetValueOrDefault("y"), 0d),
            CenterZ: ParseDouble(values.GetValueOrDefault("z"), 0d),
            RadiusMeters: Math.Clamp(ParseDouble(values.GetValueOrDefault("radius"), 18d), 6d, 2000d),
            IntervalMilliseconds: ClampInt(values.GetValueOrDefault("interval"), 250, 100, 5000),
            DurationSeconds: Math.Max(0, ClampInt(values.GetValueOrDefault("duration"), 0, 0, 86400)),
            NamePrefix: NullIfEmpty(values.GetValueOrDefault("prefix")) ?? "SIM",
            Mode: mode,
            Verify: values.ContainsKey("verify"),
            AutoStartLocalServer: !values.ContainsKey("no-auto-server"),
            PositionExplicit:
                values.ContainsKey("x") ||
                values.ContainsKey("y") ||
                values.ContainsKey("z"),
            NavigationSeedExplicit:
                values.ContainsKey("grid-x") ||
                values.ContainsKey("grid-y") ||
                values.ContainsKey("tile-x") ||
                values.ContainsKey("tile-y"),
            RadiusExplicit: values.ContainsKey("radius"),
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
  --password TEXT       Password when joining a private room.
  --players N          Simulated players, 1..32 (default 6)
  --mode vehicles|rp|mixed
  --map NAME           Optional map override. When omitted, inherit the real map from the room host.
  --map-id ID          Optional map compatibility id override.
  --x N --y N --z N   Optional movement center. When omitted, bots spawn around the real host position.
  --grid-x N --grid-y N
  --tile-x N --tile-y N
                       Optional navigation override. When omitted, inherit host grid/tile when available.
  --radius N           Movement radius in meters (default 18; centered on the real host when synchronized)
  --interval MS        Publish interval, 100..5000 (default 250)
  --duration SEC       0 = until Ctrl+C
  --verify             Verify that frames cross SignalR and positions move.
  --no-auto-server     Do not auto-start a bundled/local NavBR.Server on loopback.
  --prefix TEXT        Display-name prefix (default SIM)
  --vehicle-path PATH  Optional real .bus path for physical-vehicle testing.
  --vehicle-id ID      Optional real vehicle compatibility id.
  --line TEXT          Fallback line only for isolated tests.
  --route TEXT         Fallback route only for isolated tests.
  --destination TEXT   Fallback destination only for isolated tests.
  --next-stop TEXT     Fallback next stop only for isolated tests.
                       In a real room, the authority's active operation wins.
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

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static double? ParseNullableDouble(string? value) =>
        double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
