using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

public static class PluginExports
{
    private static long _systemCallbacks;
    private static long _lastFrameTickMs;
    private static double _averageFrameMs;
    private static double _lastFrameMs;
    private static double _peakFrameMs;
    private static long _stallCount;
    private static int _pressureLevel;

    private static float _speedKph = float.NaN;
    private static int _stopRequested = -1;
    private static float _cabinTemperatureC = float.NaN;
    private static int _passengerCount = -1;
    private static int _scheduleActive = -1;
    private static float _simulationTime = float.NaN;
    private static int _simulationDay = -1;
    private static int _simulationMonth = -1;
    private static int _simulationYear = -1;
    private static int _simulationPaused = -1;
    private static string? _ibisLineCourse;
    private static string? _ibisRouteCode;
    private static string? _ibisTerminusName;
    private static string? _ibisDelayMinutes;
    private static string? _ibisDelaySeconds;
    private static string? _ibisDelayState;

    internal static string PerformanceProfile { get; private set; } = "auto";

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(PluginStart))]
    public static void PluginStart(IntPtr owner)
    {
        ResetState();
        OpenOmsiBridge.Start();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(PluginFinalize))]
    public static void PluginFinalize()
    {
        OpenOmsiBridge.Stop();
        ResetState();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(AccessVariable))]
    public static void AccessVariable(ushort index, IntPtr value, IntPtr writeValue)
    {
        SetReadOnly(writeValue);
        if (value == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var number = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(value));
            if (!float.IsFinite(number))
            {
                return;
            }

            switch (index)
            {
                case 0:
                    Volatile.Write(ref _speedKph, Math.Abs(number));
                    break;
                case 1:
                    Volatile.Write(ref _stopRequested, number > 0.5f ? 1 : 0);
                    break;
                case 2 when number is > -80f and < 120f:
                    Volatile.Write(ref _cabinTemperatureC, number);
                    break;
                case 3 when number is >= 0f and <= 2000f:
                    Volatile.Write(ref _passengerCount, (int)MathF.Round(number));
                    break;
                case 4:
                    Volatile.Write(ref _scheduleActive, number > 0.5f ? 1 : 0);
                    break;
                case 7:
                    Volatile.Write(ref _ibisLineCourse, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 8:
                    Volatile.Write(ref _ibisRouteCode, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(AccessSystemVariable))]
    public static void AccessSystemVariable(ushort index, IntPtr value, IntPtr writeValue)
    {
        SetReadOnly(writeValue);
        if (value == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Interlocked.Increment(ref _systemCallbacks);
            var number = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(value));
            if (!float.IsFinite(number))
            {
                return;
            }

            switch (index)
            {
                case 0:
                    Volatile.Write(ref _simulationTime, number);
                    ObserveFrame();
                    OpenOmsiBridge.QueueStatus(BuildStatus());
                    break;
                case 1:
                    Volatile.Write(ref _simulationDay, (int)MathF.Round(number));
                    break;
                case 2:
                    Volatile.Write(ref _simulationMonth, (int)MathF.Round(number));
                    break;
                case 3:
                    Volatile.Write(ref _simulationYear, (int)MathF.Round(number));
                    break;
                case 4:
                    Volatile.Write(ref _simulationPaused, number > 0.5f ? 1 : 0);
                    break;
            }
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(AccessStringVariable))]
    public static void AccessStringVariable(ushort index, IntPtr text, IntPtr writeValue)
    {
        SetReadOnly(writeValue);
        if (text == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var value = Marshal.PtrToStringUni(text);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var safe = value.Trim();
            switch (index)
            {
                case 2: Volatile.Write(ref _ibisLineCourse, safe); break;
                case 3: Volatile.Write(ref _ibisDelayMinutes, safe); break;
                case 4: Volatile.Write(ref _ibisDelaySeconds, safe); break;
                case 5: Volatile.Write(ref _ibisDelayState, safe); break;
                case 6: Volatile.Write(ref _ibisTerminusName, safe); break;
            }
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(AccessTrigger))]
    public static void AccessTrigger(ushort index, IntPtr active)
    {
        if (active != IntPtr.Zero)
        {
            Marshal.WriteByte(active, 0);
        }
    }

    internal static void SetPerformanceProfile(string? profile)
    {
        PerformanceProfile = profile?.Trim().ToLowerInvariant() switch
        {
            "stability" => "stability",
            "multiplayer" => "multiplayer",
            "quality" => "quality",
            "diagnostics" => "diagnostics",
            _ => "auto"
        };
    }

    internal static PluginBridgeMessage BuildStatus()
    {
        var paused = ReadBool(_simulationPaused);
        var snapshot = OpenOmsiLuaSnapshotReader.ReadLatest(
            allowPausedStale: paused == true);
        var ibisLine = Volatile.Read(ref _ibisLineCourse);
        var ibisRoute = Volatile.Read(ref _ibisRouteCode);
        var ibisTerminus = Volatile.Read(ref _ibisTerminusName);
        var nearbyVehicles = snapshot?.NearbyVehicles ?? [];
        var nearbyAiCount = nearbyVehicles.Count(vehicle =>
            string.Equals(vehicle.Kind, "ai", StringComparison.Ordinal));
        var nearbyPlayerCount = nearbyVehicles.Count(vehicle =>
            string.Equals(vehicle.Kind, "player", StringComparison.Ordinal));
        var navigation = OpenOmsiNavigationRuntime.Build(snapshot);
        var hud = OpenOmsiHudState.Current;
        var route = OpenOmsiRouteRuntime.Build(snapshot);

        return new PluginBridgeMessage(
            PluginBridgeProtocol.PluginStatus,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            ComponentVersion: typeof(PluginExports).Assembly.GetName().Version?.ToString(),
            MapName: snapshot?.MapName,
            TimestampUnixMilliseconds:
                snapshot?.CapturedAtUtc.ToUnixTimeMilliseconds() ??
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            X: snapshot?.X,
            Y: snapshot?.Y,
            Z: snapshot?.Z,
            HeadingDegrees: snapshot?.HeadingDegrees,
            SpeedKph: ReadFinite(_speedKph) ?? snapshot?.ReportedSpeedKph,
            IsInGame: snapshot is null
                ? null
                : snapshot.HasPosition && !snapshot.OnFoot,
            SystemVariableCallbacks: Interlocked.Read(ref _systemCallbacks),
            RemoteVehicleCount: 0,
            CompatibleRemoteVehicleCount: 0,
            StaleRemovedCount: 0,
            LastSystemVariableIndex: 0,
            VehicleName: snapshot?.VehicleName,
            Line: snapshot?.Line ?? ibisLine,
            Route: ibisRoute ?? snapshot?.Tour,
            NextStopName: snapshot?.NextStop,
            DestinationName: snapshot?.Terminus ?? ibisTerminus,
            DelaySeconds: snapshot?.DelaySeconds,
            StopRequested: ReadBool(_stopRequested),
            CabinTemperatureC: ReadFinite(_cabinTemperatureC),
            PassengerCount: ReadInt(_passengerCount),
            ScheduleActive: ReadBool(_scheduleActive),
            SimulationTime: ReadFinite(_simulationTime),
            SimulationDay: ReadInt(_simulationDay),
            SimulationMonth: ReadInt(_simulationMonth),
            SimulationYear: ReadInt(_simulationYear),
            SimulationPaused: paused,
            IbisLineCourse: ibisLine,
            IbisRouteCode: ibisRoute,
            IbisTerminusName: ibisTerminus,
            IbisDelayMinutes: Volatile.Read(ref _ibisDelayMinutes),
            IbisDelaySeconds: Volatile.Read(ref _ibisDelaySeconds),
            IbisDelayState: Volatile.Read(ref _ibisDelayState),
            PluginPressureLevel: Volatile.Read(ref _pressureLevel),
            PluginAverageFrameIntervalMilliseconds: _averageFrameMs > 0d ? _averageFrameMs : null,
            PluginLastFrameIntervalMilliseconds: _lastFrameMs > 0d ? _lastFrameMs : null,
            PluginPeakFrameIntervalMilliseconds: _peakFrameMs > 0d ? _peakFrameMs : null,
            PluginFrameStallCount: Interlocked.Read(ref _stallCount),
            PluginMinimumWorkIntervalMilliseconds: StatusIntervalMilliseconds,
            PluginMaxCommandsPerSlice: 0,
            PerformanceProfile: PerformanceProfile,
            ExperimentalWritesEnabled: false,
            OpenOmsiView: snapshot?.View,
            OpenOmsiOnFoot: snapshot?.OnFoot,
            OpenOmsiMultiplayer: snapshot?.Multiplayer,
            OpenOmsiTrafficCount: snapshot?.TrafficCount,
            OpenOmsiNearbyAiCount: nearbyAiCount,
            OpenOmsiNearbyPlayerCount: nearbyPlayerCount,
            OpenOmsiNextStopArrival: snapshot?.NextStopArrival,
            OpenOmsiNextStopDeparture: snapshot?.NextStopDeparture,
            OpenOmsiTripIndex: snapshot?.TripIndex,
            OpenOmsiTripsCount: snapshot?.TripsCount,
            OpenOmsiNextStopNumber: snapshot?.NextStopNumber,
            OpenOmsiNearbyVehicles: nearbyVehicles,
            OpenOmsiSuggestedMapRadiusMeters: navigation.SuggestedMapRadiusMeters,
            OpenOmsiCongestionLevel: navigation.CongestionLevel,
            OpenOmsiAverageNearbyTrafficSpeedKph: navigation.AverageNearbyTrafficSpeedKph,
            OpenOmsiNearbyMovingAiCount: navigation.NearbyMovingAiCount,
            OpenOmsiNearbySlowAiCount: navigation.NearbySlowAiCount,
            OpenOmsiNearbyStoppedAiCount: navigation.NearbyStoppedAiCount,
            OpenOmsiMiniMapEnabled: hud.MiniMapEnabled,
            OpenOmsiFullMapEnabled: hud.FullMapEnabled,
            OpenOmsiAutoZoomEnabled: hud.AutoZoomEnabled,
            OpenOmsiFollowVehicleEnabled: hud.FollowVehicleEnabled,
            OpenOmsiTimetableHudEnabled: hud.TimetableEnabled,
            OpenOmsiTeleMatrixEnabled: hud.TeleMatrixEnabled,
            OpenOmsiTrafficLayerEnabled: hud.TrafficEnabled,
            OpenOmsiMultiplayerLayerEnabled: hud.MultiplayerEnabled,
            OpenOmsiCongestionLayerEnabled: hud.CongestionEnabled,
            OpenOmsiRouteGuidanceEnabled: hud.RouteGuidanceEnabled,
            OpenOmsiRouteLoaded: route.RouteLoaded,
            OpenOmsiRouteKey: route.RouteKey,
            OpenOmsiRoutePointCount: route.RoutePointCount,
            OpenOmsiDistanceFromRouteMeters: route.DistanceFromRouteMeters,
            OpenOmsiOffRoute: route.OffRoute,
            OpenOmsiNearestRoutePointIndex: route.NearestRoutePointIndex,
            OpenOmsiRejoinRoutePointIndex: route.RejoinRoutePointIndex,
            OpenOmsiRejoinTargetX: route.RejoinTargetX,
            OpenOmsiRejoinTargetY: route.RejoinTargetY,
            Capabilities:
            [
                PluginBridgeProtocol.CapabilityAdvancedTelemetry,
                PluginBridgeProtocol.CapabilityPerformanceGovernor,
                PluginBridgeProtocol.CapabilityOpenOmsiStandardPlugin,
                PluginBridgeProtocol.CapabilityOpenOmsiLuaSnapshot,
                PluginBridgeProtocol.CapabilityOpenOmsiNearbyVehicles,
                PluginBridgeProtocol.CapabilityOpenOmsiTimetableContext,
                PluginBridgeProtocol.CapabilityOpenOmsiNativeOnFoot,
                PluginBridgeProtocol.CapabilityOpenOmsiNavigationRuntime,
                PluginBridgeProtocol.CapabilityOpenOmsiHudConfiguration,
                PluginBridgeProtocol.CapabilityOpenOmsiRouteRejoin
            ]);
    }

    internal static long StatusIntervalMilliseconds => PerformanceProfile switch
    {
        "diagnostics" => 100,
        "stability" => 500,
        "multiplayer" => 200,
        "quality" => 150,
        _ => Volatile.Read(ref _pressureLevel) switch
        {
            >= 3 => 750,
            2 => 500,
            1 => 300,
            _ => 200
        }
    };

    private static void ObserveFrame()
    {
        var now = Environment.TickCount64;
        var previous = Interlocked.Exchange(ref _lastFrameTickMs, now);
        if (previous <= 0 || now <= previous)
        {
            return;
        }

        var interval = now - previous;
        if (interval is <= 0 or >= 1000)
        {
            return;
        }

        _lastFrameMs = interval;
        _peakFrameMs = Math.Max(_peakFrameMs, interval);
        _averageFrameMs = _averageFrameMs <= 0d
            ? interval
            : _averageFrameMs + ((interval - _averageFrameMs) * 0.08d);
        if (interval >= 100)
        {
            Interlocked.Increment(ref _stallCount);
        }

        var pressure = _averageFrameMs switch
        {
            >= 60d => 3,
            >= 45d => 2,
            >= 36d => 1,
            _ => 0
        };
        Volatile.Write(ref _pressureLevel, pressure);
    }

    private static void SetReadOnly(IntPtr writeValue)
    {
        if (writeValue != IntPtr.Zero)
        {
            Marshal.WriteByte(writeValue, 0);
        }
    }

    private static double? ReadFinite(float value) =>
        float.IsFinite(value) ? value : null;

    private static int? ReadInt(int value) =>
        value >= 0 ? value : null;

    private static bool? ReadBool(int value) =>
        value < 0 ? null : value != 0;

    private static void ResetState()
    {
        Interlocked.Exchange(ref _systemCallbacks, 0);
        Interlocked.Exchange(ref _lastFrameTickMs, 0);
        Interlocked.Exchange(ref _stallCount, 0);
        _averageFrameMs = 0d;
        _lastFrameMs = 0d;
        _peakFrameMs = 0d;
        Volatile.Write(ref _pressureLevel, 0);
        Volatile.Write(ref _speedKph, float.NaN);
        Volatile.Write(ref _stopRequested, -1);
        Volatile.Write(ref _cabinTemperatureC, float.NaN);
        Volatile.Write(ref _passengerCount, -1);
        Volatile.Write(ref _scheduleActive, -1);
        Volatile.Write(ref _simulationTime, float.NaN);
        Volatile.Write(ref _simulationDay, -1);
        Volatile.Write(ref _simulationMonth, -1);
        Volatile.Write(ref _simulationYear, -1);
        Volatile.Write(ref _simulationPaused, -1);
        Volatile.Write(ref _ibisLineCourse, null);
        Volatile.Write(ref _ibisRouteCode, null);
        Volatile.Write(ref _ibisTerminusName, null);
        Volatile.Write(ref _ibisDelayMinutes, null);
        Volatile.Write(ref _ibisDelaySeconds, null);
        Volatile.Write(ref _ibisDelayState, null);
        PerformanceProfile = "auto";
        OpenOmsiHudState.Reset();
        OpenOmsiRouteRuntime.Clear();
        OpenOmsiHudState.Reset();
    }
}
