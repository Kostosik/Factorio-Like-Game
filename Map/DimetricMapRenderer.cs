
using Engine.Camera;
using Engine.Core.Math;
using Engine.Graphics;
using Engine.Graphics.Commands;
using Engine.Graphics.Resources;
using Engine.Graphics2D.Commands;

namespace GameProject.Map;

public sealed class DimetricMapRenderer : IDisposable
{


    private const int TextureWidth = 64;
    private const int TextureHeight = 32;

    private readonly IGraphicsDevice _graphics;
    private readonly Camera2D _camera;
    private readonly MapGrid _map;
    private readonly DimetricProjection _projection;

    private readonly TextureHandle[] _groundTextures = new TextureHandle[2];

    private bool _disposed;

    public DimetricMapRenderer(
        IGraphicsDevice graphics,
        Camera2D camera,
        MapGrid map,
        DimetricProjection projection)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(projection);

        _graphics = graphics;
        _camera = camera;
        _map = map;
        _projection = projection;

        _groundTextures[0] = CreateDiamondTexture(
            new Rgba(91, 119, 76),
            new Rgba(48, 70, 43));

        try
        {
            _groundTextures[1] = CreateDiamondTexture(
                new Rgba(98, 125, 81),
                new Rgba(48, 70, 43));
        }
        catch
        {
            _graphics.Textures.Destroy(_groundTextures[0]);
            throw;
        }
    }

    public void Render(Vector2 origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tileSize = new Vector2(
            _projection.TileWidth,
            _projection.TileHeight);

        var halfTileSize = tileSize * 0.5f;

        var uv = new Rectangle(
            0.0f,
            0.0f,
            1.0f,
            1.0f);

        // Порядок: сначала дальние ряды, затем ближние.
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                if (!_map.TryGetTile(x, y, out var tile) ||
                    tile.Value == 0)
                {
                    continue;
                }

                var center = _projection.GridToWorld(
                    x,
                    y,
                    origin);

                // Графический API принимает верхний левый угол,
                // а проекция возвращает центр клетки.
                var position = center - halfTileSize;

                if (!_camera.IsVisible(
                        new Rectangle(
                            position.X,
                            position.Y,
                            tileSize.X,
                            tileSize.Y)))
                {
                    continue;
                }

                // Небольшая вариация оттенка для земли.
                var texture = _groundTextures[(x + y) & 1];

                _graphics.Submit(
                    new DrawWorldTextureCommand(
                        texture,
                        position,
                        tileSize,
                        uv));
            }
        }
    }

    private TextureHandle CreateDiamondTexture(
        Rgba fill,
        Rgba outline)
    {
        var pixels = new byte[
            TextureWidth * TextureHeight * 4];

        var halfWidth = TextureWidth * 0.5f;
        var halfHeight = TextureHeight * 0.5f;

        for (var y = 0; y < TextureHeight; y++)
        {
            for (var x = 0; x < TextureWidth; x++)
            {
                var normalizedX =
                    MathF.Abs(
                        (x + 0.5f - halfWidth) / halfWidth);

                var normalizedY =
                    MathF.Abs(
                        (y + 0.5f - halfHeight) / halfHeight);

                var distance = normalizedX + normalizedY;

                // За пределами ромба пиксель прозрачен.
                if (distance > 1.0f)
                {
                    continue;
                }

                var color = distance >= 0.90f
                    ? outline
                    : fill;

                var index =
                    (y * TextureWidth + x) * 4;

                pixels[index] = color.R;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.B;
                pixels[index + 3] = 255;
            }
        }

        return _graphics.Textures.Create(
            new TextureData(
                TextureWidth,
                TextureHeight,
                TextureFormat.Rgba8,
                pixels));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var texture in _groundTextures)
        {
            if (texture.IsValid &&
                _graphics.Textures.Exists(texture))
            {
                _graphics.Textures.Destroy(texture);
            }
        }

        _disposed = true;
    }

    private readonly record struct Rgba(
        byte R,
        byte G,
        byte B);
}
