using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Maps;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

internal sealed record RoleplayNativeAnimationDiagnostics(
    int AiMode,
    int AiModeEx,
    int AiSubMode,
    double SollSpeedMps,
    double ActSpeedMps,
    double LastMovedDistanceMeters,
    double AnimationState,
    int? ActivityLegRaw,
    int? ActivityArmUmbrellaRaw,
    int? ActivityArmKiRaw,
    int? ActivityHeadKiRaw);

internal sealed record RoleplayNativeActivityObservation(
    int Samples,
    int MovingSamples,
    int TransitionCount,
    int MovingTransitionCount,
    bool ChangedThisFrame,
    DateTimeOffset? LastTransitionAtUtc);

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
    private const double StandingTurnSpeedDegreesPerSecond = 120d;
    private const double MovingTurnSpeedDegreesPerSecond = 96d;
    private const double MovementAccelerationMps2 = 4.25d;
    private const double MovementBrakingMps2 = 6.5d;
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
    private RoleplayNativeAnimationDiagnostics? _nativeAnimationDiagnostics;
    private RoleplayNativeActivityObservation? _nativeActivityObservation;
    private (int? Leg, int? ArmUmbrella, int? ArmKi, int? HeadKi)? _lastNativeActivitySignature;
    private int _nativeActivitySamples;
    private int _nativeActivityMovingSamples;
    private int _nativeActivityTransitions;
    private int _nativeActivityMovingTransitions;
    private DateTimeOffset? _nativeActivityLastTransitionAtUtc;
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
    private string? _lastErrorCode;
    private string? _lastErrorMessage;
    private double _signedMovementSpeedMps;
    private bool _focusStopApplied;

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
    public RoleplayNativeAnimationDiagnostics? CurrentNativeAnimationDiagnostics =>
        _nativeAnimationDiagnostics;
    public RoleplayNativeActivityObservation? CurrentNativeActivityObservation =>
        _nativeActivityObservation;
    public string? LastErrorCode => _lastErrorCode;
    public string? LastErrorMessage => _lastErrorMessage;

    private void SetStatus(
        string status,
        string? errorMessage = null,
        bool isError = false)
    {
        if (isError)
        {
            _lastErrorCode = status;
            _lastErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? status
                : errorMessage.Trim();
        }
        else
        {
            _lastErrorCode = null;
            _lastErrorMessage = null;
        }

        StatusChanged?.Invoke(status);
    }

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
        var sessionGeneration = Volatile.Read(ref _sessionGeneration);
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
                if (IsCurrentInteractionSession(
                        instanceId,
                        sessionGeneration))
                {
                    StatusChanged?.Invoke(
                        pressed?.ErrorCode ??
                        "roleplay-trigger-failed");
                }

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
                if (IsCurrentInteractionSession(
                        instanceId,
                        sessionGeneration))
                {
                    StatusChanged?.Invoke(
                        released?.ErrorCode ??
                        "roleplay-trigger-release-failed");
                }

                return false;
            }

            if (IsCurrentInteractionSession(
                    instanceId,
                    sessionGeneration))
            {
                StatusChanged?.Invoke("roleplay-interaction-triggered");
            }

            return true;
        }
        finally
        {
            Interlocked.Exchange(ref _interactionInFlight, 0);
        }
    }

    private bool IsCurrentInteractionSession(
        string instanceId,
        long sessionGeneration) =>
        sessionGeneration == Volatile.Read(ref _sessionGeneration) &&
        string.Equals(
            _instanceId,
            instanceId,
            StringComparison.Ordinal) &&
        _state?.IsActive == true;

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
            SetStatus(
                "roleplay-disabled",
                "Character / RP writes are disabled.",
                isError: true);
            return false;
        }

        if (Application.Current is not App app || !app.PluginBridge.IsConnected)
        {
            SetStatus(
                "roleplay-plugin-disconnected",
                "Plugin Bridge is disconnected. Confirm the NavBR OMSI plugin is loaded by OMSI and restart OMSI after any plugin update.",
                isError: true);
            return false;
        }

        if (!app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityCharacterPossession) ||
            !app.PluginBridge.SupportsCapability(PluginBridgeProtocol.CapabilityCharacterTransform))
        {
            var connection = app.PluginBridge.GetConnectionInfo();
            var capabilities = connection.LastCapabilities?.Capabilities ??
                               connection.LastStatus?.Capabilities ??
                               Array.Empty<string>();
            var component = string.IsNullOrWhiteSpace(connection.PluginComponentVersion)
                ? "unknown"
                : connection.PluginComponentVersion;
            SetStatus(
                "roleplay-plugin-capability-unavailable",
                $"Plugin Bridge connected (plugin {component}) but RP capabilities are missing. Reported capabilities: {(capabilities.Length == 0 ? "none" : string.Join(", ", capabilities))}.",
                isError: true);
            return false;
        }

        if (selected is null)
        {
            SetStatus(
                "roleplay-character-required",
                "No live OMSI driver is selected for the current map.",
                isError: true);
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
            SetStatus(
                "roleplay-waiting-telemetry",
                "The player bus does not yet expose a finite local OMSI pose.",
                isError: true);
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
            SetStatus(
                result?.ErrorCode ?? "roleplay-acquire-failed",
                result?.ErrorMessage ??
                "The Plugin Bridge did not confirm acquisition of the active OMSI driver.",
                isError: true);
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

        ResetNativeActivityObservation();
        UpdateNativeAnimationDiagnostics(result, commandedSpeedMps: 0d);
        Interlocked.Increment(ref _sessionGeneration);
        _consecutiveFailures = 0;
        _signedMovementSpeedMps = 0d;
        _focusStopApplied = false;
        _lastTickUtc = DateTimeOffset.UtcNow;
        _lastNetworkStateUtc = DateTimeOffset.MinValue;
        InstallKeyboardHook();
        _timer.Start();
        StateChanged?.Invoke(_state);
        EmitNetworkState(_state);
        SetStatus("roleplay-active");
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
            _nativeAnimationDiagnostics = null;
            ResetNativeActivityObservation();
            _consecutiveFailures = 0;
            _signedMovementSpeedMps = 0d;
            _focusStopApplied = false;
            _groundFollowing = false;
            _groundHeightCalibrated = false;
            _groundHeightOffset = 0d;
            _lastGroundHeight = null;
            lock (_inputSync)
            {
                _pressedKeys.Clear();
            }

            // Clear local state first so the UI/HUD can always leave RP mode.
            // The plugin keeps ownership until it confirms the native driver
            // restoration, which lets us retry transient OMSI write failures.
            StateChanged?.Invoke(null);
            SetStatus(reason);

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                PluginBridgeMessage? release = null;
                Exception? releaseException = null;

                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        release = await OmsiPluginBridgeRelay.ReleaseRoleplayCharacterAsync(
                            instanceId,
                            playerId,
                            attempt == 1
                                ? cancellationToken
                                : CancellationToken.None);
                        releaseException = null;

                        if (release?.Success == true)
                        {
                            break;
                        }

                        if (string.Equals(
                                release?.ErrorCode,
                                "driver-pointer-stale",
                                StringComparison.Ordinal))
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        releaseException = ex;
                    }

                    if (attempt < 3)
                    {
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(120),
                            CancellationToken.None);
                    }
                }

                if (release?.Success != true)
                {
                    SetStatus(
                        release?.ErrorCode ?? "roleplay-release-failed",
                        release?.ErrorMessage ??
                        releaseException?.Message ??
                        "The Plugin Bridge could not confirm restoring the OMSI driver after leaving roleplay mode.",
                        isError: true);
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
                _signedMovementSpeedMps = 0d;
                _lastTickUtc = DateTimeOffset.UtcNow;

                // Key-up messages may happen after OMSI loses focus and are then
                // intentionally ignored by the keyboard hook. Clear the entire
                // RP key set so returning to OMSI can never resume stale motion.
                lock (_inputSync)
                {
                    _pressedKeys.Clear();
                }

                if (!_focusStopApplied)
                {
                    var stopped = current with
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        SpeedMps = 0d,
                        Activity = RoleplayCharacterActivity.Idle
                    };

                    var stopResult = await OmsiPluginBridgeRelay.UpdateRoleplayCharacterAsync(
                        instanceId,
                        stopped,
                        MultiplayerSettingsStore.Load().DisplayName);

                    if (sessionGeneration != Volatile.Read(ref _sessionGeneration) ||
                        !string.Equals(instanceId, _instanceId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (stopResult?.Success != true)
                    {
                        _consecutiveFailures++;
                        if (_consecutiveFailures >= 3)
                        {
                            await StopAsync(
                                stopResult?.ErrorCode ??
                                "roleplay-focus-stop-failed");
                        }

                        return;
                    }

                    _focusStopApplied = true;
                    _consecutiveFailures = 0;
                    UpdateNativeAnimationDiagnostics(stopResult, commandedSpeedMps: 0d);
                    _state = stopped with
                    {
                        LocalX = stopResult.LocalX ?? stopped.LocalX,
                        LocalY = stopResult.LocalY ?? stopped.LocalY,
                        LocalZ = stopResult.LocalZ ?? stopped.LocalZ,
                        HeadingDegrees = stopResult.HeadingDegrees ?? stopped.HeadingDegrees,
                        SpeedMps = 0d,
                        Activity = RoleplayCharacterActivity.Idle
                    };

                    StateChanged?.Invoke(_state);
                    EmitNetworkState(_state);
                    SetStatus("roleplay-paused-focus-loss");
                }

                return;
            }

            if (_focusStopApplied)
            {
                _focusStopApplied = false;
                SetStatus("roleplay-active");
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

            var direction = forward == backward ? 0d : forward ? 1d : -1d;
            var targetVelocity = direction switch
            {
                > 0d => running ? RunSpeedMps : WalkSpeedMps,
                < 0d => -BackwardSpeedMps,
                _ => 0d
            };

            var changingDirection =
                Math.Abs(_signedMovementSpeedMps) > 0.01d &&
                Math.Abs(targetVelocity) > 0.01d &&
                Math.Sign(_signedMovementSpeedMps) != Math.Sign(targetVelocity);
            var slowingDown =
                Math.Abs(targetVelocity) < Math.Abs(_signedMovementSpeedMps) ||
                changingDirection;
            var acceleration = slowingDown
                ? MovementBrakingMps2
                : MovementAccelerationMps2;

            _signedMovementSpeedMps = MoveTowards(
                _signedMovementSpeedMps,
                changingDirection ? 0d : targetVelocity,
                acceleration * deltaSeconds);

            if (!changingDirection &&
                Math.Abs(_signedMovementSpeedMps - targetVelocity) > 0.0001d)
            {
                _signedMovementSpeedMps = MoveTowards(
                    _signedMovementSpeedMps,
                    targetVelocity,
                    MovementAccelerationMps2 * deltaSeconds);
            }

            if (Math.Abs(_signedMovementSpeedMps) < 0.005d)
            {
                _signedMovementSpeedMps = 0d;
            }

            var speed = Math.Abs(_signedMovementSpeedMps);
            var heading = current.HeadingDegrees;
            if (left ^ right)
            {
                var turnSpeed = speed > 0.15d
                    ? MovingTurnSpeedDegreesPerSecond
                    : StandingTurnSpeedDegreesPerSecond;
                heading += (right ? 1d : -1d) *
                           turnSpeed *
                           deltaSeconds;
                heading = NormalizeHeading(heading);
            }

            var x = current.LocalX;
            var y = current.LocalY;
            if (speed > 0.005d)
            {
                var radians = heading * Math.PI / 180d;
                var signedDistance = _signedMovementSpeedMps * deltaSeconds;
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
                    _signedMovementSpeedMps = 0d;
                    speed = 0d;
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

            var activity = speed <= 0.05d
                ? RoleplayCharacterActivity.Idle
                : _signedMovementSpeedMps > WalkSpeedMps * 1.15d
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
            UpdateNativeAnimationDiagnostics(result, updated.SpeedMps);
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

    private void UpdateNativeAnimationDiagnostics(
        PluginBridgeMessage? result,
        double commandedSpeedMps)
    {
        if (result?.CharacterAiMode is not int aiMode ||
            result.CharacterAiModeEx is not int aiModeEx ||
            result.CharacterAiSubMode is not int aiSubMode ||
            result.CharacterSollSpeedMps is not double sollSpeed ||
            result.CharacterActSpeedMps is not double actSpeed ||
            result.CharacterLastMovedDistanceMeters is not double lastMovedDistance ||
            result.CharacterAnimationState is not double animationState ||
            aiMode is < 0 or > byte.MaxValue ||
            aiModeEx is < 0 or > byte.MaxValue ||
            aiSubMode is < 0 or > byte.MaxValue ||
            !double.IsFinite(sollSpeed) ||
            !double.IsFinite(actSpeed) ||
            !double.IsFinite(lastMovedDistance) ||
            !double.IsFinite(animationState))
        {
            _nativeAnimationDiagnostics = null;
            return;
        }

        var activityLeg = NormalizeOptionalByte(result.CharacterActivityLegRaw);
        var activityArmUmbrella =
            NormalizeOptionalByte(result.CharacterActivityArmUmbrellaRaw);
        var activityArmKi = NormalizeOptionalByte(result.CharacterActivityArmKiRaw);
        var activityHeadKi = NormalizeOptionalByte(result.CharacterActivityHeadKiRaw);

        _nativeAnimationDiagnostics = new RoleplayNativeAnimationDiagnostics(
            aiMode,
            aiModeEx,
            aiSubMode,
            sollSpeed,
            actSpeed,
            lastMovedDistance,
            animationState,
            activityLeg,
            activityArmUmbrella,
            activityArmKi,
            activityHeadKi);

        UpdateNativeActivityObservation(
            activityLeg,
            activityArmUmbrella,
            activityArmKi,
            activityHeadKi,
            commandedSpeedMps);
    }

    private void UpdateNativeActivityObservation(
        int? activityLeg,
        int? activityArmUmbrella,
        int? activityArmKi,
        int? activityHeadKi,
        double commandedSpeedMps)
    {
        if (activityLeg is null &&
            activityArmUmbrella is null &&
            activityArmKi is null &&
            activityHeadKi is null)
        {
            _nativeActivityObservation = null;
            _lastNativeActivitySignature = null;
            return;
        }

        var signature = (
            Leg: activityLeg,
            ArmUmbrella: activityArmUmbrella,
            ArmKi: activityArmKi,
            HeadKi: activityHeadKi);
        var changedThisFrame =
            _lastNativeActivitySignature is { } previous &&
            previous != signature;
        var moving = double.IsFinite(commandedSpeedMps) &&
                     commandedSpeedMps > 0.01d;

        _nativeActivitySamples++;
        if (moving)
        {
            _nativeActivityMovingSamples++;
        }

        if (changedThisFrame)
        {
            _nativeActivityTransitions++;
            if (moving)
            {
                _nativeActivityMovingTransitions++;
            }

            _nativeActivityLastTransitionAtUtc = DateTimeOffset.UtcNow;
        }

        _lastNativeActivitySignature = signature;
        _nativeActivityObservation = new RoleplayNativeActivityObservation(
            _nativeActivitySamples,
            _nativeActivityMovingSamples,
            _nativeActivityTransitions,
            _nativeActivityMovingTransitions,
            changedThisFrame,
            _nativeActivityLastTransitionAtUtc);
    }

    private void ResetNativeActivityObservation()
    {
        _nativeActivityObservation = null;
        _lastNativeActivitySignature = null;
        _nativeActivitySamples = 0;
        _nativeActivityMovingSamples = 0;
        _nativeActivityTransitions = 0;
        _nativeActivityMovingTransitions = 0;
        _nativeActivityLastTransitionAtUtc = null;
    }

    private static double MoveTowards(
        double current,
        double target,
        double maxDelta)
    {
        if (!double.IsFinite(current) ||
            !double.IsFinite(target) ||
            !double.IsFinite(maxDelta) ||
            maxDelta <= 0d)
        {
            return target;
        }

        var delta = target - current;
        if (Math.Abs(delta) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(delta) * maxDelta;
    }

    private static int? NormalizeOptionalByte(int? value) =>
        value is >= byte.MinValue and <= byte.MaxValue
            ? value
            : null;

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
