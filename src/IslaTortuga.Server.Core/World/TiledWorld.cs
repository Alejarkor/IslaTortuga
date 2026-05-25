using System.Text.Json;

namespace IslaTortuga.Server.Core.World.Tiled;

public sealed class TiledWorldBuilder
{
    public TiledWorldMap BuildFromFile(string mapPath)
    {
        if (!File.Exists(mapPath))
        {
            return new TiledWorldMap(
                mapPath,
                "missing-map",
                0,
                0,
                0,
                0,
                Array.Empty<TiledLayerData>(),
                Array.Empty<TiledTilesetData>());
        }

        using var document = JsonDocument.Parse(File.ReadAllText(mapPath));
        var root = document.RootElement;

        var layers = root.TryGetProperty("layers", out var layersElement)
            ? layersElement.EnumerateArray().Select(ParseLayer).ToArray()
            : Array.Empty<TiledLayerData>();

        var tilesets = root.TryGetProperty("tilesets", out var tilesetsElement)
            ? tilesetsElement.EnumerateArray().Select(ParseTileset).ToArray()
            : Array.Empty<TiledTilesetData>();

        return new TiledWorldMap(
            mapPath,
            Path.GetFileNameWithoutExtension(mapPath),
            ReadInt(root, "width"),
            ReadInt(root, "height"),
            ReadInt(root, "tilewidth"),
            ReadInt(root, "tileheight"),
            layers,
            tilesets);
    }

    private static TiledLayerData ParseLayer(JsonElement element)
    {
        var tileData = element.TryGetProperty("data", out var dataElement)
            ? dataElement.EnumerateArray().Select(item => item.GetInt32()).ToArray()
            : Array.Empty<int>();

        var objects = element.TryGetProperty("objects", out var objectsElement)
            ? objectsElement.EnumerateArray().Select(ParseObject).ToArray()
            : Array.Empty<TiledObjectData>();

        return new TiledLayerData(
            ReadInt(element, "id"),
            ReadString(element, "name"),
            ReadString(element, "type"),
            ReadNullableString(element, "class"),
            ReadBool(element, "visible", true),
            tileData,
            objects);
    }

    private static TiledObjectData ParseObject(JsonElement element)
    {
        return new TiledObjectData(
            ReadInt(element, "id"),
            ReadString(element, "name"),
            ReadNullableString(element, "type"),
            ReadNullableString(element, "class"),
            ReadDouble(element, "x"),
            ReadDouble(element, "y"),
            ReadDouble(element, "width"),
            ReadDouble(element, "height"),
            ParseProperties(element),
            ParsePoints(element));
    }

    private static TiledTilesetData ParseTileset(JsonElement element)
    {
        var firstGlobalId = ReadInt(element, "firstgid");

        var tiles = element.TryGetProperty("tiles", out var tilesElement)
            ? tilesElement.EnumerateArray().Select(tile => ParseTileDefinition(tile, firstGlobalId)).ToArray()
            : Array.Empty<TiledTileDefinition>();

        return new TiledTilesetData(
            ReadString(element, "name"),
            firstGlobalId,
            ReadInt(element, "tilewidth"),
            ReadInt(element, "tileheight"),
            tiles);
    }

    private static TiledTileDefinition ParseTileDefinition(JsonElement element, int firstGlobalId)
    {
        var localTileId = ReadInt(element, "id");

        var collisionShapes =
            element.TryGetProperty("objectgroup", out var collisionElement) &&
            collisionElement.TryGetProperty("objects", out var collisionObjects)
                ? collisionObjects.EnumerateArray().Select(ParseCollisionShape).ToArray()
                : Array.Empty<TiledCollisionShape>();

        return new TiledTileDefinition(
            firstGlobalId + localTileId,
            localTileId,
            ReadNullableString(element, "type"),
            ReadNullableString(element, "class"),
            ParseProperties(element),
            collisionShapes);
    }

    private static TiledCollisionShape ParseCollisionShape(JsonElement element)
    {
        return new TiledCollisionShape(
            ReadInt(element, "id"),
            ResolveShapeType(element),
            ReadDouble(element, "x"),
            ReadDouble(element, "y"),
            ReadDouble(element, "width"),
            ReadDouble(element, "height"),
            ParsePoints(element),
            ParseProperties(element));
    }

    private static string ResolveShapeType(JsonElement element)
    {
        if (element.TryGetProperty("polygon", out _))
        {
            return "polygon";
        }

        if (element.TryGetProperty("polyline", out _))
        {
            return "polyline";
        }

        if (ReadBool(element, "ellipse"))
        {
            return "ellipse";
        }

        if (ReadBool(element, "point"))
        {
            return "point";
        }

        return "rectangle";
    }

