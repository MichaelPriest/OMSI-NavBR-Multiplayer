using System.Diagnostics;
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
                var sha256 = ComputeSha256(executablePath);

                results.Add(new OmsiProcessInfo(
                    process.Id,
                    executablePath,
                    installDirectory,
                    version,
                    sha256));
            }
            catch (Exception)
            {
                // A process can disappear between enumeration and inspection,
                // or Windows can deny access. Skip it and keep looking.
            }
        }

        return results;
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
