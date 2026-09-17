using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Maps;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

internal sealed class RoleplayCharacterController : IAsyncDisposable
{
    private const int VkEscape = 0x1B;
    private const int VkW = 0x57;
    private const int VkA = 0x41;
    private const int VkS = 0x53;
    private const int VkD = 0x44;
    private const int VkShift = 0x10;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;

    private const double WalkSpeedMps = 1.45d;
    private const double RunSpeedMps = 3.25d;
    private const double BackwardSpeedMps = 1.05d;
    private const double TurnSpeedDegreesPerSecond = 105d;
    private const double MaxDistanceFromBusMeters = 85d;

    private readonly Func<VehicleTelemetry?> _telemetrySource;
    private readonly Func<OmsiMapInfo?> _activeMapSource;
    private readonly Func<string?> _mapKeySource;
    private readonly DispatcherTimer _timer;
    private readonly object _inputSync = new();
    private readonly HashSet<int> _pressedKeys = [];

    private RoleplayKeyboardHook? _keyboardHook;
    private RoleplayCharacterState? _state;
    private string? _instanceId;
    private DateTimeOffset _lastTickUtc;
    private DateTimeOffset _lastNetworkStateUtc;
    private int _updateInFlight;
    private int _consecutiveFailures;
    private double _originX;
    private double _originY;

    public event Action<RoleplayCharacterState?>? StateChanged;
    public event Action<RoleplayCharacterState>? NetworkStateReady;
    public event Action<string>? StatusChanged;

    public RoleplayCharacterController(
        Func<VehicleTelemetry?> telemetrySource,
        Func<OmsiMapInfo?> activeMapSource,
        Func<string?> mapKeySource)
    {
        _telemetrySource = telemetrySource;
        _activeMapSource = activeMapSource;
        _mapKeySource = mapKeySource;

        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(50d)
        };
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public bool IsActive => _state?.IsActive == true;
    public RoleplayCharacterState? CurrentState => _state;

    public bool IsRuntimeAvailable
    {
        get
        {
            if (!ExperimentalFeatureFlags.RoleplayCharacterEnabled ||
                Application.Current is not App app ||
                !app.PluginBridge.IsConnected)
            {
                return false;
            }

            return app.PluginBridge.SupportsCapability(
                       PluginBridgeProtocol.CapabilityCharacterPossession) &&
                   app.PluginBridge.SupportsCapability(
                       PluginBridgeProtocol.CapabilityCharacterTransform);
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsActive)
        {
            return true;
        }

        var telemetry = _telemetrySource();
        var map = _activeMapSource();
        var mapKey = _mapKeySource();
        var selected = RoleplayCharacterSelectionStore.Get(mapKey);

        if (!ExperimentalFeatureFlags.RoleplayCharacterEnabled)
        {
            StatusChanged?.Invoke("roleplay-disabled");
            return false;
        }

        if (!IsRuntimeAvailable)
        {
            StatusChanged?.Invoke("roleplay-plugin-unavailable");
            return false;
        }

        if (selected is null)
        {
            StatusChanged?.Invoke("roleplay-character-required");
            return false;
        }

        if (telemetry is null ||
            !telemetry.IsInGame ||
            telemetry.LocalX is not double anchorX ||
            telemetry.LocalY is not double anchorY ||
            telemetry.LocalZ is not double anchorZ ||
            !double.IsFinite(anchorX) ||
            !double.IsFinite(anchorY) ||
            !double.IsFinite(anchorZ))
        {
            StatusChanged?.Invoke("roleplay-waiting-telemetry");
            return false;
        }

        var settings = MultiplayerSettingsStore.Load();
        var playerId = string.IsNullOrWhiteSpace(settings.PlayerId)
            ? Guid.NewGuid().ToString("N")
            : settings.PlayerId.Trim();
        var displayName = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? "Driver"
            : settings.DisplayName.Trim();

        _instanceId = $"rp-local-{playerId}";
        var result = await OmsiPluginBridgeRelay.AcquireRoleplayCharacterAsync(
            _instanceId,
            playerId,
            displayName,
            anchorX,
            anchorY,
            anchorZ,
            selected.DefinitionPointer,
            cancellationToken);

        if (result?.Success != true ||
            result.LocalX is not double x ||
            result.LocalY is not double y ||
            result.LocalZ is not double z ||
            result.HeadingDegrees is not double heading)
        {
            _instanceId = null;
            StatusChanged?.Invoke(result?.ErrorCode ?? "roleplay-acquire-failed");
            return false;
        }

        _originX = x;
        _originY = y;
        _state = new RoleplayCharacterState(
            PlayerId: playerId,
            Timestamp: DateTimeOffset.UtcNow,
            MapName: telemetry.MapName ?? map?.FolderName,
            MapCompatibilityId: telemetry.MapCompatibilityId ?? map?.CompatibilityId,
            LocalX: x,
            LocalY: y,
            LocalZ: z,
            HeadingDegrees: NormalizeHeading(heading),
            SpeedMps: 0d,
            Activity: RoleplayCharacterActivity.Idle,
            IsActive: true,
            CharacterId: selected.Id,
            CharacterName: selected.DisplayName,
            HumanIndex: result.CharacterHumanIndex);

        _consecutiveFailures = 0;
        _lastTickUtc = DateTimeOffset.UtcNow;
        _lastNetworkStateUtc = DateTimeOffset.MinValue;
        InstallKeyboardHook();
        _timer.Start();
        StateChanged?.Invoke(_state);
        EmitNetworkState(_state);
        StatusChanged?.Invoke("roleplay-active");
        return true;
    }

