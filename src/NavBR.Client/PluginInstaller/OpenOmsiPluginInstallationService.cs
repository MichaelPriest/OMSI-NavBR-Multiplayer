using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace NavBR.Client.PluginInstaller;

internal static class OpenOmsiPluginInstallationService
{
    private const string EmbeddedResourceName =
        "NavBR.Client.Assets.NavBROpenOmsiPlugin.bundle.zip";

    private static readonly string[] RequiredPluginFiles =
    [
        "NavBR.OpenOmsiPlugin.dll",
        "NavBR.OpenOmsiPlugin.opl"
    ];

    public static bool HasEmbeddedPackage =>
        Assembly.GetExecutingAssembly()
            .GetManifestResourceInfo(EmbeddedResourceName) is not null;

    public static OpenOmsiPluginVerification Verify(string? preferredExecutable = null)
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;
        var executable = ResolveOpenOmsiExecutable(preferredExecutable);
        var contentRoot = executable is null
            ? null
            : ResolveContentRoot(executable);
        var pluginDirectory = contentRoot is null
            ? null
            : Path.Combine(contentRoot, "Plugins", "NavBR.OpenOmsi");
        var manifestPath = pluginDirectory is null
            ? null
            : Path.Combine(
                pluginDirectory,
                "NavBR.OpenOmsiPlugin.install-manifest.txt");

        if (!HasEmbeddedPackage)
        {
            return new(
                "package-missing",
                executable,
                contentRoot,
                pluginDirectory,
                GetCurrentPackageVersion(),
                null,
                null,
                RequiredPluginFiles.Length,
                0,
                0,
                false,
                checkedAtUtc,
                [],
                "Esta build do NavBR não contém o pacote openOMSI embutido.");
        }

        if (executable is null || contentRoot is null || pluginDirectory is null)
        {
            return new(
                "openomsi-not-found",
                null,
                null,
                null,
                GetCurrentPackageVersion(),
                GetEmbeddedPackageHash(),
                null,
                RequiredPluginFiles.Length,
                0,
                0,
                false,
                checkedAtUtc,
                [],
                "openomsi.exe não foi localizado. Selecione o executável do openOMSI.");
        }

        try
        {
            using var packageStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(EmbeddedResourceName)
                ?? throw new InvalidOperationException(
                    "Pacote openOMSI embutido não pôde ser aberto.");
            using var archive = new ZipArchive(
                packageStream,
                ZipArchiveMode.Read,
                leaveOpen: false);
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

            var files =
                new List<OpenOmsiPluginFileVerification>(
                    RequiredPluginFiles.Length);
            foreach (var required in RequiredPluginFiles)
            {
                if (!entries.TryGetValue(required, out var entry))
                {
                    files.Add(new(
                        required,
                        File.Exists(Path.Combine(pluginDirectory, required)),
                        false,
                        null,
                        null));
                    continue;
                }

                string expectedHash;
                using (var stream = entry.Open())
                {
                    expectedHash = ToSha256(SHA256.HashData(stream));
                }

                var installedPath = Path.Combine(pluginDirectory, required);
                var exists = File.Exists(installedPath);
                string? installedHash = null;
                if (exists)
                {
                    using var stream = File.OpenRead(installedPath);
                    installedHash = ToSha256(SHA256.HashData(stream));
                }

                files.Add(new(
                    required,
                    exists,
                    exists &&
                    string.Equals(
                        expectedHash,
                        installedHash,
                        StringComparison.OrdinalIgnoreCase),
                    expectedHash,
                    installedHash));
            }

            var found = files.Count(file => file.Exists);
            var verified =
                files.Count(file => file.Exists && file.HashMatches);
            var manifestPresent =
                manifestPath is not null && File.Exists(manifestPath);
            var installedVersion =
                manifestPath is null ? null : ReadManifestValue(
                    manifestPath,
                    "# NavBR:");
            var installedHash =
                manifestPath is null ? null : ReadManifestValue(
                    manifestPath,
                    "# Package-SHA256:");
            var expectedVersion = GetCurrentPackageVersion();
            var expectedPackageHash = GetEmbeddedPackageHash();

            var manifestCurrent =
                manifestPresent &&
                string.Equals(
                    expectedVersion,
                    installedVersion,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(expectedPackageHash) &&
                string.Equals(
                    expectedPackageHash,
                    installedHash,
                    StringComparison.OrdinalIgnoreCase);
            var allFilesCurrent =
                found == RequiredPluginFiles.Length &&
                verified == RequiredPluginFiles.Length;

            var state = allFilesCurrent && manifestCurrent
                ? "ready"
                : found == 0
                    ? "missing"
                    : found < RequiredPluginFiles.Length
                        ? "partial"
                        : "outdated";

            return new(
                state,
                executable,
                contentRoot,
                pluginDirectory,
                expectedVersion,
                expectedPackageHash,
                installedVersion,
                RequiredPluginFiles.Length,
                found,
                verified,
                state is "missing" or "partial" or "outdated",
                checkedAtUtc,
                files,
                state == "ready"
                    ? null
                    : "O plugin NavBR for openOMSI precisa ser instalado ou atualizado.");
        }
        catch (Exception ex)
        {
            return new(
                "error",
                executable,
                contentRoot,
                pluginDirectory,
                GetCurrentPackageVersion(),
                GetEmbeddedPackageHash(),
                null,
                RequiredPluginFiles.Length,
                0,
                0,
                false,
                checkedAtUtc,
                [],
                ex.Message);
        }
    }

