using System.Globalization;
using System.IO;

namespace NavBR.Client.Overlay;

internal sealed record OmsiKeyboardConflictResult(
    bool ConfigFound,
    DateTime LastWriteUtc,
    IReadOnlyList<string> F9Events,
    IReadOnlyList<string> F10Events)
{
    public bool F9InUse => F9Events.Count > 0;
    public bool F10InUse => F10Events.Count > 0;
}

internal static class OmsiKeyboardConflictDetector
{
    // OMSI keyboard.cfg uses DirectInput DIK scan codes. F9/F10 are DIK_F9/DIK_F10.
    internal const int F9ScanCode = 67;
    internal const int F10ScanCode = 68;

    public static OmsiKeyboardConflictResult AnalyzeProcessInstallation(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            var executablePath = process.MainModule?.FileName;
            var installDirectory = string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Path.GetDirectoryName(executablePath);

            return AnalyzeInstallDirectory(installDirectory);
        }
        catch
        {
            return Empty();
        }
    }

    public static OmsiKeyboardConflictResult AnalyzeInstallDirectory(string? installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return Empty();
        }

        var keyboardPath = Path.Combine(installDirectory, "Inputs", "keyboard.cfg");
        if (!File.Exists(keyboardPath))
        {
            return Empty();
        }

        try
        {
            var f9Events = new List<string>();
            var f10Events = new List<string>();
            var lines = File.ReadAllLines(keyboardPath);

            for (var index = 0; index < lines.Length; index++)
            {
                if (!string.Equals(lines[index].Trim(), "[entry]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var eventName = ReadNextValue(lines, ref index);
                var keyCodeText = ReadNextValue(lines, ref index);
                if (string.IsNullOrWhiteSpace(eventName) ||
                    !int.TryParse(keyCodeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyCode))
                {
                    continue;
                }

                if (keyCode == F9ScanCode)
                {
                    f9Events.Add(eventName);
                }
                else if (keyCode == F10ScanCode)
                {
                    f10Events.Add(eventName);
                }
            }

            return new OmsiKeyboardConflictResult(
                ConfigFound: true,
                File.GetLastWriteTimeUtc(keyboardPath),
                f9Events.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                f10Events.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch
        {
            return Empty();
        }
    }

    private static string? ReadNextValue(string[] lines, ref int index)
    {
        while (++index < lines.Length)
        {
            var value = lines[index].Trim();
            if (value.Length == 0 || value.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (value.StartsWith('[', StringComparison.Ordinal))
            {
                index--;
                return null;
            }

            return value;
        }

        return null;
    }

    private static OmsiKeyboardConflictResult Empty() =>
        new(false, DateTime.MinValue, Array.Empty<string>(), Array.Empty<string>());
}
