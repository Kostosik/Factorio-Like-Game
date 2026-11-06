
using Engine.Worlds;
using Engine.Worlds.Spatial;
using Engine.Worlds.Tiles;

namespace GameProject.Map;

public sealed class MapGrid
{
    public int Width = 64;
    public int Height = 64;

    // Пока единый тип земли для всей карты.
    public const uint GroundTileValue = 1;

    private readonly World _world;

    public int ChunkCount { get; }

    public MapGrid(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;

        ChunkCount = CreateInitialChunks();
    }

    private int CreateInitialChunks()
    {
        var size = _world.ChunkSize;

        var countX = DivideRoundUp(Width, size.Width);
        var countY = DivideRoundUp(Height, size.Height);

        for (var y = 0; y < countY; y++)
        {
            for (var x = 0; x < countX; x++)
            {
                var position = new ChunkPosition(x, y);

                // Не перезаписываем уже загруженные данные.
                if (_world.TryGetChunk(position, out _))
                {
                    continue;
                }

                // Пока потоковую загрузку для нашей карты
                // не используем. Не допускаем скрытую
                // перезапись зарегистрированного чанка.
                if (_world.Chunks.TryGet(position, out _))
                {
                    throw new InvalidOperationException(
                        $"Map chunk '{position}' exists but is not loaded.");
                }

                var chunk = _world.CreateChunk(position);

                chunk.Tiles.Fill(
                    new Tile(GroundTileValue));
            }
        }

        return countX * countY;
    }

    public bool Contains(int x, int y)
    {
        return
            (uint)x < Width &&
            (uint)y < Height;
    }

    public Tile GetTile(int x, int y)
    {
        var chunk = GetRequiredChunk(x, y);

        var worldPosition = new WorldPosition(x, y);
        var localPosition = _world.GetLocalPosition(worldPosition);

        return chunk.Tiles.Get(localPosition);
    }

    public void SetTile(int x, int y, Tile tile)
    {
        var chunk = GetRequiredChunk(x, y);

        var worldPosition = new WorldPosition(x, y);
        var localPosition = _world.GetLocalPosition(worldPosition);

        chunk.Tiles.Set(localPosition, tile);
    }

    public bool TryGetTile(int x, int y, out Tile tile)
    {
        if (!Contains(x, y))
        {
            tile = Tile.Empty;
            return false;
        }

        var worldPosition = new WorldPosition(x, y);
        var chunkPosition = _world.GetChunkPosition(worldPosition);

        if (!_world.TryGetChunk(chunkPosition, out var chunk) ||
            chunk is null)
        {
            tile = Tile.Empty;
            return false;
        }

        var localPosition = _world.GetLocalPosition(worldPosition);

        tile = chunk.Tiles.Get(localPosition);

        return true;
    }

    private Chunk GetRequiredChunk(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Cell ({x}, {y}) is outside the map.");
        }

        var worldPosition = new WorldPosition(x, y);
        var chunkPosition = _world.GetChunkPosition(worldPosition);

        if (!_world.TryGetChunk(chunkPosition, out var chunk) ||
            chunk is null)
        {
            throw new InvalidOperationException(
                $"Map chunk '{chunkPosition}' is not loaded.");
        }

        return chunk;
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return (int)(((long)value + divisor - 1) / divisor);
    }
}
