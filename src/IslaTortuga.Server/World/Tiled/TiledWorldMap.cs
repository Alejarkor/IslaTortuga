namespace IslaTortuga.Server.World.Tiled;

public sealed class TiledWorldMap
{
    public TiledWorldMap(
        string sourcePath,
        string name,
        int width,
        int height,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<TiledLayerData> layers,
        IReadOnlyList<TiledTilesetData> tilesets)
    {
        SourcePath = sourcePath;
        Name = name;
        Width = width;
        Height = height;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Layers = layers;
        Tilesets = tilesets;
    }

    public string SourcePath { get; }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public int TileWidth { get; }

    public int TileHeight { get; }

    public IReadOnlyList<TiledLayerData> Layers { get; }

    public IReadOnlyList<TiledTilesetData> Tilesets { get; }

    public TiledLayerData? GetLayer(string name)
    {
        return Layers.FirstOrDefault(layer =>
            string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<TiledObjectData> GetSpawnPoints()
    {
        var spawnLayer = GetLayer("SpawnPoints");
        return spawnLayer?.Objects
            .Where(obj =>
                string.Equals(obj.Class, "PlayerSpawn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(obj.Type, "PlayerSpawn", StringComparison.OrdinalIgnoreCase) ||
                spawnLayer.Name.Equals("SpawnPoints", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            ?? Array.Empty<TiledObjectData>();
    }
}

public sealed record TiledLayerData(
    int Id,
    string Name,
    string Type,
    string? Class,
    bool Visible,
    IReadOnlyList<int> TileData,
    IReadOnlyList<TiledObjectData> Objects);

public sealed record TiledObjectData(
    int Id,
    string Name,
    string? Type,
    string? Class,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<TiledPropertyValue> Properties,
    IReadOnlyList<TiledShapePoint> Points);

public sealed record TiledTilesetData(
    string Name,
    int FirstGlobalId,
    int TileWidth,
    int TileHeight,
    IReadOnlyList<TiledTileDefinition> Tiles);

public sealed record TiledTileDefinition(
    int GlobalTileId,
    int LocalTileId,
    string? Type,
    string? Class,
    IReadOnlyList<TiledPropertyValue> Properties,
    IReadOnlyList<TiledCollisionShape> CollisionShapes);

public sealed record TiledCollisionShape(
    int Id,
    string ShapeType,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<TiledShapePoint> Points,
    IReadOnlyList<TiledPropertyValue> Properties);

public sealed record TiledShapePoint(double X, double Y);

public sealed record TiledPropertyValue(string Name, string Type, object? Value);
