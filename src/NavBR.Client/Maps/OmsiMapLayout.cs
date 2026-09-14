namespace NavBR.Client.Maps;

public sealed record OmsiMapLayout(
    int MinGridX,
    int MinGridY,
    int MaxGridX,
    int MaxGridY,
    bool UsesWorldCoordinates,
    double? TileSize)
{
    public int GridWidth => MaxGridX - MinGridX + 1;
    public int GridHeight => MaxGridY - MinGridY + 1;

    public double? WorldWidth => TileSize is double tileSize
        ? GridWidth * tileSize
        : null;

    public double? WorldHeight => TileSize is double tileSize
        ? GridHeight * tileSize
        : null;
}
