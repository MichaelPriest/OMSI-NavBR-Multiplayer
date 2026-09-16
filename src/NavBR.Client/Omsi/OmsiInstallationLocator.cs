using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NavBR.Client.Omsi;

internal enum OmsiInstallationSource
{
    Manual,
    RunningProcess,
    AerosoftRegistry,
    SteamManifest,
    SteamLibraryFallback
}

internal sealed record OmsiInstallationInfo(
    string InstallDirectory,
    string ExecutablePath,
    OmsiInstallationSource Source,
    string? SteamLibrary = null,
    string? SteamManifestPath = null);

internal static class OmsiInstallationLocator
{
    private const string SteamAppId = "252530";

    public static IReadOnlyList<OmsiInstallationInfo> Discover(string? preferredPath = null)
    {
        var result = new Dictionary<string, OmsiInstallationInfo>(StringComparer.OrdinalIgnoreCase);

        AddCandidate(result, preferredPath, OmsiInstallationSource.Manual);
        AddAerosoftRegistry(result);

        foreach (var steamRoot in DiscoverSteamRoots())
        {
            var steamApps = Path.Combine(steamRoot, "steamapps");
            var manifestPath = Path.Combine(steamApps, $"appmanifest_{SteamAppId}.acf");
            if (File.Exists(manifestPath))
            {
                var installDir = TryReadSteamInstallDir(manifestPath);
                if (!string.IsNullOrWhiteSpace(installDir))
                {
                    AddCandidate(
                        result,
                        Path.Combine(steamApps, "common", installDir),
                        OmsiInstallationSource.SteamManifest,
                        steamRoot,
                        manifestPath);
                }
            }

            AddCandidate(
                result,
                Path.Combine(steamApps, "common", "OMSI 2"),
                OmsiInstallationSource.SteamLibraryFallback,
                steamRoot,
                manifestPath);
            AddCandidate(
                result,
                Path.Combine(steamApps, "common", "OMSI 2 Steam Edition"),
                OmsiInstallationSource.SteamLibraryFallback,
                steamRoot,
                manifestPath);
        }

        return result.Values
            .OrderBy(item => item.InstallDirectory, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static OmsiInstallationInfo? FromRunningProcess(OmsiProcessInfo? process)
    {
        if (process is null || string.IsNullOrWhiteSpace(process.InstallDirectory))
        {
            return null;
        }

        var root = NormalizeDirectory(process.InstallDirectory);
        if (root is null)
        {
            return null;
        }

        var exe = Path.Combine(root, "Omsi.exe");
        return File.Exists(exe)
            ? new OmsiInstallationInfo(root, exe, OmsiInstallationSource.RunningProcess)
            : null;
    }

    private static void AddAerosoftRegistry(IDictionary<string, OmsiInstallationInfo> result)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(@"SOFTWARE\aerosoft\OMSI 2");
                    if (key?.GetValue("Product_Path") is string path)
                    {
                        AddCandidate(result, path, OmsiInstallationSource.AerosoftRegistry);
                    }
                }
                catch
                {
                    // Registry discovery is best-effort.
                }
            }
        }
    }

    private static IReadOnlyList<string> DiscoverSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TryAddRegistryPath(
            roots,
            RegistryHive.CurrentUser,
            RegistryView.Default,
            @"Software\Valve\Steam",
            "SteamPath");
        TryAddRegistryPath(
            roots,
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            @"SOFTWARE\Valve\Steam",
            "InstallPath");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            var defaultSteam = Path.Combine(programFilesX86, "Steam");
            if (Directory.Exists(defaultSteam))
            {
                roots.Add(defaultSteam);
            }
        }

        foreach (var root in roots.ToArray())
        {
            foreach (var library in ReadSteamLibraryFolders(root))
            {
                roots.Add(library);
            }
        }

        return roots.ToArray();
    }

    private static IEnumerable<string> ReadSteamLibraryFolders(string steamRoot)
    {
        var file = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(file))
        {
            yield break;
        }

        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(
                     text,
                     "\\\"path\\\"\\s*\\\"(?<path>[^\\\"]+)\\\"",
                     RegexOptions.IgnoreCase))
        {
            var path = match.Groups["path"].Value.Replace("\\\\", "\\");
            var normalized = NormalizeDirectory(path);
            if (normalized is not null)
            {
                yield return normalized;
            }
        }
    }

    private static void TryAddRegistryPath(
        ISet<string> roots,
        RegistryHive hive,
        RegistryView view,
        string subKey,
        string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            if (key?.GetValue(valueName) is string path)
            {
                var normalized = NormalizeDirectory(path.Replace('/', Path.DirectorySeparatorChar));
                if (normalized is not null && Directory.Exists(normalized))
                {
                    roots.Add(normalized);
                }
            }
        }
        catch
        {
        }
    }

    private static string? TryReadSteamInstallDir(string manifestPath)
    {
        try
        {
            var text = File.ReadAllText(manifestPath);
            var match = Regex.Match(
                text,
                "\\\"installdir\\\"\\s*\\\"(?<dir>[^\\\"]+)\\\"",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["dir"].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static void AddCandidate(
        IDictionary<string, OmsiInstallationInfo> result,
        string? path,
        OmsiInstallationSource source,
        string? steamLibrary = null,
        string? steamManifestPath = null)
    {
        var root = NormalizeDirectory(path);
        if (root is null)
        {
            return;
        }

        var exe = Path.Combine(root, "Omsi.exe");
        if (!File.Exists(exe))
        {
            return;
        }

        result[root] = new OmsiInstallationInfo(
            root,
            exe,
            source,
            steamLibrary,
            File.Exists(steamManifestPath) ? steamManifestPath : null);
    }

    private static string? NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch
        {
            return null;
        }
    }
}
