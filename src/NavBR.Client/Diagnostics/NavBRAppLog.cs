using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace NavBR.Client.Diagnostics;

internal static class NavBRAppLog
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    public static string LogPath => Path.Combine(DirectoryPath, "navbr.log");

    public static void StartSession()
    {
        Write("session-start", string.Join(
            " ",
            $"version={GetVersion()}",
            $"build={GetBuildVersion()}",
            $"pid={Environment.ProcessId}",
            $"arch={(Environment.Is64BitProcess ? "x64" : "x86")}",
            $"os={Sanitize(Environment.OSVersion.VersionString)}"));
    }

    public static void EndSession() => Write("session-end");

    public static void Info(string eventName, string? details = null) => Write(eventName, details);

    public static void Error(string eventName, Exception exception)
    {
        var details = $"type={exception.GetType().Name} message={Sanitize(exception.Message)}";
        Write(eventName, details);
        RemoteDiagnosticsService.Record(
            "app-error",
            "error",
            $"event={Sanitize(eventName)} {details}");

        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(LogPath, exception + Environment.NewLine + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never affect application flow.
        }
    }

    private static void Write(string eventName, string? details = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DirectoryPath);
                var line = $"[{DateTimeOffset.Now:O}] {eventName}";
                if (!string.IsNullOrWhiteSpace(details))
                {
                    line += " " + details.Trim();
                }

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never affect application flow.
        }
    }

    private static string GetVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private static string GetBuildVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        return Sanitize(
            assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? assembly?.GetName().Version?.ToString()
            ?? "unknown");
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