    public static OpenOmsiPluginInstallResult InstallOrUpdate(
        string? preferredExecutable = null)
    {
        AssertOpenOmsiClosed();

        if (!HasEmbeddedPackage)
        {
            throw new InvalidOperationException(
                "Esta build do NavBR não contém o pacote do plugin openOMSI.");
        }

        var executable = ResolveOpenOmsiExecutable(preferredExecutable)
            ?? throw new FileNotFoundException(
                "openomsi.exe não foi localizado.");
        var contentRoot = ResolveContentRoot(executable)
            ?? throw new DirectoryNotFoundException(
                "O content root do openOMSI não pôde ser resolvido.");
        var pluginsRoot = Path.Combine(contentRoot, "Plugins");
        var pluginDirectory = Path.Combine(
            pluginsRoot,
            "NavBR.OpenOmsi");
        Directory.CreateDirectory(pluginDirectory);

        var manifestPath = Path.Combine(
            pluginDirectory,
            "NavBR.OpenOmsiPlugin.install-manifest.txt");
        var tracked = ReadTrackedFiles(manifestPath);

        foreach (var required in RequiredPluginFiles)
        {
            var path = Path.Combine(pluginDirectory, required);
            if (File.Exists(path) &&
                !tracked.Contains(required) &&
                !LooksLikeLegacyNavBrInstall(pluginDirectory))
            {
                throw new InvalidOperationException(
                    $"Arquivo não rastreado já existe: {path}");
            }
        }

        using var packageStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                "Pacote openOMSI embutido não pôde ser aberto.");
        using var archive = new ZipArchive(
            packageStream,
            ZipArchiveMode.Read,
            leaveOpen: false);
        var entries = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var required in RequiredPluginFiles)
        {
            if (!entries.TryGetValue(required, out var entry))
            {
                throw new InvalidOperationException(
                    $"Pacote openOMSI incompleto: {required}");
            }

            entry.ExtractToFile(
                Path.Combine(pluginDirectory, required),
                overwrite: true);
        }

        var packageHash = GetEmbeddedPackageHash()
            ?? throw new InvalidOperationException(
                "Não foi possível calcular o hash do pacote openOMSI.");
        var version = GetCurrentPackageVersion();
        File.WriteAllLines(
            manifestPath,
            [
                "# NavBR for openOMSI - arquivos instalados",
                $"# Instalado em: {DateTimeOffset.Now:O}",
                $"# NavBR: {version}",
                $"# Package-SHA256: {packageHash}",
                "# Deployment: Native AOT win-x64 + standard OMSI .opl ABI",
                .. RequiredPluginFiles
            ]);

        EnsureContentMarker(contentRoot);

        return new(
            executable,
            contentRoot,
            pluginDirectory,
            RequiredPluginFiles.Length);
    }

    public static int Remove(string? preferredExecutable = null)
    {
        AssertOpenOmsiClosed();

        var executable = ResolveOpenOmsiExecutable(preferredExecutable)
            ?? throw new FileNotFoundException(
                "openomsi.exe não foi localizado.");
        var contentRoot = ResolveContentRoot(executable)
            ?? throw new DirectoryNotFoundException(
                "O content root do openOMSI não pôde ser resolvido.");
        var pluginDirectory = Path.Combine(
            contentRoot,
            "Plugins",
            "NavBR.OpenOmsi");
        var manifestPath = Path.Combine(
            pluginDirectory,
            "NavBR.OpenOmsiPlugin.install-manifest.txt");

        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                "Manifesto do NavBR for openOMSI não foi encontrado.");
        }

        var removed = 0;
        foreach (var name in ReadTrackedFiles(manifestPath))
        {
            var path = Path.Combine(pluginDirectory, Path.GetFileName(name));
            if (!File.Exists(path))
            {
                continue;
            }

            File.Delete(path);
            removed++;
        }

        File.Delete(manifestPath);
        if (Directory.Exists(pluginDirectory) &&
            !Directory.EnumerateFileSystemEntries(pluginDirectory).Any())
        {
            Directory.Delete(pluginDirectory);
        }

        return removed;
    }

    public static string? ResolveOpenOmsiExecutable(
        string? preferredExecutable = null)
    {
        var candidates = new List<string?>();

        if (!string.IsNullOrWhiteSpace(preferredExecutable))
        {
            candidates.Add(preferredExecutable);
        }

        candidates.Add(Environment.GetEnvironmentVariable("OPENOMSI_BIN"));

        try
        {
            var config = Path.Combine(
                GetOpenOmsiDataDirectory(),
                "launcher.json");
            if (File.Exists(config))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(config));
                if (json.RootElement.TryGetProperty("game", out var game) &&
                    game.ValueKind == JsonValueKind.String)
                {
                    candidates.Add(game.GetString());
                }
            }
        }
        catch
        {
        }

        foreach (var raw in candidates.Where(
                     value => !string.IsNullOrWhiteSpace(value)))
        {
            var resolved = ResolveExecutableCandidate(raw!);
            if (resolved is not null)
            {
                SavePreferredExecutable(resolved);
                return resolved;
            }
        }

        var saved = ReadPreferredExecutable();
        return ResolveExecutableCandidate(saved);
    }

    public static string? ResolveContentRoot(string executablePath)
    {
        try
        {
            var exe = Path.GetFullPath(executablePath);
            if (!File.Exists(exe) ||
                !string.Equals(
                    Path.GetFileName(exe),
                    "openomsi.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var envContent =
                Environment.GetEnvironmentVariable("OMSI_CONTENT");
            if (!string.IsNullOrWhiteSpace(envContent))
            {
                return Path.GetFullPath(envContent.Trim());
            }

            var binaryDirectory =
                Path.GetDirectoryName(exe)
                ?? throw new DirectoryNotFoundException(
                    "Pasta do openomsi.exe não encontrada.");
            var binaryInsideOriginalOmsi =
                File.Exists(Path.Combine(binaryDirectory, "Omsi.exe")) &&
                Directory.Exists(Path.Combine(binaryDirectory, "maps"));
            if (binaryInsideOriginalOmsi)
            {
                return Path.Combine(binaryDirectory, "openOMSI");
            }

            if (IsDirectoryWritable(binaryDirectory))
            {
                return binaryDirectory;
            }

            return Path.Combine(
                GetOpenOmsiDataDirectory(),
                "content");
        }
        catch
        {
            return null;
        }
    }

    public static bool IsOpenOmsiRunning() =>
        Process.GetProcessesByName("openomsi")
            .Any(process =>
            {
                try
                {
                    return !process.HasExited;
                }
                finally
                {
                    process.Dispose();
                }
            });

    public static void SavePreferredExecutable(string executablePath)
    {
        var path = Path.Combine(
            GetOpenOmsiDataDirectory(),
            "navbr-openomsi.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new OpenOmsiPreference(
                    Path.GetFullPath(executablePath))));
    }

    private static string? ReadPreferredExecutable()
    {
        try
        {
            var path = Path.Combine(
                GetOpenOmsiDataDirectory(),
                "navbr-openomsi.json");
            if (!File.Exists(path))
            {
                return null;
            }

            var preference =
                JsonSerializer.Deserialize<OpenOmsiPreference>(
                    File.ReadAllText(path));
            return preference?.ExecutablePath;
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
            home =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(home ?? string.Empty, ".openomsi");
    }

    private static bool IsDirectoryWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(
                directory,
                ".navbr-openomsi-write-test");
            File.WriteAllText(probe, "NavBR");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureContentMarker(string contentRoot)
    {
        Directory.CreateDirectory(contentRoot);
        var marker = Path.Combine(contentRoot, ".openomsi-content");
        if (!File.Exists(marker))
        {
            File.WriteAllText(
                marker,
                "This folder holds openOMSI content. NavBR installed its plugin under Plugins/NavBR.OpenOmsi." +
                Environment.NewLine);
        }
    }

    private static void AssertOpenOmsiClosed()
    {
        if (IsOpenOmsiRunning())
        {
            throw new InvalidOperationException(
                "Feche o openOMSI antes de instalar, atualizar ou remover o plugin NavBR.");
        }
    }

    private static bool LooksLikeLegacyNavBrInstall(
        string pluginDirectory) =>
        File.Exists(
            Path.Combine(
                pluginDirectory,
                "NavBR.OpenOmsiPlugin.opl")) &&
        File.Exists(
            Path.Combine(
                pluginDirectory,
                "NavBR.OpenOmsiPlugin.dll"));

    private static HashSet<string> ReadTrackedFiles(string manifestPath)
    {
        var result = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(manifestPath))
        {
            return result;
        }

        foreach (var line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var name = Path.GetFileName(line.Trim());
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name);
            }
        }

        return result;
    }

    private static string? ReadManifestValue(
        string path,
        string prefix)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (!line.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = line[prefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string GetCurrentPackageVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    private static string? GetEmbeddedPackageHash()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(EmbeddedResourceName);
            return stream is null
                ? null
                : ToSha256(SHA256.HashData(stream));
        }
        catch
        {
            return null;
        }
    }

    private static string ToSha256(byte[] hash) =>
        "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
}

internal sealed record OpenOmsiPreference(string ExecutablePath);

internal sealed record OpenOmsiPluginInstallResult(
    string ExecutablePath,
    string ContentRoot,
    string PluginDirectory,
    int InstalledFiles);

internal sealed record OpenOmsiPluginVerification(
    string Status,
    string? ExecutablePath,
    string? ContentRoot,
    string? PluginDirectory,
    string? ExpectedVersion,
    string? ExpectedPackageHash,
    string? InstalledVersion,
    int RequiredFilesTotal,
    int RequiredFilesFound,
    int VerifiedFiles,
    bool UpdateRequired,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<OpenOmsiPluginFileVerification> Files,
    string? Message);

internal sealed record OpenOmsiPluginFileVerification(
    string Name,
    bool Exists,
    bool HashMatches,
    string? ExpectedSha256,
    string? InstalledSha256);