    public async Task StopAsync(
        string reason = "roleplay-stopped",
        CancellationToken cancellationToken = default)
    {
        _timer.Stop();
        DisposeKeyboardHook();

        var instanceId = _instanceId;
        var playerId = _state?.PlayerId;
        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            _ = await OmsiPluginBridgeRelay.ReleaseRoleplayCharacterAsync(
                instanceId,
                playerId,
                cancellationToken);
        }

        _instanceId = null;
        _state = null;
        _consecutiveFailures = 0;
        lock (_inputSync)
        {
            _pressedKeys.Clear();
        }

        StateChanged?.Invoke(null);
        StatusChanged?.Invoke(reason);
    }

    private async Task TickAsync()
    {
        if (_state is not { IsActive: true } current ||
            string.IsNullOrWhiteSpace(_instanceId) ||
            Interlocked.CompareExchange(ref _updateInFlight, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var mapKey = _mapKeySource();
            var selected = RoleplayCharacterSelectionStore.Get(mapKey);
            if (selected is null ||
                !string.Equals(selected.Id, current.CharacterId, StringComparison.OrdinalIgnoreCase))
            {
                await StopAsync("roleplay-map-or-character-changed");
                return;
            }

            if (!RoleplayKeyboardHook.IsOmsiForeground())
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var deltaSeconds = Math.Clamp(
                (now - _lastTickUtc).TotalSeconds,
                0d,
                0.15d);
            _lastTickUtc = now;

            bool forward;
            bool backward;
            bool left;
            bool right;
            bool running;
            lock (_inputSync)
            {
                forward = _pressedKeys.Contains(VkW);
                backward = _pressedKeys.Contains(VkS);
                left = _pressedKeys.Contains(VkA);
                right = _pressedKeys.Contains(VkD);
                running = _pressedKeys.Contains(VkShift) ||
                          _pressedKeys.Contains(VkLeftShift) ||
                          _pressedKeys.Contains(VkRightShift);
            }

            var heading = current.HeadingDegrees;
            if (left ^ right)
            {
                heading += (right ? 1d : -1d) *
                           TurnSpeedDegreesPerSecond *
                           deltaSeconds;
                heading = NormalizeHeading(heading);
            }

            var direction = forward == backward ? 0d : forward ? 1d : -1d;
            var speed = direction switch
            {
                > 0d => running ? RunSpeedMps : WalkSpeedMps,
                < 0d => BackwardSpeedMps,
                _ => 0d
            };

            var x = current.LocalX;
            var y = current.LocalY;
            if (direction != 0d && speed > 0d)
            {
                var radians = heading * Math.PI / 180d;
                var signedDistance = direction * speed * deltaSeconds;
                x += Math.Sin(radians) * signedDistance;
                y += Math.Cos(radians) * signedDistance;

                var fromOriginX = x - _originX;
                var fromOriginY = y - _originY;
                var fromOrigin = Math.Sqrt(
                    fromOriginX * fromOriginX +
                    fromOriginY * fromOriginY);
                if (fromOrigin > MaxDistanceFromBusMeters)
                {
                    var scale = MaxDistanceFromBusMeters / fromOrigin;
                    x = _originX + fromOriginX * scale;
                    y = _originY + fromOriginY * scale;
                }
            }

            var activity = speed <= 0.01d
                ? RoleplayCharacterActivity.Idle
                : running && direction > 0d
                    ? RoleplayCharacterActivity.Running
                    : RoleplayCharacterActivity.Walking;

            var updated = current with
            {
                Timestamp = now,
                LocalX = x,
                LocalY = y,
                HeadingDegrees = heading,
                SpeedMps = speed,
                Activity = activity
            };

            var result = await OmsiPluginBridgeRelay.UpdateRoleplayCharacterAsync(
                _instanceId,
                updated,
                MultiplayerSettingsStore.Load().DisplayName);

            if (result?.Success != true)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= 3)
                {
                    await StopAsync(result?.ErrorCode ?? "roleplay-control-lost");
                }
                return;
            }

            _consecutiveFailures = 0;
            _state = updated with
            {
                LocalX = result.LocalX ?? updated.LocalX,
                LocalY = result.LocalY ?? updated.LocalY,
                LocalZ = result.LocalZ ?? updated.LocalZ,
                HeadingDegrees = result.HeadingDegrees ?? updated.HeadingDegrees,
                SpeedMps = result.SpeedMps ?? updated.SpeedMps
            };

            StateChanged?.Invoke(_state);
            EmitNetworkState(_state);
        }
        finally
        {
            Interlocked.Exchange(ref _updateInFlight, 0);
        }
    }

    private void EmitNetworkState(RoleplayCharacterState state)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastNetworkStateUtc < TimeSpan.FromMilliseconds(100d))
        {
            return;
        }

        _lastNetworkStateUtc = now;
        NetworkStateReady?.Invoke(state);
    }

    private void InstallKeyboardHook()
    {
        DisposeKeyboardHook();
        try
        {
            _keyboardHook = new RoleplayKeyboardHook
            {
                HandleKey = HandleRoleplayKey
            };
        }
        catch
        {
            _keyboardHook = null;
        }
    }

    private bool HandleRoleplayKey(int virtualKey, bool isDown)
    {
        if (!IsActive || !RoleplayKeyboardHook.IsOmsiForeground())
        {
            return false;
        }

        if (virtualKey == VkEscape)
        {
            if (isDown)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(
                    async () => await StopAsync("roleplay-exit"));
            }
            return true;
        }

        var controlled = virtualKey is
            VkW or VkA or VkS or VkD or
            VkShift or VkLeftShift or VkRightShift;

        if (!controlled)
        {
            return false;
        }

        lock (_inputSync)
        {
            if (isDown)
            {
                _pressedKeys.Add(virtualKey);
            }
            else
            {
                _pressedKeys.Remove(virtualKey);
            }
        }

        return true;
    }

    private void DisposeKeyboardHook()
    {
        if (_keyboardHook is null)
        {
            return;
        }

        _keyboardHook.HandleKey = null;
        _keyboardHook.Dispose();
        _keyboardHook = null;
    }

    private static double NormalizeHeading(double value)
    {
        value %= 360d;
        return value < 0d ? value + 360d : value;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync("roleplay-disposed");
        _timer.Stop();
        DisposeKeyboardHook();
    }
}
