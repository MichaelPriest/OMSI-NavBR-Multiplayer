using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NavBR.Client.PluginInstaller;

internal static class OmsiPluginInstallationService
{
    private const string EmbeddedResourceName = "NavBR.Client.Assets.NavBROmsiPlugin.bundle.zip";
    private const string SteamAppId = "252530";

    private static readonly string[] RequiredPluginFiles =
    [
        "NavBR.OmsiPlugin.dll",
        "NavBR.OmsiInterop.dll",
        "NavBR.OmsiPlugin.opl"
    ];

    public static bool HasEmbeddedPackage =>
        Assembly.GetExecutingAssembly().GetManifestResourceInfo(EmbeddedResourceName) is not null;

    public static PluginStartupInstallResult EnsureInstalledAtStartup(string? preferredRoot = null)
    {
        if (!HasEmbeddedPackage)
        {
            return new PluginStartupInstallResult(
                "package-missing",
                null,
                null,
                Changed: false,
                "A build atual não contém o pacote do plugin embutido.");
        }

        var root = ResolveOmsiRoot(preferredRoot);
        if (string.IsNullOrWhiteSpace(root))
        {
            return new PluginStartupInstallResult(
                "omsi-not-found",
                null,
                null,
                Changed: false,
                "Nenhuma instalação válida do OMSI 2 foi localizada.");
        }

        var pluginsRoot = Path.Combine(root, "plugins");
        var manifestPath = Path.Combine(
            pluginsRoot,
            "NavBR.OmsiPlugin.install-manifest.txt");
        var tracked = ReadTrackedFiles(manifestPath);
        var requiredFilesFound = RequiredPluginFiles.Count(file =>
            File.Exists(Path.Combine(pluginsRoot, file)));
        var manifestPresent = File.Exists(manifestPath);
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        var installedVersion = ReadInstalledVersion(manifestPath);
        var versionMatches =
            manifestPresent &&
            !string.IsNullOrWhiteSpace(installedVersion) &&
            string.Equals(
                installedVersion,
                currentVersion,
                StringComparison.OrdinalIgnoreCase);

        if (requiredFilesFound == RequiredPluginFiles.Length &&
            (!manifestPresent || versionMatches))
        {
            return new PluginStartupInstallResult(
                manifestPresent ? "ready" : "untracked",
                root,
                pluginsRoot,
                Changed: false,
                manifestPresent
                    ? null
                    : "Os arquivos exigidos já existem sem manifesto NavBR; nenhuma sobrescrita automática foi feita.");
        }

        // Never change plugin binaries while OMSI is using them. The manual
        // installer remains available after the simulator is closed.
        if (Process.GetProcessesByName("Omsi").Any(process => !process.HasExited))
        {
            return new PluginStartupInstallResult(
                "omsi-running",
                root,
                pluginsRoot,
                Changed: false,
                "OMSI está em execução; a instalação automática foi adiada.");
        }

        // If a partial installation contains an untracked NavBR-named file,
        // preserve it. InstallOrUpdate performs the same guard and will return
        // a controlled failure instead of overwriting unknown content.
        var hasUntrackedRequiredFile = RequiredPluginFiles.Any(file =>
            File.Exists(Path.Combine(pluginsRoot, file)) &&
            !tracked.Contains(file));
        if (hasUntrackedRequiredFile)
        {
            return new PluginStartupInstallResult(
                "conflict",
                root,
                pluginsRoot,
                Changed: false,
                "Há arquivos NavBR não rastreados na pasta plugins; instalação automática não sobrescreveu esses arquivos.");
        }

        try
        {
            var installed = InstallOrUpdate(root);
            return new PluginStartupInstallResult(
                "installed",
                installed.OmsiRoot,
                installed.PluginsDirectory,
                Changed: true,
                null);
        }
        catch (Exception ex)
        {
            return new PluginStartupInstallResult(
                "failed",
                root,
                pluginsRoot,
                Changed: false,
                ex.Message);
        }
    }

    public static string? ResolveOmsiRoot(string? preferredRoot = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateOmsiRootCandidates(preferredRoot))
        {
            string normalized;
            try
            {
                normalized = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (!seen.Add(normalized))
            {
                continue;
            }

            if (File.Exists(Path.Combine(normalized, "Omsi.exe")))
            {
                return normalized;
            }
        }

        return null;
    }

