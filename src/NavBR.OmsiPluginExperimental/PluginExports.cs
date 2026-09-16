using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NavBR.OmsiPluginExperimental;

public static class PluginExports
{
    private static readonly object LogSync = new();
    private static DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;
    private static long _systemVariableCallbacks;

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static string LogPath => Path.Combine(LogDirectory, "navbr-plugin.log");

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(PluginStart))]
    public static void PluginStart(IntPtr owner)
    {
        try
        {
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
            PluginBridgeClient.Stop();
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
        // O bridge pode receber estado remoto, mas esta fase ainda não escreve no OMSI.
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
            // write is handed off here. Process only one per callback so a burst
            // cannot stall the simulator frame for an unbounded amount of time.
            OmsiThreadCommandQueue.Drain(
                1,
                PluginBridgeClient.QueueCommandResult);

            var now = DateTimeOffset.UtcNow;
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
            var staleRemoved = PluginBridgeClient.PruneStaleRemoteStates();
            var remote = PluginBridgeClient.LatestRemoteState;
            var remoteSummary = remote is null
                ? "remote=none"
                : $"remote={remote.PlayerId} map={remote.MapName ?? "-"} pos=({remote.X:F2},{remote.Y:F2},{remote.Z:F2}) heading={remote.HeadingDegrees:F1} speed={remote.SpeedKph:F1}";

            PluginBridgeClient.ReportRuntimeStatus(
                callbacks,
                variableIndex,
                staleRemoved);

            Log(
                $"heartbeat systemVar={variableIndex} omsiTime={omsiTime:F3} " +
                $"callbacks={callbacks} " +
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