    private static IReadOnlyList<TiledShapePoint> ParsePoints(JsonElement element)
    {
        if (element.TryGetProperty("polygon", out var polygon))
        {
            return polygon.EnumerateArray()
                .Select(point => new TiledShapePoint(ReadDouble(point, "x"), ReadDouble(point, "y")))
                .ToArray();
        }

        if (element.TryGetProperty("polyline", out var polyline))
        {
            return polyline.EnumerateArray()
                .Select(point => new TiledShapePoint(ReadDouble(point, "x"), ReadDouble(point, "y")))
                .ToArray();
        }

        return Array.Empty<TiledShapePoint>();
    }

    private static IReadOnlyList<TiledPropertyValue> ParseProperties(JsonElement element)
    {
        if (!element.TryGetProperty("properties", out var propertiesElement))
        {
            return Array.Empty<TiledPropertyValue>();
        }

        return propertiesElement.EnumerateArray()
            .Select(property => new TiledPropertyValue(
                ReadString(property, "name"),
                ReadString(property, "type", "string"),
                ReadPropertyValue(property)))
            .ToArray();
    }

    private static object? ReadPropertyValue(JsonElement property)
    {
        if (!property.TryGetProperty("value", out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.String => valueElement.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when valueElement.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number => valueElement.GetDouble(),
            _ => valueElement.GetRawText(),
        };
    }

    private static int ReadInt(JsonElement element, string name, int defaultValue = 0)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : defaultValue;
    }

    private static double ReadDouble(JsonElement element, string name, double defaultValue = 0)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetDouble(out var parsed)
            ? parsed
            : defaultValue;
    }

    private static bool ReadBool(JsonElement element, string name, bool defaultValue = false)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : defaultValue;
    }

    private static string ReadString(JsonElement element, string name, string defaultValue = "")
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? defaultValue
            : defaultValue;
    }

    private static string? ReadNullableString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

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

public sealed class TiledLayerData
{
    public TiledLayerData(
        int id,
        string name,
        string type,
        string? className,
        bool visible,
        IReadOnlyList<int> tileData,
        IReadOnlyList<TiledObjectData> objects)
    {
        Id = id;
        Name = name;
        Type = type;
        Class = className;
        Visible = visible;
        TileData = tileData;
        Objects = objects;
    }

    public int Id { get; }

    public string Name { get; }

    public string Type { get; }

    public string? Class { get; }

    public bool Visible { get; }

    public IReadOnlyList<int> TileData { get; }

    public IReadOnlyList<TiledObjectData> Objects { get; }
}

public sealed class TiledObjectData
{
    public TiledObjectData(
        int id,
        string name,
        string? type,
        string? className,
        double x,
        double y,
        double width,
        double height,
        IReadOnlyList<TiledPropertyValue> properties,
        IReadOnlyList<TiledShapePoint> points)
    {
        Id = id;
        Name = name;
        Type = type;
        Class = className;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Properties = properties;
        Points = points;
    }

    public int Id { get; }

    public string Name { get; }

    public string? Type { get; }

    public string? Class { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public IReadOnlyList<TiledPropertyValue> Properties { get; }

    public IReadOnlyList<TiledShapePoint> Points { get; }
}

public sealed class TiledTilesetData
{
    public TiledTilesetData(
        string name,
        int firstGlobalId,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<TiledTileDefinition> tiles)
    {
        Name = name;
        FirstGlobalId = firstGlobalId;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Tiles = tiles;
    }

    public string Name { get; }

    public int FirstGlobalId { get; }

    public int TileWidth { get; }

    public int TileHeight { get; }

    public IReadOnlyList<TiledTileDefinition> Tiles { get; }
}

public sealed class TiledTileDefinition
{
    public TiledTileDefinition(
        int globalTileId,
        int localTileId,
        string? type,
        string? className,
        IReadOnlyList<TiledPropertyValue> properties,
        IReadOnlyList<TiledCollisionShape> collisionShapes)
    {
        GlobalTileId = globalTileId;
        LocalTileId = localTileId;
        Type = type;
        Class = className;
        Properties = properties;
        CollisionShapes = collisionShapes;
    }

    public int GlobalTileId { get; }

    public int LocalTileId { get; }

    public string? Type { get; }

    public string? Class { get; }

    public IReadOnlyList<TiledPropertyValue> Properties { get; }

    public IReadOnlyList<TiledCollisionShape> CollisionShapes { get; }
}

public sealed class TiledCollisionShape
{
    public TiledCollisionShape(
        int id,
        string shapeType,
        double x,
        double y,
        double width,
        double height,
        IReadOnlyList<TiledShapePoint> points,
        IReadOnlyList<TiledPropertyValue> properties)
    {
        Id = id;
        ShapeType = shapeType;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Points = points;
        Properties = properties;
    }

    public int Id { get; }

    public string ShapeType { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public IReadOnlyList<TiledShapePoint> Points { get; }

    public IReadOnlyList<TiledPropertyValue> Properties { get; }
}

public sealed class TiledShapePoint
{
    public TiledShapePoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }
}

public sealed class TiledPropertyValue
{
    public TiledPropertyValue(string name, string type, object? value)
    {
        Name = name;
        Type = type;
        Value = value;
    }

    public string Name { get; }

    public string Type { get; }

    public object? Value { get; }
}
