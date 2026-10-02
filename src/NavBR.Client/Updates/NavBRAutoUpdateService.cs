using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NavBR.Client.Windows;

namespace NavBR.Client.Updates;

internal sealed record NavBRAutoUpdateSnapshot(
    string Status,
    string CurrentVersion,
    string? AvailableVersion,
    string? ReleaseUrl,
    string? InstallerPath,
    int? ProgressPercent,
    bool UpdateAvailable,
    bool ReadyToInstall,
    DateTimeOffset? CheckedAtUtc,
    string? Message);

internal sealed class NavBRAutoUpdateService : IDisposable
{
    private const string ReleasesApi =
        "https://api.github.com/repos/MichaelPriest/OMSI-NavBR-Multiplayer/releases?per_page=20";
    private const long MaximumInstallerBytes = 1024L * 1024L * 1024L;

    private static readonly Regex ReleaseVersionRegex = new(
        @"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<label>[A-Za-z][A-Za-z0-9-]*)(?:[.-](?<serial>\d+(?:\.\d+)*))?)?(?:\+.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private readonly object _stateLock = new();

    private NavBRAutoUpdateSnapshot _snapshot;
    private string? _preparedSha256;
    private bool _disposed;

    public NavBRAutoUpdateService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"OMSI-NavBR-Multiplayer-Updater/{NavBRVersionInfo.Current}");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(
            "application/vnd.github+json");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-GitHub-Api-Version",
            "2022-11-28");

        _snapshot = new NavBRAutoUpdateSnapshot(
            Status: "idle",
            CurrentVersion: NavBRVersionInfo.Current,
            AvailableVersion: null,
            ReleaseUrl: null,
            InstallerPath: null,
            ProgressPercent: null,
            UpdateAvailable: false,
            ReadyToInstall: false,
            CheckedAtUtc: null,
            Message: null);
    }

    public NavBRAutoUpdateSnapshot GetSnapshot()
    {
        lock (_stateLock)
        {
            return _snapshot;
        }
    }

    public async Task<NavBRAutoUpdateSnapshot> CheckAndPrepareAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!await _checkGate.WaitAsync(0, cancellationToken))
        {
            return GetSnapshot();
        }

        try
        {
            var existing = GetSnapshot();
            if (existing.ReadyToInstall &&
                !string.IsNullOrWhiteSpace(existing.InstallerPath) &&
                File.Exists(existing.InstallerPath))
            {
                return existing;
            }

            SetSnapshot(existing with
            {
                Status = "checking",
                AvailableVersion = null,
                ReleaseUrl = null,
                InstallerPath = null,
                ProgressPercent = null,
                UpdateAvailable = false,
                ReadyToInstall = false,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                Message = null
            });
            _preparedSha256 = null;

            var candidate = await FindNewestReleaseAsync(cancellationToken);
            if (candidate is null)
            {
                SetSnapshot(GetSnapshot() with
                {
                    Status = "current",
                    ProgressPercent = null,
                    UpdateAvailable = false,
                    ReadyToInstall = false,
                    CheckedAtUtc = DateTimeOffset.UtcNow,
                    Message = "Você já está usando a versão pública mais recente."
                });
                return GetSnapshot();
            }

            SetSnapshot(GetSnapshot() with
            {
                Status = "downloading",
                AvailableVersion = candidate.Version,
                ReleaseUrl = candidate.ReleaseUrl,
                ProgressPercent = 0,
                UpdateAvailable = true,
                ReadyToInstall = false,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                Message = "Nova versão encontrada. Baixando o instalador oficial."
            });

            var prepared = await PrepareInstallerAsync(
                candidate,
                cancellationToken);

            _preparedSha256 = prepared.ExpectedSha256;
            SetSnapshot(GetSnapshot() with
            {
                Status = "ready",
                AvailableVersion = candidate.Version,
                ReleaseUrl = candidate.ReleaseUrl,
                InstallerPath = prepared.InstallerPath,
                ProgressPercent = 100,
                UpdateAvailable = true,
                ReadyToInstall = true,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                Message = "Atualização baixada e validada pelo SHA-256 oficial."
            });

            return GetSnapshot();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return GetSnapshot();
        }
        catch (Exception ex)
        {
            SetSnapshot(GetSnapshot() with
            {
                Status = "failed",
                ProgressPercent = null,
                ReadyToInstall = false,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                Message = $"Falha ao preparar atualização: {ex.Message}"
            });
            return GetSnapshot();
        }
        finally
        {
            _checkGate.Release();
        }
    }

    public async Task<bool> BeginInstallAndRestartAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var snapshot = GetSnapshot();
        var installerPath = snapshot.InstallerPath;
        var expectedSha256 = _preparedSha256;

        if (!snapshot.ReadyToInstall ||
            string.IsNullOrWhiteSpace(installerPath) ||
            string.IsNullOrWhiteSpace(expectedSha256) ||
            !File.Exists(installerPath))
        {
            return false;
        }

        SetSnapshot(snapshot with
        {
            Status = "verifying",
            ReadyToInstall = false,
            Message = "Validando novamente o instalador antes de atualizar."
        });

        var actualSha256 = await ComputeSha256Async(
            installerPath,
            cancellationToken);
        if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(installerPath);
            _preparedSha256 = null;
            SetSnapshot(GetSnapshot() with
            {
                Status = "failed",
                InstallerPath = null,
                ProgressPercent = null,
                ReadyToInstall = false,
                Message = "O instalador mudou após o download e foi descartado por segurança."
            });
            return false;
        }

        var helperPath = Path.Combine(
            Path.GetDirectoryName(installerPath)!,
            "install-navbr-update.ps1");
        var processId = Environment.ProcessId;
        var currentExe = Environment.ProcessPath
            ?? Path.Combine(
                AppContext.BaseDirectory,
                "OMSI.NavBR.Multiplayer.exe");
        var fallbackExe = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "OMSI NavBR Multiplayer",
            "OMSI.NavBR.Multiplayer.exe");
        var installedBuild = IsInstalledBuild();

        var script = BuildUpdateHelperScript(
            processId,
            installerPath,
            currentExe,
            fallbackExe,
            installedBuild);
        await File.WriteAllTextAsync(
            helperPath,
            script,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(helperPath);

        using var helper = Process.Start(startInfo);
        if (helper is null)
        {
            SetSnapshot(GetSnapshot() with
            {
                Status = "failed",
                ReadyToInstall = true,
                Message = "Não foi possível iniciar o processo auxiliar de atualização."
            });
            return false;
        }

        SetSnapshot(GetSnapshot() with
        {
            Status = "installing",
            ReadyToInstall = false,
            Message = "O NavBR será fechado, atualizado e aberto novamente."
        });
        return true;
    }

    internal static int CompareReleaseVersions(
        string left,
        string right)
    {
        if (!TryParseReleaseVersion(left, out var leftVersion))
        {
            throw new FormatException($"Invalid NavBR version: {left}");
        }

        if (!TryParseReleaseVersion(right, out var rightVersion))
        {
            throw new FormatException($"Invalid NavBR version: {right}");
        }

        return CompareParsedVersions(leftVersion, rightVersion);
    }

    internal static bool TryGetExpectedSha256(
        string checksumText,
        string fileName,
        out string sha256)
    {
        sha256 = string.Empty;
        if (string.IsNullOrWhiteSpace(checksumText) ||
            string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (var rawLine in checksumText.Split(
                     new[] { '\r', '\n' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length < 66)
            {
                continue;
            }

            var candidateHash = line[..64];
            if (!candidateHash.All(Uri.IsHexDigit))
            {
                continue;
            }

            var candidateName = line[64..].TrimStart();
            if (candidateName.StartsWith('*'))
            {
                candidateName = candidateName[1..];
            }

            if (!string.Equals(
                    candidateName,
                    fileName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            sha256 = candidateHash.ToLowerInvariant();
            return true;
        }

        return false;
    }

    private async Task<ReleaseCandidate?> FindNewestReleaseAsync(
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            ReleasesApi,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document =
            await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "GitHub Releases returned an unexpected response.");
        }

        ReleaseCandidate? newest = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (GetBoolean(release, "draft"))
            {
                continue;
            }

            var tagName = GetString(release, "tag_name");
            if (string.IsNullOrWhiteSpace(tagName) ||
                !TryParseReleaseVersion(tagName, out _))
            {
                continue;
            }

            if (CompareReleaseVersions(
                    tagName,
                    NavBRVersionInfo.Current) <= 0)
            {
                continue;
            }

            if (!release.TryGetProperty(
                    "assets",
                    out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            ReleaseAsset? installer = null;
            ReleaseAsset? checksums = null;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = GetString(asset, "name");
                var url = GetString(asset, "browser_download_url");
                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (string.Equals(
                        name,
                        "SHA256SUMS.txt",
                        StringComparison.Ordinal))
                {
                    checksums = new ReleaseAsset(name, url);
                    continue;
                }

                if (name.StartsWith(
                        "OMSI-NavBR-Multiplayer-",
                        StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(
                        "-Setup-win-x86.exe",
                        StringComparison.OrdinalIgnoreCase))
                {
                    installer = new ReleaseAsset(name, url);
                }
            }

            if (installer is null || checksums is null)
            {
                continue;
            }

            EnsureTrustedReleaseUrl(installer.Url);
            EnsureTrustedReleaseUrl(checksums.Url);

            var candidate = new ReleaseCandidate(
                TagName: tagName,
                Version: NormalizeVersion(tagName),
                ReleaseUrl:
                    GetString(release, "html_url") ??
                    $"https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/releases/tag/{tagName}",
                InstallerName: installer.Name,
                InstallerUrl: installer.Url,
                ChecksumsUrl: checksums.Url);

            if (newest is null ||
                CompareReleaseVersions(
                    candidate.TagName,
                    newest.TagName) > 0)
            {
                newest = candidate;
            }
        }

        return newest;
    }

    private async Task<PreparedInstaller> PrepareInstallerAsync(
        ReleaseCandidate candidate,
        CancellationToken cancellationToken)
    {
        var installerName = Path.GetFileName(candidate.InstallerName);
        if (!string.Equals(
                installerName,
                candidate.InstallerName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Release installer name is not a plain file name.");
        }

        var checksumText = await _httpClient.GetStringAsync(
            candidate.ChecksumsUrl,
            cancellationToken);
        if (!TryGetExpectedSha256(
                checksumText,
                installerName,
                out var expectedSha256))
        {
            throw new InvalidDataException(
                $"SHA256SUMS.txt does not contain {installerName}.");
        }

        var updateDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "OMSI NavBR Multiplayer",
            "Updates",
            SanitizeTag(candidate.TagName));
        Directory.CreateDirectory(updateDirectory);

        var finalPath = Path.Combine(
            updateDirectory,
            installerName);
        if (File.Exists(finalPath))
        {
            var existingHash = await ComputeSha256Async(
                finalPath,
                cancellationToken);
            if (string.Equals(
                    existingHash,
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new PreparedInstaller(
                    finalPath,
                    expectedSha256);
            }

            TryDelete(finalPath);
        }

        var partialPath = finalPath + ".download";
        TryDelete(partialPath);

        await DownloadInstallerAsync(
            candidate.InstallerUrl,
            partialPath,
            cancellationToken);

        SetSnapshot(GetSnapshot() with
        {
            Status = "verifying",
            Message = "Download concluído. Validando SHA-256 oficial."
        });

        var actualSha256 = await ComputeSha256Async(
            partialPath,
            cancellationToken);
        if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(partialPath);
            throw new InvalidDataException(
                "Downloaded installer SHA-256 does not match SHA256SUMS.txt.");
        }

        File.Move(
            partialPath,
            finalPath,
            overwrite: true);
        return new PreparedInstaller(
            finalPath,
            expectedSha256);
    }

    private async Task DownloadInstallerAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        EnsureTrustedReleaseUrl(url);

        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var length = response.Content.Headers.ContentLength;
        if (length is > MaximumInstallerBytes)
        {
            throw new InvalidDataException(
                "Release installer exceeds the updater size limit.");
        }

        await using var input =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        var buffer = new byte[64 * 1024];
        long written = 0;
        var lastProgress = -1;

        while (true)
        {
            var read = await input.ReadAsync(
                buffer,
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);
            written += read;

            if (written > MaximumInstallerBytes)
            {
                throw new InvalidDataException(
                    "Release installer exceeded the updater size limit.");
            }

            if (length is > 0)
            {
                var progress = Math.Clamp(
                    (int)Math.Round(
                        written * 100d / length.Value),
                    0,
                    99);
                if (progress != lastProgress)
                {
                    lastProgress = progress;
                    SetSnapshot(GetSnapshot() with
                    {
                        Status = "downloading",
                        ProgressPercent = progress,
                        Message = $"Baixando atualização… {progress}%"
                    });
                }
            }
        }

        await output.FlushAsync(cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(
            stream,
            cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildUpdateHelperScript(
        int processId,
        string installerPath,
        string currentExe,
        string fallbackExe,
        bool installedBuild)
    {
        var installer = ToPowerShellLiteral(installerPath);
        var current = ToPowerShellLiteral(currentExe);
        var fallback = ToPowerShellLiteral(fallbackExe);
        var installed = installedBuild ? "$true" : "$false";

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "$ErrorActionPreference = 'SilentlyContinue'",
                $"$targetPid = {processId}",
                $"$installer = {installer}",
                $"$currentExe = {current}",
                $"$fallbackExe = {fallback}",
                $"$installedBuild = {installed}",
                string.Empty,
                "while (Get-Process -Id $targetPid -ErrorAction SilentlyContinue) {",
                "    Start-Sleep -Milliseconds 250",
                "}",
                string.Empty,
                "$arguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS')",
                "$setup = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru",
                string.Empty,
                "if ($setup -and $setup.ExitCode -eq 0) {",
                "    $restart = $null",
                "    if ($installedBuild -and (Test-Path -LiteralPath $currentExe)) {",
                "        $restart = $currentExe",
                "    } elseif (Test-Path -LiteralPath $fallbackExe) {",
                "        $restart = $fallbackExe",
                "    } elseif (Test-Path -LiteralPath $currentExe) {",
                "        $restart = $currentExe",
                "    }",
                string.Empty,
                "    if ($restart) {",
                "        Start-Process -FilePath $restart",
                "    }",
                "}",
                string.Empty,
                "Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue",
                "Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue",
                string.Empty
            });
    }

    private static bool IsInstalledBuild()
    {
        try
        {
            return Directory.EnumerateFiles(
                    AppContext.BaseDirectory,
                    "unins*.exe",
                    SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch
        {
            return false;
        }
    }

    private static string ToPowerShellLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static void EnsureTrustedReleaseUrl(string value)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Updater asset URL is not an approved GitHub HTTPS URL.");
        }
    }

    private static string NormalizeVersion(string version) =>
        version.Trim().TrimStart('v', 'V').Split('+')[0];

    private static string SanitizeTag(string tag)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(tag.Length);
        foreach (var character in tag)
        {
            builder.Append(
                invalid.Contains(character)
                    ? '_'
                    : character);
        }

        return builder.ToString();
    }

    private static string? GetString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(
            propertyName,
            out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool GetBoolean(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(
            propertyName,
            out var property) &&
        (property.ValueKind == JsonValueKind.True ||
         property.ValueKind == JsonValueKind.False) &&
        property.GetBoolean();

    private static bool TryParseReleaseVersion(
        string value,
        out ParsedVersion parsed)
    {
        parsed = default!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = ReleaseVersionRegex.Match(value.Trim());
        if (!match.Success ||
            !int.TryParse(match.Groups["major"].Value, out var major) ||
            !int.TryParse(match.Groups["minor"].Value, out var minor) ||
            !int.TryParse(match.Groups["patch"].Value, out var patch))
        {
            return false;
        }

        var label = match.Groups["label"].Success
            ? match.Groups["label"].Value
            : null;
        var serial = Array.Empty<int>();
        if (match.Groups["serial"].Success)
        {
            var parts = match.Groups["serial"].Value.Split('.');
            serial = new int[parts.Length];
            for (var index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out serial[index]))
                {
                    return false;
                }
            }
        }

        parsed = new ParsedVersion(
            major,
            minor,
            patch,
            label,
            serial);
        return true;
    }

    private static int CompareParsedVersions(
        ParsedVersion left,
        ParsedVersion right)
    {
        var core = left.Major.CompareTo(right.Major);
        if (core != 0) return core;
        core = left.Minor.CompareTo(right.Minor);
        if (core != 0) return core;
        core = left.Patch.CompareTo(right.Patch);
        if (core != 0) return core;

        if (left.Label is null && right.Label is null)
        {
            return 0;
        }

        if (left.Label is null)
        {
            return 1;
        }

        if (right.Label is null)
        {
            return -1;
        }

        var leftRank = GetPreReleaseRank(left.Label);
        var rightRank = GetPreReleaseRank(right.Label);
        var labelCompare =
            leftRank >= 0 && rightRank >= 0
                ? leftRank.CompareTo(rightRank)
                : string.Compare(
                    left.Label,
                    right.Label,
                    StringComparison.OrdinalIgnoreCase);
        if (labelCompare != 0)
        {
            return labelCompare;
        }

        var count = Math.Max(
            left.Serial.Length,
            right.Serial.Length);
        for (var index = 0; index < count; index++)
        {
            var leftPart =
                index < left.Serial.Length
                    ? left.Serial[index]
                    : 0;
            var rightPart =
                index < right.Serial.Length
                    ? right.Serial[index]
                    : 0;
            var serialCompare =
                leftPart.CompareTo(rightPart);
            if (serialCompare != 0)
            {
                return serialCompare;
            }
        }

        return 0;
    }

    private static int GetPreReleaseRank(string label) =>
        label.ToLowerInvariant() switch
        {
            "alpha" => 0,
            "beta" => 1,
            "preview" => 1,
            "rc" => 2,
            _ => -1
        };

    private void SetSnapshot(
        NavBRAutoUpdateSnapshot snapshot)
    {
        lock (_stateLock)
        {
            _snapshot = snapshot;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
        _checkGate.Dispose();
    }

    private sealed record ReleaseAsset(
        string Name,
        string Url);

    private sealed record ReleaseCandidate(
        string TagName,
        string Version,
        string ReleaseUrl,
        string InstallerName,
        string InstallerUrl,
        string ChecksumsUrl);

    private sealed record PreparedInstaller(
        string InstallerPath,
        string ExpectedSha256);

    private sealed record ParsedVersion(
        int Major,
        int Minor,
        int Patch,
        string? Label,
        int[] Serial);
}
