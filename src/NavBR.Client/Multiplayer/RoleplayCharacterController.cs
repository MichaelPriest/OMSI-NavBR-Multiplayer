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
    private const int VkE = 0x45;
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
    private const double EnterBusDistanceMeters = 8d;
    private const double MaxVerticalFollowSpeedMps = 2.75d;
    private const double MaxInitialGroundOffsetMeters = 3.5d;
    private const double MaxGroundSampleJumpMeters = 1.25d;
    private const double MaxGroundTargetErrorMeters = 1.5d;

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
    private int _stopping;
    private int _consecutiveFailures;
    private int _interactionInFlight;
    private long _sessionGeneration;
    private double _originX;
    private double _originY;
    private double _groundHeightOffset;
    private bool _groundHeightCalibrated;
    private bool _groundFollowing;
    private double? _lastGroundHeight;

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
    public bool IsGroundFollowing => _groundFollowing;
    public double EnterBusRangeMeters => EnterBusDistanceMeters;
    public double InteractionRangeMeters => EnterBusDistanceMeters;
    public RoleplayCharacterState? CurrentState => _state;

    public double? GetBusDistanceMeters()
    {
        if (_state is not { IsActive: true } current)
        {
            return null;
        }

        var telemetry = _telemetrySource();
        var map = _activeMapSource();
        if (telemetry?.IsInGame != true ||
            telemetry.LocalX is not double busX ||
            telemetry.LocalY is not double busY ||
            !double.IsFinite(busX) ||
            !double.IsFinite(busY) ||
            !IsSameRoleplayMap(current, telemetry, map))
        {
            return null;
        }

        var dx = current.LocalX - busX;
        var dy = current.LocalY - busY;
        var dz = telemetry.LocalZ is double busZ && double.IsFinite(busZ)
            ? current.LocalZ - busZ
            : 0d;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return double.IsFinite(distance) ? distance : null;
    }

    public async Task<bool> TryTriggerBusInteractionAsync(
        string triggerName,
        CancellationToken cancellationToken = default)
    {
        if (_state is not { IsActive: true } current ||
            string.IsNullOrWhiteSpace(_instanceId))
        {
            StatusChanged?.Invoke("roleplay-interaction-unavailable");
            return false;
        }

        triggerName = triggerName?.Trim() ?? string.Empty;
        if (triggerName.Length is <= 0 or > 128)
        {
            StatusChanged?.Invoke("roleplay-interaction-invalid");
            return false;
        }

        if (!IsInteractionRuntimeAvailable)
        {
            StatusChanged?.Invoke("roleplay-interaction-plugin-unavailable");
            return false;
        }

        var distance = GetBusDistanceMeters();
        if (distance is not double finiteDistance)
        {
            StatusChanged?.Invoke("roleplay-bus-position-unavailable");
            return false;
        }

        if (finiteDistance > EnterBusDistanceMeters)
        {
            StatusChanged?.Invoke("roleplay-interaction-too-far");
            return false;
        }

        if (Interlocked.CompareExchange(ref _interactionInFlight, 1, 0) != 0)
        {
            StatusChanged?.Invoke("roleplay-interaction-busy");
            return false;
        }

        var instanceId = _instanceId;
        try
        {
            var pressed = await OmsiPluginBridgeRelay.SetRoleplayVehicleTriggerAsync(
                instanceId,
                current.PlayerId,
                triggerName,
                active: true,
                cancellationToken);
            if (pressed?.Success != true)
            {
                StatusChanged?.Invoke(
                    pressed?.ErrorCode ??
                    "roleplay-trigger-failed");
                return false;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(75),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // A release is still attempted below so a cancelled UI request
                // cannot leave the OMSI trigger held.
            }

            var released = await OmsiPluginBridgeRelay.SetRoleplayVehicleTriggerAsync(
                instanceId,
                current.PlayerId,
                triggerName,
                active: false,
                CancellationToken.None);
            if (released?.Success != true)
            {
                StatusChanged?.Invoke(
                    released?.ErrorCode ??
                    "roleplay-trigger-release-failed");
                return false;
            }

            StatusChanged?.Invoke("roleplay-interaction-triggered");
            return true;
        }
        finally
        {
            Interlocked.Exchange(ref _interactionInFlight, 0);
        }
    }

    public async Task<bool> TryEnterBusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsActive)
        {
            return true;
        }

        var distance = GetBusDistanceMeters();
        if (distance is not double finiteDistance)
        {
            StatusChanged?.Invoke("roleplay-bus-position-unavailable");
            return false;
        }

        if (finiteDistance > EnterBusDistanceMeters)
        {
            StatusChanged?.Invoke("roleplay-bus-too-far");
            return false;
        }

        await StopAsync("roleplay-entered-bus", cancellationToken);
        return true;
    }

    public bool IsInteractionRuntimeAvailable
    {
        get
        {
            if (!IsRuntimeAvailable ||
                Application.Current is not App app)
            {
                return false;
            }

            return app.PluginBridge.SupportsCapability(
                PluginBridgeProtocol.CapabilityCharacterInteraction);
        }
    }

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
            telemetry.HeadingDegrees,
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
        var initialGroundZ = 0d;
        var initialGroundResolved = map is not null &&
                                    OmsiSplineGroundHeightResolver.TryResolve(
                                        map,
                                        telemetry,
                                        x,
                                        y,
                                        preferredGroundZ: z,
                                        out initialGroundZ);
        var initialGroundOffset = initialGroundResolved
            ? z - initialGroundZ
            : double.NaN;
        _groundFollowing = initialGroundResolved &&
                           double.IsFinite(initialGroundOffset) &&
                           Math.Abs(initialGroundOffset) <= MaxInitialGroundOffsetMeters;
        _groundHeightCalibrated = _groundFollowing;
        _groundHeightOffset = _groundFollowing
            ? initialGroundOffset
            : 0d;
        _lastGroundHeight = _groundFollowing
            ? initialGroundZ
            : null;

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

        Interlocked.Increment(ref _sessionGeneration);
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
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
        {
            return;
        }

        try
        {
            _timer.Stop();
            DisposeKeyboardHook();
            Interlocked.Increment(ref _sessionGeneration);

            var instanceId = _instanceId;
            var playerId = _state?.PlayerId;

            _instanceId = null;
            _state = null;
            _consecutiveFailures = 0;
            _groundFollowing = false;
            _groundHeightCalibrated = false;
            _groundHeightOffset = 0d;
            _lastGroundHeight = null;
            lock (_inputSync)
            {
                _pressedKeys.Clear();
            }

            // Clear local state first so the UI/HUD can always leave RP mode,
            // even if the experimental plugin fails while restoring the driver.
            StateChanged?.Invoke(null);
            StatusChanged?.Invoke(reason);

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                try
                {
                    var release = await OmsiPluginBridgeRelay.ReleaseRoleplayCharacterAsync(
                        instanceId,
                        playerId,
                        cancellationToken);
                    if (release?.Success != true)
                    {
                        StatusChanged?.Invoke("roleplay-release-failed");
                    }
                }
                catch
                {
                    StatusChanged?.Invoke("roleplay-release-failed");
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _stopping, 0);
        }
    }

    private async Task TickAsync()
    {
        var sessionGeneration = Volatile.Read(ref _sessionGeneration);
        var instanceId = _instanceId;
        if (_state is not { IsActive: true } current ||
            string.IsNullOrWhiteSpace(instanceId) ||
            Interlocked.CompareExchange(ref _updateInFlight, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var mapKey = _mapKeySource();
            var selected = RoleplayCharacterSelectionStore.Get(mapKey);
            var telemetry = _telemetrySource();
            var map = _activeMapSource();
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

            var z = current.LocalZ;
            if (TryFollowGround(
                    map,
                    telemetry,
                    x,
                    y,
                    z,
                    deltaSeconds,
                    out var followedZ))
            {
                z = followedZ;
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
                LocalZ = z,
                HeadingDegrees = heading,
                SpeedMps = speed,
                Activity = activity
            };

            var result = await OmsiPluginBridgeRelay.UpdateRoleplayCharacterAsync(
                instanceId,
                updated,
                MultiplayerSettingsStore.Load().DisplayName);

            if (sessionGeneration != Volatile.Read(ref _sessionGeneration) ||
                !string.Equals(instanceId, _instanceId, StringComparison.Ordinal))
            {
                return;
            }

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

    private bool TryFollowGround(
        OmsiMapInfo? map,
        VehicleTelemetry? telemetry,
        double x,
        double y,
        double currentZ,
        double deltaSeconds,
        out double resolvedZ)
    {
        resolvedZ = currentZ;

        if (map is null ||
            telemetry is null ||
            !OmsiSplineGroundHeightResolver.TryResolve(
                map,
                telemetry,
                x,
                y,
                _lastGroundHeight ?? currentZ,
                out var groundZ))
        {
            _groundFollowing = false;
            return false;
        }

        if (!_groundHeightCalibrated)
        {
            var offset = currentZ - groundZ;
            if (!double.IsFinite(offset) ||
                Math.Abs(offset) > MaxInitialGroundOffsetMeters)
            {
                _groundFollowing = false;
                return false;
            }

            _groundHeightOffset = offset;
            _groundHeightCalibrated = true;
        }
        else if (_lastGroundHeight is double previousGround &&
                 Math.Abs(groundZ - previousGround) > MaxGroundSampleJumpMeters)
        {
            // A sudden Z discontinuity usually means an overlapping road,
            // bridge or another nearby spline became the 2D nearest candidate.
            // Keep the current character height rather than drifting to it.
            _groundFollowing = false;
            return false;
        }

        var targetZ = groundZ + _groundHeightOffset;
        if (!double.IsFinite(targetZ) ||
            Math.Abs(targetZ - currentZ) > MaxGroundTargetErrorMeters)
        {
            _groundFollowing = false;
            return false;
        }

        var maxVerticalDelta = Math.Max(
            0.02d,
            MaxVerticalFollowSpeedMps * deltaSeconds);
        resolvedZ = currentZ + Math.Clamp(
            targetZ - currentZ,
            -maxVerticalDelta,
            maxVerticalDelta);

        _lastGroundHeight = groundZ;
        _groundFollowing = true;
        return true;
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
                    async () => await StopAsync("roleplay-emergency-return"));
            }
            return true;
        }

        if (virtualKey == VkE)
        {
            var shouldEnter = false;
            lock (_inputSync)
            {
                if (isDown)
                {
                    shouldEnter = _pressedKeys.Add(virtualKey);
                }
                else
                {
                    _pressedKeys.Remove(virtualKey);
                }
            }

            if (shouldEnter)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(
                    async () => await TryEnterBusAsync());
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

    private static bool IsSameRoleplayMap(
        RoleplayCharacterState state,
        VehicleTelemetry telemetry,
        OmsiMapInfo? map)
    {
        var currentCompatibilityId =
            telemetry.MapCompatibilityId ??
            map?.CompatibilityId;
        if (!string.IsNullOrWhiteSpace(state.MapCompatibilityId) &&
            !string.IsNullOrWhiteSpace(currentCompatibilityId))
        {
            return string.Equals(
                state.MapCompatibilityId,
                currentCompatibilityId,
                StringComparison.OrdinalIgnoreCase);
        }

        var currentMapName =
            telemetry.MapName ??
            map?.FolderName;
        return !string.IsNullOrWhiteSpace(state.MapName) &&
               !string.IsNullOrWhiteSpace(currentMapName) &&
               string.Equals(
                   state.MapName,
                   currentMapName,
                   StringComparison.OrdinalIgnoreCase);
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
