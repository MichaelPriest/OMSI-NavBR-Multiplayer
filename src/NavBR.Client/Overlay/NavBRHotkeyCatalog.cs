namespace NavBR.Client.Overlay;

public sealed record NavBRHotkeyDefinition(
    string Name,
    int VirtualKey,
    int OmsiScanCode);

public static class NavBRHotkeyCatalog
{
    public const string DefaultChatHotkey = "F9";
    public const string DefaultVoiceHotkey = "F10";

    public static IReadOnlyList<NavBRHotkeyDefinition> Options { get; } =
    [
        new("F6", 0x75, 64),
        new("F7", 0x76, 65),
        new("F8", 0x77, 66),
        new("F9", 0x78, 67),
        new("F10", 0x79, 68)
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
