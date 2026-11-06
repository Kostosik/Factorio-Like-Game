
using Engine.Core.Math;
using Engine.Worlds.Spatial;

namespace GameProject.Map;

public sealed class DimetricProjection
{
    public float TileWidth { get; }

    public float TileHeight { get; }

    public DimetricProjection(
        float tileWidth = 64.0f,
        float tileHeight = 32.0f)
    {
        if (!float.IsFinite(tileWidth) || tileWidth <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(tileWidth));
        }

        if (!float.IsFinite(tileHeight) || tileHeight <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(tileHeight));
        }

        TileWidth = tileWidth;
        TileHeight = tileHeight;
    }

    /// <summary>
    /// Converts a logical cell coordinate into its visual center.
    /// Origin represents the visual center of cell (0, 0).
    /// </summary>
    public Vector2 GridToWorld(
        float gridX,
        float gridY,
        Vector2 origin)
    {
        if (!float.IsFinite(gridX) ||
            !float.IsFinite(gridY) ||
            !float.IsFinite(origin.X) ||
            !float.IsFinite(origin.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(gridX),
                "Coordinates must be finite.");
        }

        var halfWidth = TileWidth * 0.5f;
        var halfHeight = TileHeight * 0.5f;

        return new Vector2(
            origin.X + (gridX - gridY) * halfWidth,
            origin.Y + (gridX + gridY) * halfHeight);
    }

    public Vector2 GridToWorld(
        WorldPosition cell,
        Vector2 origin)
    {
        return GridToWorld(
            cell.X,
            cell.Y,
            origin);
    }

    /// <summary>
    /// Converts visual coordinates into fractional grid coordinates.
    /// </summary>
    public Vector2 WorldToGrid(
        Vector2 worldPosition,
        Vector2 origin)
    {
        if (!float.IsFinite(worldPosition.X) ||
            !float.IsFinite(worldPosition.Y) ||
            !float.IsFinite(origin.X) ||
            !float.IsFinite(origin.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldPosition));
        }

        var dx =
            (worldPosition.X - origin.X) /
            (TileWidth * 0.5f);

        var dy =
            (worldPosition.Y - origin.Y) /
            (TileHeight * 0.5f);

        return new Vector2(
            (dx + dy) * 0.5f,
            (dy - dx) * 0.5f);
    }

    /// <summary>
    /// Finds the nearest cell center to a visual position.
    /// </summary>
    public WorldPosition WorldToCell(
        Vector2 worldPosition,
        Vector2 origin)
    {
        var grid = WorldToGrid(worldPosition, origin);

        return new WorldPosition(
            RoundToCell(grid.X),
            RoundToCell(grid.Y));
    }

    private static int RoundToCell(float value)
    {
        var rounded = Math.Floor((double)value + 0.5);

        if (rounded < int.MinValue || rounded > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return (int)rounded;
    }
}
