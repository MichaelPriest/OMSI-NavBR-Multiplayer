using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DNNE;

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
        Log($"PluginStart owner=0x{owner.ToInt64():X} arch={RuntimeInformation.ProcessArchitecture}");
        PluginBridgeClient.Start(Log);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(PluginFinalize))]
    public static void PluginFinalize()
    {
        PluginBridgeClient.Stop();
        Log($"PluginFinalize callbacks={Interlocked.Read(ref _systemVariableCallbacks)}");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessVariable))]
    public static void AccessVariable(
        ushort variableIndex,
        [C99Type("float*")] IntPtr value,
        [C99Type("__crt_bool*")] IntPtr writeValue)
    {
        // O bridge pode receber estado remoto, mas esta fase ainda não escreve no OMSI.
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessTrigger))]
    public static void AccessTrigger(
        ushort triggerIndex,
        [C99Type("__crt_bool*")] IntPtr triggerScript)
    {
        // Nenhum trigger do OMSI é acionado nesta fase.
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessStringVariable))]
    public static void AccessStringVariable(
        ushort variableIndex,
        [C99Type("char*")] IntPtr firstCharacterAddress,
        [C99Type("__crt_bool*")] IntPtr writeValue)
    {
        // Mantido para cumprir a interface esperada pelo OMSI.
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = nameof(AccessSystemVariable))]
    public static void AccessSystemVariable(
        ushort variableIndex,
        [C99Type("float*")] IntPtr value,
        [C99Type("__crt_bool*")] IntPtr writeValue)
    {
        Interlocked.Increment(ref _systemVariableCallbacks);

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
                omsiTime = Marshal.PtrToStructure<float>(value);
            }
            catch
            {
                // Diagnóstico é best-effort e nunca deve interromper o OMSI.
            }
        }

        var remote = PluginBridgeClient.LatestRemoteState;
        var remoteSummary = remote is null
            ? "remote=none"
            : $"remote={remote.PlayerId} map={remote.MapName ?? "-"} pos=({remote.X:F2},{remote.Y:F2},{remote.Z:F2}) heading={remote.HeadingDegrees:F1} speed={remote.SpeedKph:F1}";

        Log(
            $"heartbeat systemVar={variableIndex} omsiTime={omsiTime:F3} " +
            $"callbacks={Interlocked.Read(ref _systemVariableCallbacks)} " +
            $"remoteCount={PluginBridgeClient.RemoteVehicleCount} {remoteSummary}");
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
