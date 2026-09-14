using System.Globalization;
using System.IO;

namespace NavBR.Client.Overlay;

internal sealed record OmsiKeyboardConflictResult(
    bool ConfigFound,
    DateTime LastWriteUtc,
    IReadOnlyDictionary<int, IReadOnlyList<string>> EventsByScanCode)
{
    public IReadOnlyList<string> GetEvents(int scanCode) =>
        EventsByScanCode.TryGetValue(scanCode, out var events)
            ? events
            : Array.Empty<string>();

    public bool IsInUse(int scanCode) => GetEvents(scanCode).Count > 0;
}

internal static class OmsiKeyboardConflictDetector
{
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
            var eventsByScanCode = new Dictionary<int, HashSet<string>>();
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

                if (!eventsByScanCode.TryGetValue(keyCode, out var events))
                {
                    events = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    eventsByScanCode[keyCode] = events;
                }

                events.Add(eventName);
            }

            return new OmsiKeyboardConflictResult(
                ConfigFound: true,
                File.GetLastWriteTimeUtc(keyboardPath),
                eventsByScanCode.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()));
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

            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                index--;
                return null;
            }

            return value;
        }

        return null;
    }

    private static OmsiKeyboardConflictResult Empty() =>
        new(false, DateTime.MinValue, new Dictionary<int, IReadOnlyList<string>>());
}
