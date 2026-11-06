using Engine.Core.Math;
using Engine.Graphics.Commands;
using Engine.Graphics.DebugGraphics;
using Engine.Input;
using Engine.Networking.Replication;
using Engine.Runtime;
using Engine.Serialization.SaveLoad.Ecs;
using Engine.Serialization.SaveLoad.Worlds;
using GameProject.Map;

namespace GameProject;

internal sealed class GameMain : EngineGame
{
    private string _hoveredCellText =
    "Cell: outside map";
    private static readonly InputAction CameraMoveUp =
    new("CameraMoveUp");

    private static readonly InputAction CameraMoveDown =
        new("CameraMoveDown");

    private static readonly InputAction CameraMoveLeft =
        new("CameraMoveLeft");

    private static readonly InputAction CameraMoveRight =
        new("CameraMoveRight");

    private readonly IPointerInput? _pointerInput;

    private readonly string _startupScenePath;
    private MapGrid? _mapGrid;
    private DimetricProjection? _projection;
    private DimetricMapRenderer? _mapRenderer;
    public GameMain(
        EngineRuntime runtime,
        string startupScenePath,
        IPointerInput? pointerInput = null)
        : base(runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            startupScenePath);

        _startupScenePath =
            Path.GetFullPath(startupScenePath);

