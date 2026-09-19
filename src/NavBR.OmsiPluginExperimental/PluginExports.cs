using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NavBR.OmsiPluginExperimental;

public static class PluginExports
{
    private const long VehicleVariableFreshnessMs = 1_000;

    private static readonly object LogSync = new();
    private static DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;
    private static DateTimeOffset _lastStatusReport = DateTimeOffset.MinValue;
    private static long _systemVariableCallbacks;
    private static float _pluginVelocityKph = float.NaN;
    private static int _stopRequested;
    private static long _lastVelocityTickMs;
    private static long _lastStopRequestTickMs;

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static string LogPath => Path.Combine(LogDirectory, "navbr-plugin.log");

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(PluginStart))]
    public static void PluginStart(IntPtr owner)
    {
        try
        {
            _lastHeartbeat = DateTimeOffset.MinValue;
            _lastStatusReport = DateTimeOffset.MinValue;
            Volatile.Write(ref _pluginVelocityKph, float.NaN);
            Volatile.Write(ref _stopRequested, 0);
            Interlocked.Exchange(ref _lastVelocityTickMs, 0);
            Interlocked.Exchange(ref _lastStopRequestTickMs, 0);
            PhysicalVehicleMotionController.Clear();
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
            PluginBridgeClient.Stop();
            Volatile.Write(ref _pluginVelocityKph, float.NaN);
            Volatile.Write(ref _stopRequested, 0);
            Interlocked.Exchange(ref _lastVelocityTickMs, 0);
            Interlocked.Exchange(ref _lastStopRequestTickMs, 0);
            Log($"PluginFinalize callbacks={Interlocked.Read(ref _systemVariableCallbacks)}");
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
        // Mantido para cumprir a interface esperada pelo OMSI.
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

            // Commands arrive through the named-pipe worker, but every raw OMSI
            // write is handed off here. Process one arbitrary command plus a few
            // lightweight movement updates; expensive spawn/acquire work remains
            // bounded to at most one command per OMSI frame.
            OmsiThreadCommandQueue.DrainFrame(
                5,
                PluginBridgeClient.QueueCommandResult);

            // Remote buses receive network targets at a lower cadence than the
            // OMSI render/update loop. Apply interpolation here so all guarded
            // transform writes remain on OMSI's callback thread.
            PhysicalVehicleMotionController.Tick();

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

                PluginBridgeClient.ReportRuntimeStatus(
                    Interlocked.Read(ref _systemVariableCallbacks),
                    variableIndex,
                    staleRemoved,
                    speedKph,
                    stopRequested);
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

            Log(
                $"heartbeat systemVar={variableIndex} omsiTime={omsiTime:F3} " +
                $"callbacks={callbacks} " +
                $"localVelocityKph={localSpeedSummary} stopRequested={stopSummary} " +
                $"remoteCount={PluginBridgeClient.RemoteVehicleCount} " +
                $"compatibleRemoteCount={PluginBridgeClient.CompatibleRemoteVehicleCount} " +
                $"trafficCount={PluginBridgeClient.TrafficVehicleCount} " +
                $"trafficAuthority={PluginBridgeClient.TrafficAuthorityPlayerId ?? "-"} " +
                $"physicalQueue={OmsiThreadCommandQueue.Count} " +
                $"staleRemoved={staleRemoved} {remoteSummary}");
        }
        catch (Exception ex)
        {
            Log($"AccessSystemVariable erro: {ex.GetType().Name}: {ex.Message}");
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

    private static void Log(string message)
    {
        try
        {
            lock (LogSync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    LogPath,
                    $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Um erro de log nunca deve afetar a estabilidade do simulador.
        }
    }
}
