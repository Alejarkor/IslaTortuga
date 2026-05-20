using System.Text.Json;

namespace IslaTortuga.Server.World.Tiled;

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
            ReadBool(element, "visible", defaultValue: true),
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
            ? tilesElement.EnumerateArray()
                .Select(tile => ParseTileDefinition(tile, firstGlobalId))
                .ToArray()
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
                .Select(point => new TiledShapePoint(
                    ReadDouble(point, "x"),
                    ReadDouble(point, "y")))
                .ToArray();
        }

        if (element.TryGetProperty("polyline", out var polyline))
        {
            return polyline.EnumerateArray()
                .Select(point => new TiledShapePoint(
                    ReadDouble(point, "x"),
                    ReadDouble(point, "y")))
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
