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

    public static IReadOnlyList<NavBRHotkeyDefinition> Options { get; } =
    [
        new("F9", 0x78, 67, 0),
        new("F10", 0x79, 68, 0),
        new("Shift+F9", 0x78, 67, OmsiShiftModifier),
        new("Shift+F10", 0x79, 68, OmsiShiftModifier),
        new("Ctrl+F9", 0x78, 67, OmsiCtrlModifier),
        new("Ctrl+F10", 0x79, 68, OmsiCtrlModifier),
        new("Ctrl+Shift+F9", 0x78, 67, OmsiCtrlModifier | OmsiShiftModifier),
        new("Ctrl+Shift+F10", 0x79, 68, OmsiCtrlModifier | OmsiShiftModifier)
    ];

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
}