        _pointerInput = pointerInput;
    }

    protected override void ConfigureRuntime()
    {
        InputActions.Bind(
    CameraMoveUp,
    new InputBinding("Keyboard.W"));

        InputActions.Bind(
            CameraMoveDown,
            new InputBinding("Keyboard.S"));

        InputActions.Bind(
            CameraMoveLeft,
            new InputBinding("Keyboard.A"));

        InputActions.Bind(
            CameraMoveRight,
            new InputBinding("Keyboard.D"));

        if (!File.Exists(_startupScenePath))
        {
            throw new FileNotFoundException(
                "Startup scene was not found.",
                _startupScenePath);
        }

        var serializers =
            new EcsComponentSerializerRegistry();

        serializers.RegisterEngineComponents();
        serializers.RegisterNetworkComponents();
        var saveLoad =
            new WorldSaveLoadService(
                serializers);

        saveLoad.Load(
            _startupScenePath,
            Runtime.World);

        // Создаём игровую карту.
        _mapGrid = new MapGrid(Runtime.World);

        // Базовая проекция 2:1.
        _projection = new DimetricProjection(
            tileWidth: 64.0f,
            tileHeight: 32.0f);
    }

    protected override void OnUpdate(
    Engine.Core.Time.TimeSnapshot time)
    {
        var input = Runtime.Services.Input;
        var camera = Runtime.Services.Camera;

        var movement = new Vector2(
            (input.IsDown(CameraMoveRight) ? 1.0f : 0.0f) -
            (input.IsDown(CameraMoveLeft) ? 1.0f : 0.0f),

            (input.IsDown(CameraMoveDown) ? 1.0f : 0.0f) -
            (input.IsDown(CameraMoveUp) ? 1.0f : 0.0f));

        var movementLength = MathF.Sqrt(
            movement.X * movement.X +
            movement.Y * movement.Y);

        if (movementLength > 0.0f)
        {
            movement /= movementLength;

            const float panSpeed = 700.0f;

            var deltaSeconds =
                (float)time.Delta.TotalSeconds;

            camera.Position +=
                movement *
                (panSpeed * deltaSeconds / camera.Zoom);
        }
        ClampCameraToMap();
        if (_pointerInput is null)
        {
            return;
        }

        var scroll = _pointerInput.ScrollDelta;

        if (scroll == 0.0f)
        {
            return;
        }

        var mousePosition = _pointerInput.Position;

        // Запоминаем мировую точку под курсором.
        var worldBefore =
            camera.ScreenToWorld(mousePosition);

        camera.Zoom = Math.Clamp(
            camera.Zoom * MathF.Pow(1.15f, scroll),
            0.2f,
            3.0f);
        ClampCameraToMap();
        // После изменения масштаба возвращаем
        // эту точку под курсор.
        var worldAfter =
            camera.ScreenToWorld(mousePosition);

        camera.Position +=
            worldBefore - worldAfter;
        UpdateHoveredCell();
    }

    private void UpdateHoveredCell()
    {
        if (_pointerInput is null)
        {
            _hoveredCellText = "Cell: unavailable";
            return;
        }

        var camera = Runtime.Services.Camera;

        // Экранные координаты мыши -> мировые.
        var worldPosition =
            camera.ScreenToWorld(
                _pointerInput.Position);

        // Мировые координаты -> координаты клетки.
        var cell =
            _projection.WorldToCell(
                worldPosition,
                Vector2.Zero);

        if (_mapGrid.TryGetTile(
                cell.X,
                cell.Y,
                out _))
        {
            _hoveredCellText =
                $"Cell: {cell.X}, {cell.Y}";
        }
        else
        {
            _hoveredCellText =
                "Cell: outside map";
        }
    }

    private void ClampCameraToMap()
    {
        var camera = Runtime.Services.Camera;

        var halfTileWidth =
            _projection.TileWidth * 0.5f;

        var halfTileHeight =
            _projection.TileHeight * 0.5f;

        // Внешние границы диметрической проекции
        // всей прямоугольной карты.
        var minX =
            -_mapGrid.Height * halfTileWidth;

        var maxX =
            _mapGrid.Width * halfTileWidth;

        var minY =
            -halfTileHeight;

        var maxY =
            (_mapGrid.Width + _mapGrid.Height - 1)
            * halfTileHeight;

        var halfViewWidth =
            camera.ViewportSize.X /
            (2.0f * camera.Zoom);

        var halfViewHeight =
            camera.ViewportSize.Y /
            (2.0f * camera.Zoom);

        var position = camera.Position;

        camera.Position = new Vector2(
            ClampAxis(
                position.X,
                minX,
                maxX,
                halfViewWidth),

            ClampAxis(
                position.Y,
                minY,
                maxY,
                halfViewHeight));
    }

    private static float ClampAxis(
        float value,
        float min,
        float max,
        float halfView)
    {
        var allowedMin = min + halfView;
        var allowedMax = max - halfView;

        // Окно больше карты по этой оси:
        // фиксируем камеру в центре.
        if (allowedMin > allowedMax)
        {
            return (min + max) * 0.5f;
        }

        return Math.Clamp(
            value,
            allowedMin,
            allowedMax);
    }

    protected override void OnInitialize()
    {
        if (_mapGrid is null || _projection is null)
        {
            throw new InvalidOperationException(
                "Map was not initialized.");
        }

        _mapRenderer = new DimetricMapRenderer(
            Runtime.Services.Graphics,
            Runtime.Services.Camera,
            _mapGrid,
            _projection);

        var mapCenter = _projection.GridToWorld(
            (_mapGrid.Width - 1) * 0.5f,
            (_mapGrid.Height - 1) * 0.5f,
            Vector2.Zero);

        Runtime.Services.Camera.Position = mapCenter;

        Console.WriteLine(
            $"Map initialized: {_mapGrid.Width}x{_mapGrid.Height}");

        Console.WriteLine(
            $"Map chunks: {_mapGrid.ChunkCount}");


    }

    protected override void OnRender(
        double interpolationAlpha)
    {
        _mapRenderer?.Render(Vector2.Zero);

        Runtime.Services.Graphics.Submit(
    new DrawDebugTextCommand(
        _hoveredCellText,
        new Vector2(24.0f, 48.0f),
        DebugColor.White,
        2.0f,
        DebugRenderSpace.Screen));
    }

    protected override void OnShutdown()
    {
        _mapRenderer?.Dispose();
        _mapRenderer = null;
    }
}
