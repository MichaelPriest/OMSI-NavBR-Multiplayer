using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace NavBR.Client.Omsi;

public sealed class OmsiProcessDetector
{
    public IReadOnlyList<OmsiProcessInfo> FindRunningInstances()
    {
        var results = new List<OmsiProcessInfo>();

        foreach (var process in Process.GetProcessesByName("Omsi"))
        {
            try
            {
                var executablePath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                var installDirectory = Path.GetDirectoryName(executablePath);
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    continue;
                }

                var version = FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? "unknown";
                var runtimeVersion = TryReadRuntimeVersionFromLog(installDirectory);
                var sha256 = ComputeSha256(executablePath);

                results.Add(new OmsiProcessInfo(
                    process.Id,
                    executablePath,
                    installDirectory,
                    version,
                    sha256,
                    runtimeVersion));
            }
            catch (Exception)
            {
                // A process can disappear between enumeration and inspection,
                // or Windows can deny access. Skip it and keep looking.
            }
        }

        return results;
    }

    private static string? TryReadRuntimeVersionFromLog(string installDirectory)
    {
        var logfilePath = Path.Combine(installDirectory, "logfile.txt");
        if (!File.Exists(logfilePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(
                logfilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

            // OMSI writes its runtime version at the beginning of logfile.txt,
            // e.g. "Version: 2.3.004". Only scan the header so detection stays
            // cheap even when the log has grown to thousands of lines.
            for (var index = 0; index < 64 && reader.ReadLine() is { } line; index++)
            {
                var marker = line.IndexOf("Version:", StringComparison.OrdinalIgnoreCase);
                if (marker < 0)
                {
                    continue;
                }

                var value = line[(marker + "Version:".Length)..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
