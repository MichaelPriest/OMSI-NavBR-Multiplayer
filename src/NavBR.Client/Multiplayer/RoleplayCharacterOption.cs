namespace NavBR.Client.Multiplayer;

internal sealed record RoleplayCharacterOption(
    string Id,
    string DisplayName,
    string SourceValue,
    int DefinitionPointer,
    bool IsActiveDriver = false)
{
    public string DisplayLabel => IsActiveDriver
        ? $"★ {DisplayName}"
        : DisplayName;
}
