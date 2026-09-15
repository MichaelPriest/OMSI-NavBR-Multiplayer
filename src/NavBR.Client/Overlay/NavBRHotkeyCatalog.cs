namespace NavBR.Client.Overlay;

public sealed record NavBRHotkeyDefinition(
    string Name,
    int VirtualKey,
    int OmsiScanCode,
    int OmsiModifierMask);

public static class NavBRHotkeyCatalog
{
    public const int OmsiShiftModifier = 2;
    public const int OmsiCtrlModifier = 4;

    public const string DefaultChatHotkey = "F9";
    public const string DefaultVoiceHotkey = "F10";

    private static readonly (string Name, int VirtualKey, int ScanCode)[] BaseKeys =
    [
        ("F1", 0x70, 59),
        ("F2", 0x71, 60),
        ("F3", 0x72, 61),
        ("F4", 0x73, 62),
        // F5-F8 are intentionally omitted because OMSI commonly uses them.
        ("F9", 0x78, 67),
        ("F10", 0x79, 68),
        ("F11", 0x7A, 87),
        ("F12", 0x7B, 88)
    ];

    public static IReadOnlyList<NavBRHotkeyDefinition> Options { get; } = BuildOptions();

    public static NavBRHotkeyDefinition Resolve(string? name, string fallback)
    {
        var match = Options.FirstOrDefault(option =>
            string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        return Options.First(option =>
            string.Equals(option.Name, fallback, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<NavBRHotkeyDefinition> BuildOptions()
    {
        var result = new List<NavBRHotkeyDefinition>();
        foreach (var key in BaseKeys)
        {
            result.Add(new(key.Name, key.VirtualKey, key.ScanCode, 0));
            result.Add(new($"Shift+{key.Name}", key.VirtualKey, key.ScanCode, OmsiShiftModifier));
            result.Add(new($"Ctrl+{key.Name}", key.VirtualKey, key.ScanCode, OmsiCtrlModifier));
            result.Add(new(
                $"Ctrl+Shift+{key.Name}",
                key.VirtualKey,
                key.ScanCode,
                OmsiCtrlModifier | OmsiShiftModifier));
        }

        return result;
    }
}
