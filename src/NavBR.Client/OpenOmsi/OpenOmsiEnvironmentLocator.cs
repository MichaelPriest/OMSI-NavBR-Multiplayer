using System.Diagnostics;
using System.Text.Json;

namespace NavBR.Client.OpenOmsi;

/// <summary>
/// Read-only discovery helpers for an externally managed openOMSI runtime.
/// This class never installs, removes, updates or launches openOMSI or its
/// plugins. It only discovers the running process and content roots needed by
/// NavBR for telemetry identity and gateway compatibility.
/// </summary>
internal static class OpenOmsiEnvironmentLocator
{
    public static int? GetRunningProcessId()
    {
        foreach (var process in Process.GetProcessesByName("openomsi"))
        {
            try
            {
                if (!process.HasExited)
                {
                    return process.Id;
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    public static IReadOnlyList<string> ResolveContentSearchRoots()
    {
        var roots = new List<string>();

        void AddRoot(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            try
            {
                var full = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(candidate.Trim().Trim('"')));
                if (!Directory.Exists(full) ||
                    roots.Contains(full, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }

                roots.Add(full);
            }
            catch
            {
            }
        }

        AddRoot(Environment.GetEnvironmentVariable("OMSI_CONTENT"));
        AddRoot(Environment.GetEnvironmentVariable("OMSI_ROOT"));

        var executable = ResolveRunningExecutable()
            ?? ResolveExecutableCandidate(
                Environment.GetEnvironmentVariable("OPENOMSI_BIN"))
            ?? ReadLauncherExecutable();

        if (!string.IsNullOrWhiteSpace(executable))
        {
            var binaryDirectory = Path.GetDirectoryName(executable);
            if (!string.IsNullOrWhiteSpace(binaryDirectory))
            {
                var envContent =
                    Environment.GetEnvironmentVariable("OMSI_CONTENT");
                if (!string.IsNullOrWhiteSpace(envContent))
                {
                    AddRoot(envContent);
                }
                else if (File.Exists(Path.Combine(binaryDirectory, "Omsi.exe")) &&
                         Directory.Exists(Path.Combine(binaryDirectory, "maps")))
                {
                    AddRoot(Path.Combine(binaryDirectory, "openOMSI"));
                    AddRoot(binaryDirectory);
                }
                else
                {
                    AddRoot(binaryDirectory);
                }
            }
        }

        try
        {
            var dataDirectory = GetOpenOmsiDataDirectory();
            var home = Directory.GetParent(dataDirectory)?.FullName;
            if (!string.IsNullOrWhiteSpace(home))
            {
                var memo = Path.Combine(home, ".openomsi-root");
                if (File.Exists(memo))
                {
                    AddRoot(File.ReadAllText(memo).Trim());
                }
            }

            var launcher = Path.Combine(dataDirectory, "launcher.json");
            if (File.Exists(launcher))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(launcher));
                if (json.RootElement.TryGetProperty("root", out var rootValue) &&
                    rootValue.ValueKind == JsonValueKind.String)
                {
                    AddRoot(rootValue.GetString());
                }
            }
        }
        catch
        {
        }

        return roots;
    }

    private static string? ResolveRunningExecutable()
    {
        foreach (var process in Process.GetProcessesByName("openomsi"))
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                return ResolveExecutableCandidate(
                    process.MainModule?.FileName);
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static string? ReadLauncherExecutable()
    {
        try
        {
            var path = Path.Combine(
                GetOpenOmsiDataDirectory(),
                "launcher.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var json = JsonDocument.Parse(File.ReadAllText(path));
            return json.RootElement.TryGetProperty("game", out var game) &&
                   game.ValueKind == JsonValueKind.String
                ? ResolveExecutableCandidate(game.GetString())
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveExecutableCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var candidate = value.Trim().Trim('"');
            if (Directory.Exists(candidate))
            {
                candidate = Path.Combine(candidate, "openomsi.exe");
            }

            var full = Path.GetFullPath(candidate);
            return File.Exists(full) &&
                   string.Equals(
                       Path.GetFileName(full),
                       "openomsi.exe",
                       StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetOpenOmsiDataDirectory()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (OperatingSystem.IsWindows() &&
            (string.IsNullOrWhiteSpace(home) ||
             !Path.IsPathRooted(home) ||
             !Directory.Exists(home)))
        {
            home = Environment.GetEnvironmentVariable("USERPROFILE");
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(home ?? string.Empty, ".openomsi");
    }
}
