namespace NavBR.Client.Maps;

public static class RoadmapTransform
{
    public static bool TryToPixel(
        OmsiMapLayout layout,
        int imagePixelWidth,
        int imagePixelHeight,
        int gridX,
        int gridY,
        double tileX,
        double tileY,
        out double pixelX,
        out double pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        if (layout.TileSize is not double tileSize ||
            layout.WorldWidth is not double worldWidth ||
            layout.WorldHeight is not double worldHeight ||
            imagePixelWidth <= 0 ||
            imagePixelHeight <= 0 ||
            worldWidth <= 0 ||
            worldHeight <= 0)
        {
            return false;
        }

        // OMSI tile coordinates grow from the lower-left map origin. Roadmap
        // bitmap coordinates grow from the upper-left, so the Y axis is flipped.
        var worldX = (gridX - layout.MinGridX) * tileSize + tileX;
        var worldY = (gridY - layout.MinGridY) * tileSize + tileY;

        pixelX = worldX * imagePixelWidth / worldWidth;
        pixelY = imagePixelHeight - (worldY * imagePixelHeight / worldHeight);

        return double.IsFinite(pixelX) && double.IsFinite(pixelY);
    }
}
