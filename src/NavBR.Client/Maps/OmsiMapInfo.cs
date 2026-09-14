namespace NavBR.Client.Maps;

public sealed record OmsiMapInfo(
    string FolderName,
    string DisplayName,
    string DirectoryPath,
    string GlobalConfigPath,
    string? RoadmapPath,
    int TileCount,
    string? CompatibilityId);
