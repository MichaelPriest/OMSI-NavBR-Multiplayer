using System.Globalization;
using System.IO;

namespace NavBR.Client.Overlay;

internal readonly record struct OmsiKeyboardBinding(int ScanCode, int ModifierMask);

internal sealed record OmsiKeyboardConflictResult(
    bool ConfigFound,
    DateTime LastWriteUtc,
    IReadOnlyDictionary<OmsiKeyboardBinding, IReadOnlyList<string>> EventsByBinding)
{
    public IReadOnlyList<string> GetEvents(int scanCode, int modifierMask) =>
        EventsByBinding.TryGetValue(new OmsiKeyboardBinding(scanCode, modifierMask), out var events)
            ? events
            : Array.Empty<string>();

    public bool IsInUse(int scanCode, int modifierMask) => GetEvents(scanCode, modifierMask).Count > 0;
}

internal static class OmsiKeyboardConflictDetector
{
    private const int SupportedModifierMask =
        NavBRHotkeyCatalog.OmsiShiftModifier | NavBRHotkeyCatalog.OmsiCtrlModifier;

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
            var eventsByBinding = new Dictionary<OmsiKeyboardBinding, HashSet<string>>();
            var lines = File.ReadAllLines(keyboardPath);

            for (var index = 0; index < lines.Length; index++)
            {
                if (!string.Equals(lines[index].Trim(), "[entry]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var eventName = ReadNextValue(lines, ref index);
                var keyCodeText = ReadNextValue(lines, ref index);
                var flagsText = ReadNextValue(lines, ref index);

                if (string.IsNullOrWhiteSpace(eventName) ||
                    !int.TryParse(keyCodeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyCode))
                {
                    continue;
                }

                var flags = 0;
                _ = int.TryParse(flagsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out flags);
                var binding = new OmsiKeyboardBinding(keyCode, flags & SupportedModifierMask);

                if (!eventsByBinding.TryGetValue(binding, out var events))
                {
                    events = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    eventsByBinding[binding] = events;
                }

                events.Add(eventName);
            }

            return new OmsiKeyboardConflictResult(
                ConfigFound: true,
                File.GetLastWriteTimeUtc(keyboardPath),
                eventsByBinding.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray()));
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
        new(
            false,
            DateTime.MinValue,
            new Dictionary<OmsiKeyboardBinding, IReadOnlyList<string>>());
}