    public static PluginInstallResult InstallOrUpdate(string omsiRoot)
    {
        var root = ValidateOmsiRoot(omsiRoot);
        AssertOmsiClosed();

        if (!HasEmbeddedPackage)
        {
            throw new InvalidOperationException(
                "Esta build do NavBR não contém o pacote do plugin embutido. Use uma build oficial/teste gerada pelo CI.");
        }

        var pluginsRoot = Path.Combine(root, "plugins");
        Directory.CreateDirectory(pluginsRoot);
        var manifestPath = Path.Combine(pluginsRoot, "NavBR.OmsiPlugin.install-manifest.txt");
        var previouslyTracked = ReadTrackedFiles(manifestPath);

        using var packageStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException("Pacote do plugin embutido não pôde ser aberto.");
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);

        var entries = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var required in RequiredPluginFiles)
        {
            if (!entries.ContainsKey(required))
            {
                throw new InvalidOperationException($"Pacote interno do plugin incompleto: {required}");
            }

            var destination = Path.Combine(pluginsRoot, required);
            if (File.Exists(destination) && !previouslyTracked.Contains(required))
            {
                throw new InvalidOperationException(
                    $"Já existe um arquivo não rastreado em '{destination}'. A instalação foi interrompida para não sobrescrever arquivos de terceiros.");
            }
        }

        foreach (var oldName in previouslyTracked)
        {
            if (RequiredPluginFiles.Contains(oldName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var obsolete = Path.Combine(pluginsRoot, Path.GetFileName(oldName));
            if (File.Exists(obsolete))
            {
                File.Delete(obsolete);
            }
        }

        var staging = Path.Combine(
            Path.GetTempPath(),
            "NavBR-PluginInstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            foreach (var required in RequiredPluginFiles)
            {
                var stagedFile = Path.Combine(staging, required);
                entries[required].ExtractToFile(stagedFile, overwrite: true);
            }

            foreach (var required in RequiredPluginFiles)
            {
                File.Copy(
                    Path.Combine(staging, required),
                    Path.Combine(pluginsRoot, required),
                    overwrite: true);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // Staging cleanup must not invalidate a successful install.
            }
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        File.WriteAllLines(
            manifestPath,
            [
                "# OMSI NavBR Plugin experimental - arquivos instalados",
                $"# Instalado em: {DateTimeOffset.Now:O}",
                $"# NavBR: {version}",
                "# Deployment: Native AOT x86 + NavBR OMSI ABI interop x86",
                .. RequiredPluginFiles
            ]);

        return new PluginInstallResult(root, pluginsRoot, RequiredPluginFiles.Length);
    }

    public static PluginRemoveResult Remove(string omsiRoot)
    {
        var root = ValidateOmsiRoot(omsiRoot);
        AssertOmsiClosed();

        var pluginsRoot = Path.Combine(root, "plugins");
        var manifestPath = Path.Combine(pluginsRoot, "NavBR.OmsiPlugin.install-manifest.txt");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                "Manifesto do NavBR não foi encontrado. Nenhum arquivo foi removido automaticamente.");
        }

        var tracked = ReadTrackedFiles(manifestPath);
        var removed = 0;
        foreach (var name in tracked)
        {
            var target = Path.Combine(pluginsRoot, Path.GetFileName(name));
            if (!File.Exists(target))
            {
                continue;
            }

            File.Delete(target);
            removed++;
        }

        File.Delete(manifestPath);
        return new PluginRemoveResult(root, pluginsRoot, removed);
    }

    private static IEnumerable<string> EnumerateOmsiRootCandidates(string? preferredRoot)
    {
        if (!string.IsNullOrWhiteSpace(preferredRoot))
        {
            yield return preferredRoot;
        }

        // OMSI Launcher documents the native Aerosoft registration as an additional
        // installation source. This is useful when OMSI lives outside the default
        // Steam library, or when the Steam folder is symlinked/junctioned.
        foreach (var registeredRoot in EnumerateRegisteredOmsiRoots())
        {
            yield return registeredRoot;
        }

        foreach (var steamRoot in EnumerateSteamRoots())
        {
            var steamApps = Path.Combine(steamRoot, "steamapps");
            var manifest = Path.Combine(steamApps, $"appmanifest_{SteamAppId}.acf");
            if (File.Exists(manifest))
            {
                var installDir = TryReadSteamInstallDir(manifest);
                if (!string.IsNullOrWhiteSpace(installDir))
                {
                    yield return Path.Combine(steamApps, "common", installDir);
                }
            }

            yield return Path.Combine(steamApps, "common", "OMSI 2");
            yield return Path.Combine(steamApps, "common", "OMSI 2 Steam Edition");
        }
    }

    private static IEnumerable<string> EnumerateRegisteredOmsiRoots()
    {
        var roots = new List<string>();

        AddRegistryPath(
            roots,
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            @"SOFTWARE\aerosoft\OMSI 2",
            "Product_Path");

        // Some systems expose the WOW6432Node path literally despite Registry32.
        AddRegistryPath(
            roots,
            RegistryHive.LocalMachine,
            RegistryView.Default,
            @"SOFTWARE\WOW6432Node\aerosoft\OMSI 2",
            "Product_Path");

        AddRegistryPath(
            roots,
            RegistryHive.CurrentUser,
            RegistryView.Default,
            @"Software\aerosoft\OMSI 2",
            "Product_Path");

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var roots = new List<string>();

        AddRegistryPath(roots, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath");
        AddRegistryPath(roots, RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath");
        AddRegistryPath(roots, RegistryHive.LocalMachine, RegistryView.Default, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            roots.Add(Path.Combine(programFilesX86, "Steam"));
        }

        var expanded = new List<string>(roots);
        foreach (var root in roots.ToArray())
        {
            var libraryFolders = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFolders))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(libraryFolders);
                foreach (Match match in Regex.Matches(
                             text,
                             "\\\"path\\\"\\s*\\\"(?<path>[^\\\"]+)\\\"",
                             RegexOptions.IgnoreCase))
                {
                    var path = match.Groups["path"].Value.Replace("\\\\", "\\");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        expanded.Add(path);
                    }
                }
            }
            catch
            {
                // A malformed Steam library file must not break NavBR startup.
            }
        }

        return expanded.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddRegistryPath(
        ICollection<string> roots,
        RegistryHive hive,
        RegistryView view,
        string subKey,
        string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            if (key?.GetValue(valueName) is string path && !string.IsNullOrWhiteSpace(path))
            {
                roots.Add(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar));
            }
        }
        catch
        {
            // Registry discovery is best-effort; other sources remain available.
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

    private static string ValidateOmsiRoot(string omsiRoot)
    {
        var root = Path.GetFullPath(omsiRoot);
        if (!File.Exists(Path.Combine(root, "Omsi.exe")))
        {
            throw new DirectoryNotFoundException($"Omsi.exe não foi encontrado em: {root}");
        }

        return root;
    }

    private static void AssertOmsiClosed()
    {
        if (Process.GetProcessesByName("Omsi").Any(process => !process.HasExited))
        {
            throw new InvalidOperationException("Feche o OMSI antes de instalar, atualizar ou remover o plugin NavBR.");
        }
    }

    private static string? ReadInstalledVersion(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            const string prefix = "# NavBR:";
            foreach (var line in File.ReadLines(manifestPath))
            {
                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = line[prefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch
        {
            // Startup inspection is best-effort. Installation itself performs
            // the authoritative checks before touching any file.
        }

        return null;
    }

    private static HashSet<string> ReadTrackedFiles(string manifestPath)
    {
        var tracked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(manifestPath))
        {
            return tracked;
        }

        foreach (var line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var name = Path.GetFileName(line.Trim());
            if (!string.IsNullOrWhiteSpace(name))
            {
                tracked.Add(name);
            }
        }

        return tracked;
    }
}

internal sealed record PluginInstallResult(
    string OmsiRoot,
    string PluginsDirectory,
    int InstalledFiles);

internal sealed record PluginRemoveResult(
    string OmsiRoot,
    string PluginsDirectory,
    int RemovedFiles);

internal sealed record PluginStartupInstallResult(
    string Status,
    string? OmsiRoot,
    string? PluginsDirectory,
    bool Changed,
    string? Message);
