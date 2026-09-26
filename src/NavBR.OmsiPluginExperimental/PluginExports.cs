using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NavBR.OmsiPluginExperimental;

public static class PluginExports
{
    private const long VehicleVariableFreshnessMs = 1_000;

    private static DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;
    private static DateTimeOffset _lastStatusReport = DateTimeOffset.MinValue;
    private static long _systemVariableCallbacks;
    private static float _pluginVelocityKph = float.NaN;
    private static int _stopRequested;
    private static long _lastVelocityTickMs;
    private static long _lastStopRequestTickMs;
    private static long _lastOmsiWorkTickMs;
    private static float _cabinTemperatureC = float.NaN;
    private static int _passengerCount = -1;
    private static int _scheduleActive = -1;
    private static float _simulationTime = float.NaN;
    private static int _simulationDay = -1;
    private static int _simulationMonth = -1;
    private static int _simulationYear = -1;
    private static int _simulationPaused = -1;
    private static readonly object TelematrixStringSync = new();
    private static string? _ibisLineCourse;
    private static string? _ibisRouteCode;
    private static string? _ibisTerminusName;
    private static string? _ibisDelayMinutes;
    private static string? _ibisDelaySeconds;
    private static string? _ibisDelayState;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(PluginStart))]
    public static void PluginStart(IntPtr owner)
    {
        try
        {
            _lastHeartbeat = DateTimeOffset.MinValue;
            _lastStatusReport = DateTimeOffset.MinValue;
            Interlocked.Exchange(ref _systemVariableCallbacks, 0);
            Interlocked.Exchange(ref _lastOmsiWorkTickMs, 0);
            PluginLogWriter.Start();
            Volatile.Write(ref _pluginVelocityKph, float.NaN);
            Volatile.Write(ref _stopRequested, 0);
            Interlocked.Exchange(ref _lastVelocityTickMs, 0);
            Interlocked.Exchange(ref _lastStopRequestTickMs, 0);
            ResetTelematrixState();
            PhysicalVehicleMotionController.Clear();
            PhysicalVehicleLifecycleSupervisor.ClearManagedState();
            Log($"PluginStart owner=0x{owner.ToInt64():X} arch={RuntimeInformation.ProcessArchitecture} deployment=native-aot");
            PluginBridgeClient.Start(Log);
        }
        catch (Exception ex)
        {
            Log($"PluginStart erro: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(PluginFinalize))]
    public static void PluginFinalize()
    {
        try
        {
            RoleplayCharacterBackend.ReleaseAllBestEffort();
            PhysicalVehicleBackend.MarkAllOwnedVehiclesForRemoval();
            PhysicalVehicleLifecycleSupervisor.ClearManagedState();
            PluginBridgeClient.Stop();
            Volatile.Write(ref _pluginVelocityKph, float.NaN);
            Volatile.Write(ref _stopRequested, 0);
            Interlocked.Exchange(ref _lastVelocityTickMs, 0);
            Interlocked.Exchange(ref _lastStopRequestTickMs, 0);
            ResetTelematrixState();
            Log($"PluginFinalize callbacks={Interlocked.Read(ref _systemVariableCallbacks)}");
            PluginLogWriter.Stop();
        }
        catch (Exception ex)
        {
            Log($"PluginFinalize erro: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessVariable))]
    public static void AccessVariable(
        ushort variableIndex,
        IntPtr value,
        IntPtr writeValue)
    {
        // Read-only hardware telemetry. Never write through writeValue here.
        if (value == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var variableValue = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(value));
            if (!float.IsFinite(variableValue))
            {
                return;
            }

            var tick = Environment.TickCount64;
            switch (variableIndex)
            {
                // [varlist] index 0 = Velocity (km/h).
                case 0:
                    Volatile.Write(ref _pluginVelocityKph, Math.Abs(variableValue));
                    Interlocked.Exchange(ref _lastVelocityTickMs, tick);
                    break;

                // [varlist] index 1 = haltewunsch. OMSI buses conventionally
                // expose the active stop request as a positive value.
                case 1:
                    Volatile.Write(ref _stopRequested, variableValue > 0.5f ? 1 : 0);
                    Interlocked.Exchange(ref _lastStopRequestTickMs, tick);
                    break;

                // Telematrix-compatible operational fields.
                case 2: // Cabinair_Temp
                    if (variableValue is > -80f and < 120f)
                    {
                        Volatile.Write(ref _cabinTemperatureC, variableValue);
                    }
                    break;
                case 3: // humans_count
                    if (variableValue is >= 0f and <= 2000f)
                    {
                        Volatile.Write(ref _passengerCount, (int)MathF.Round(variableValue));
                    }
                    break;
                case 4: // schedule_active
                    Volatile.Write(ref _scheduleActive, variableValue > 0.5f ? 1 : 0);
                    break;
                case 7: // IBIS_LinieKurs
                    lock (TelematrixStringSync)
                    {
                        _ibisLineCourse = variableValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    break;
                case 8: // IBIS_Route
                    lock (TelematrixStringSync)
                    {
                        _ibisRouteCode = variableValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    break;
            }
        }
        catch
        {
            // A malformed/unsupported bus variable must never affect OMSI.
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessTrigger))]
    public static void AccessTrigger(
        ushort triggerIndex,
        IntPtr triggerScript)
    {
        // Nenhum trigger do OMSI é acionado nesta fase.
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessStringVariable))]
    public static void AccessStringVariable(
        ushort variableIndex,
        IntPtr firstCharacterAddress,
        IntPtr writeValue)
    {
        if (firstCharacterAddress == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var text = ReadOmsiAnsiString(firstCharacterAddress, 128);
            lock (TelematrixStringSync)
            {
                switch (variableIndex)
                {
                    case 2: _ibisLineCourse = NullIfWhiteSpace(text); break; // IBIS_Complex_Line
                    case 3: _ibisDelayMinutes = NullIfWhiteSpace(text); break;
                    case 4: _ibisDelaySeconds = NullIfWhiteSpace(text); break;
                    case 5: _ibisDelayState = NullIfWhiteSpace(text); break;
                    case 6: _ibisTerminusName = NullIfWhiteSpace(text); break;
                }
            }
        }
        catch
        {
            // Optional bus string variables must never affect OMSI.
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessSystemVariable))]
    public static void AccessSystemVariable(
        ushort variableIndex,
        IntPtr value,
        IntPtr writeValue)
    {
        try
        {
            Interlocked.Increment(ref _systemVariableCallbacks);

            if (value != IntPtr.Zero)
            {
                try
                {
                    var systemValue =
                        BitConverter.Int32BitsToSingle(Marshal.ReadInt32(value));
                    if (float.IsFinite(systemValue))
                    {
                        switch (variableIndex)
                        {
                            case 0:
                                Volatile.Write(ref _simulationTime, systemValue);
                                break;
                            case 1:
                                Volatile.Write(ref _simulationDay, (int)MathF.Round(systemValue));
                                break;
                            case 2:
                                Volatile.Write(ref _simulationMonth, (int)MathF.Round(systemValue));
                                break;
                            case 3:
                                Volatile.Write(ref _simulationYear, (int)MathF.Round(systemValue));
                                break;
                            case 4:
                                Volatile.Write(ref _simulationPaused, systemValue > 0.5f ? 1 : 0);
                                break;
                        }
                    }
                }
                catch
                {
                }
            }

            // AccessSystemVariable can be called several times inside one OMSI
            // render/update frame. Never drain the command queue on every
            // variable callback: that multiplies native work on OMSI's main
            // thread. The guard admits one bounded work slice at a time.
            var workTick = Environment.TickCount64;
            var activePhysicalVehicles = PhysicalVehicleMotionController.ActiveCount;
            var minimumWorkIntervalMs = activePhysicalVehicles switch
            {
                >= 9 => 33L,
                >= 5 => 24L,
                _ => 16L
            };

            if (TryAcquireOmsiWorkSlot(workTick, minimumWorkIntervalMs))
            {
                var maxCommands = activePhysicalVehicles switch
                {
                    >= 9 => 2,
                    >= 5 => 3,
                    _ => 4
                };

                OmsiThreadCommandQueue.DrainFrame(
                    maxCommands,
                    PluginBridgeClient.QueueCommandResult);

                // Keep physical ownership alive inside the OMSI callback even
                // if the desktop misses a response or a pointer has to be
                // recreated. At most one native retry is performed per tick.
                PhysicalVehicleLifecycleSupervisor.Tick();

                // Remote buses receive network targets at a lower cadence than
                // OMSI's callback loop. The motion controller has its own
                // adaptive rate and skips settled vehicles entirely.
                PhysicalVehicleMotionController.Tick();

                // RP targets come from the desktop at ~20 Hz, but OMSI can
                // restore human/driver state inside the frames between bridge
                // commands. Reassert the last confirmed target on this same
                // OMSI callback loop so movement remains physically visible.
                RoleplayCharacterBackend.Tick();
            }

            var now = DateTimeOffset.UtcNow;
            var staleRemoved = 0;
            if (now - _lastStatusReport >= TimeSpan.FromMilliseconds(200))
            {
                _lastStatusReport = now;
                staleRemoved = PluginBridgeClient.PruneStaleRemoteStates();

                var tick = Environment.TickCount64;
                var velocityAge = AgeMilliseconds(tick, Interlocked.Read(ref _lastVelocityTickMs));
                var stopAge = AgeMilliseconds(tick, Interlocked.Read(ref _lastStopRequestTickMs));
                var pluginVelocity = Volatile.Read(ref _pluginVelocityKph);

                var speedKph = velocityAge <= VehicleVariableFreshnessMs && float.IsFinite(pluginVelocity)
                    ? (double?)Math.Abs(pluginVelocity)
                    : null;
                bool? stopRequested = stopAge <= VehicleVariableFreshnessMs
                    ? Volatile.Read(ref _stopRequested) != 0
                    : null;

                string? ibisLineCourse;
                string? ibisRouteCode;
                string? ibisTerminusName;
                string? ibisDelayMinutes;
                string? ibisDelaySeconds;
                string? ibisDelayState;
                lock (TelematrixStringSync)
                {
                    ibisLineCourse = _ibisLineCourse;
                    ibisRouteCode = _ibisRouteCode;
                    ibisTerminusName = _ibisTerminusName;
                    ibisDelayMinutes = _ibisDelayMinutes;
                    ibisDelaySeconds = _ibisDelaySeconds;
                    ibisDelayState = _ibisDelayState;
                }

                var cabinTemperature = Volatile.Read(ref _cabinTemperatureC);
                var passengers = Volatile.Read(ref _passengerCount);
                var schedule = Volatile.Read(ref _scheduleActive);
                var simulationTime = Volatile.Read(ref _simulationTime);
                var simulationDay = Volatile.Read(ref _simulationDay);
                var simulationMonth = Volatile.Read(ref _simulationMonth);
                var simulationYear = Volatile.Read(ref _simulationYear);
                var simulationPaused = Volatile.Read(ref _simulationPaused);

                PluginBridgeClient.ReportRuntimeStatus(
                    Interlocked.Read(ref _systemVariableCallbacks),
                    variableIndex,
                    staleRemoved,
                    speedKph,
                    stopRequested,
                    float.IsFinite(cabinTemperature) ? cabinTemperature : null,
                    passengers >= 0 ? passengers : null,
                    schedule >= 0 ? schedule != 0 : null,
                    float.IsFinite(simulationTime) ? simulationTime : null,
                    simulationDay > 0 ? simulationDay : null,
                    simulationMonth > 0 ? simulationMonth : null,
                    simulationYear > 0 ? simulationYear : null,
                    simulationPaused >= 0 ? simulationPaused != 0 : null,
                    ibisLineCourse,
                    ibisRouteCode,
                    ibisTerminusName,
                    ibisDelayMinutes,
                    ibisDelaySeconds,
                    ibisDelayState);
            }

            // Keep the verbose file heartbeat sparse. Hardware/status delivery is
            // handled above at 5 Hz and is intentionally independent from logging.
            if (now - _lastHeartbeat < TimeSpan.FromSeconds(5))
            {
                return;
            }

            _lastHeartbeat = now;

            float omsiTime = float.NaN;
            if (value != IntPtr.Zero)
            {
                try
                {
                    omsiTime = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(value));
                }
                catch
                {
                    // Diagnóstico é best-effort e nunca deve interromper o OMSI.
                }
            }

            var callbacks = Interlocked.Read(ref _systemVariableCallbacks);
            var remote = PluginBridgeClient.LatestRemoteState;
            var remoteSummary = remote is null
                ? "remote=none"
                : $"remote={remote.PlayerId} map={remote.MapName ?? "-"} pos=({remote.X:F2},{remote.Y:F2},{remote.Z:F2}) heading={remote.HeadingDegrees:F1} speed={remote.SpeedKph:F1}";

            var heartbeatTick = Environment.TickCount64;
            var localVelocity = Volatile.Read(ref _pluginVelocityKph);
            var velocityFresh = AgeMilliseconds(
                heartbeatTick,
                Interlocked.Read(ref _lastVelocityTickMs)) <= VehicleVariableFreshnessMs;
            var stopFresh = AgeMilliseconds(
                heartbeatTick,
                Interlocked.Read(ref _lastStopRequestTickMs)) <= VehicleVariableFreshnessMs;
            var localSpeedSummary = velocityFresh && float.IsFinite(localVelocity)
                ? localVelocity.ToString("F1")
                : "unsupported/stale";
            var stopSummary = stopFresh
                ? (Volatile.Read(ref _stopRequested) != 0 ? "1" : "0")
                : "unsupported/stale";

            var hostVehiclePointer = OmsiNativeInterop.GetPlayerVehiclePointer();
            var hostTileIndex = hostVehiclePointer != 0
                ? OmsiNativeInterop.ReadRoadVehicleTileIndex(hostVehiclePointer)
                : -1;
            var hostGridSummary = "hostPhysicalGrid=unavailable";
            if (OmsiNativeInterop.ReadPlayerVehicleGrid(
                    out var hostGridX,
                    out var hostGridY,
                    out var hostGridTileIndex) == 1)
            {
                hostGridSummary =
                    $"hostPhysicalGrid={hostGridX},{hostGridY} hostGridKachel={hostGridTileIndex}";
            }

            Log(
                $"heartbeat systemVar={variableIndex} omsiTime={omsiTime:F3} " +
                $"callbacks={callbacks} " +
                $"localVelocityKph={localSpeedSummary} stopRequested={stopSummary} " +
                $"remoteCount={PluginBridgeClient.RemoteVehicleCount} " +
                $"compatibleRemoteCount={PluginBridgeClient.CompatibleRemoteVehicleCount} " +
                $"trafficCount={PluginBridgeClient.TrafficVehicleCount} " +
                $"trafficAuthority={PluginBridgeClient.TrafficAuthorityPlayerId ?? "-"} " +
                $"physicalQueue={OmsiThreadCommandQueue.Count} " +
                $"physicalLifecycle={PhysicalVehicleLifecycleSupervisor.Summary} " +
                $"hostKachel={hostTileIndex} {hostGridSummary} " +
                $"staleRemoved={staleRemoved} {remoteSummary}");
        }
        catch (Exception ex)
        {
            Log($"AccessSystemVariable erro: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ResetTelematrixState()
    {
        Volatile.Write(ref _cabinTemperatureC, float.NaN);
        Volatile.Write(ref _passengerCount, -1);
        Volatile.Write(ref _scheduleActive, -1);
        Volatile.Write(ref _simulationTime, float.NaN);
        Volatile.Write(ref _simulationDay, -1);
        Volatile.Write(ref _simulationMonth, -1);
        Volatile.Write(ref _simulationYear, -1);
        Volatile.Write(ref _simulationPaused, -1);
        lock (TelematrixStringSync)
        {
            _ibisLineCourse = null;
            _ibisRouteCode = null;
            _ibisTerminusName = null;
            _ibisDelayMinutes = null;
            _ibisDelaySeconds = null;
            _ibisDelayState = null;
        }
    }

    private static string ReadOmsiAnsiString(IntPtr address, int maxChars)
    {
        var bytes = new List<byte>(Math.Min(maxChars, 128));
        for (var index = 0; index < maxChars; index++)
        {
            var value = Marshal.ReadByte(address, index);
            if (value == 0)
            {
                break;
            }
            bytes.Add(value);
        }

        return bytes.Count == 0
            ? string.Empty
            : System.Text.Encoding.Latin1.GetString(bytes.ToArray()).Trim();
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryAcquireOmsiWorkSlot(long nowTick, long minimumIntervalMs)
    {
        while (true)
        {
            var previous = Interlocked.Read(ref _lastOmsiWorkTickMs);
            if (previous > 0 &&
                nowTick >= previous &&
                nowTick - previous < minimumIntervalMs)
            {
                return false;
            }

            if (Interlocked.CompareExchange(
                    ref _lastOmsiWorkTickMs,
                    nowTick,
                    previous) == previous)
            {
                return true;
            }
        }
    }

    private static long AgeMilliseconds(long nowTick, long lastTick)
    {
        if (lastTick <= 0 || nowTick < lastTick)
        {
            return long.MaxValue;
        }

        return nowTick - lastTick;
    }

    private static void Log(string message) =>
        PluginLogWriter.Enqueue(message);
}