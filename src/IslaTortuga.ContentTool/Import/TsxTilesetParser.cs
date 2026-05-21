using System.Globalization;
using System.Xml.Linq;
using System.Text.Json.Nodes;

namespace IslaTortuga.ContentTool.Import;

internal static class TsxTilesetParser
{
    public static (JsonObject TilesetJson, string ImageReference) Parse(
        string tsxPath,
        int firstGlobalId)
    {
        var document = XDocument.Load(tsxPath);
        var root = document.Root ?? throw new InvalidOperationException("El TSX no tiene nodo raiz.");
        if (!string.Equals(root.Name.LocalName, "tileset", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El archivo seleccionado no es un tileset TSX valido.");
        }

        var imageElement = root.Element("image");
        if (imageElement is null)
        {
            throw new InvalidOperationException(
                $"El tileset {Path.GetFileName(tsxPath)} no tiene un <image> principal. Ese formato todavia no esta soportado por la herramienta.");
        }

        var tilesetJson = new JsonObject
        {
            ["firstgid"] = firstGlobalId,
            ["name"] = GetAttribute(root, "name") ?? Path.GetFileNameWithoutExtension(tsxPath),
            ["tilewidth"] = GetIntAttribute(root, "tilewidth"),
            ["tileheight"] = GetIntAttribute(root, "tileheight"),
            ["tilecount"] = GetOptionalIntAttribute(root, "tilecount"),
            ["columns"] = GetOptionalIntAttribute(root, "columns"),
            ["spacing"] = GetOptionalIntAttribute(root, "spacing") ?? 0,
            ["margin"] = GetOptionalIntAttribute(root, "margin") ?? 0,
            ["image"] = GetAttribute(imageElement, "source") ?? string.Empty,
            ["imagewidth"] = GetOptionalIntAttribute(imageElement, "width") ?? 0,
            ["imageheight"] = GetOptionalIntAttribute(imageElement, "height") ?? 0,
        };

        var tilesetClass = GetAttribute(root, "class");
        if (!string.IsNullOrWhiteSpace(tilesetClass))
        {
            tilesetJson["class"] = tilesetClass;
        }

        var rootProperties = ParseProperties(root.Element("properties"));
        if (rootProperties.Count > 0)
        {
            tilesetJson["properties"] = rootProperties;
        }

        var tileOffset = root.Element("tileoffset");
        if (tileOffset is not null)
        {
            tilesetJson["tileoffset"] = new JsonObject
            {
                ["x"] = GetOptionalIntAttribute(tileOffset, "x") ?? 0,
                ["y"] = GetOptionalIntAttribute(tileOffset, "y") ?? 0,
            };
        }

        var tiles = new JsonArray();
        foreach (var tileElement in root.Elements("tile"))
        {
            var tileObject = new JsonObject
            {
                ["id"] = GetIntAttribute(tileElement, "id"),
            };

            var tileType = GetAttribute(tileElement, "type");
            if (!string.IsNullOrWhiteSpace(tileType))
            {
                tileObject["type"] = tileType;
            }

            var tileClass = GetAttribute(tileElement, "class");
            if (!string.IsNullOrWhiteSpace(tileClass))
            {
                tileObject["class"] = tileClass;
            }

            var tileProperties = ParseProperties(tileElement.Element("properties"));
            if (tileProperties.Count > 0)
            {
                tileObject["properties"] = tileProperties;
            }

            var objectGroup = tileElement.Element("objectgroup");
            if (objectGroup is not null)
            {
                tileObject["objectgroup"] = ParseObjectGroup(objectGroup);
            }

            var animation = tileElement.Element("animation");
            if (animation is not null)
            {
                tileObject["animation"] = ParseAnimation(animation);
            }

            tiles.Add(tileObject);
        }

        if (tiles.Count > 0)
        {
            tilesetJson["tiles"] = tiles;
        }

        return (tilesetJson, GetAttribute(imageElement, "source") ?? string.Empty);
    }

    private static JsonArray ParseProperties(XElement? propertiesElement)
    {
        var result = new JsonArray();

        if (propertiesElement is null)
        {
            return result;
        }

        foreach (var propertyElement in propertiesElement.Elements("property"))
        {
            var propertyType = GetAttribute(propertyElement, "type") ?? "string";
            var propertyValue = ParsePropertyValue(propertyElement, propertyType);

            var propertyObject = new JsonObject
            {
                ["name"] = GetAttribute(propertyElement, "name") ?? string.Empty,
                ["type"] = propertyType,
                ["value"] = propertyValue is JsonNode node ? node : JsonValue.Create(propertyValue),
            };

            result.Add(propertyObject);
        }

        return result;
    }

    private static object? ParsePropertyValue(XElement propertyElement, string propertyType)
    {
        var rawValue = GetAttribute(propertyElement, "value") ?? propertyElement.Value;

        if (string.Equals(propertyType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            return bool.TryParse(rawValue, out var boolValue) && boolValue;
        }

        if (string.Equals(propertyType, "int", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                ? intValue
                : 0;
        }

        if (string.Equals(propertyType, "float", StringComparison.OrdinalIgnoreCase))
        {
            return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue)
                ? floatValue
                : 0d;
        }

        return rawValue;
    }

    private static JsonObject ParseObjectGroup(XElement objectGroupElement)
    {
        var objectGroup = new JsonObject
        {
            ["draworder"] = GetAttribute(objectGroupElement, "draworder") ?? "index",
            ["id"] = GetOptionalIntAttribute(objectGroupElement, "id") ?? 0,
            ["name"] = GetAttribute(objectGroupElement, "name") ?? string.Empty,
            ["opacity"] = GetOptionalDoubleAttribute(objectGroupElement, "opacity") ?? 1d,
            ["type"] = "objectgroup",
            ["visible"] = GetOptionalBoolAttribute(objectGroupElement, "visible") ?? true,
            ["x"] = GetOptionalDoubleAttribute(objectGroupElement, "x") ?? 0d,
            ["y"] = GetOptionalDoubleAttribute(objectGroupElement, "y") ?? 0d,
        };

        var objects = new JsonArray();
        foreach (var objectElement in objectGroupElement.Elements("object"))
        {
            var obj = new JsonObject
            {
                ["id"] = GetOptionalIntAttribute(objectElement, "id") ?? 0,
                ["name"] = GetAttribute(objectElement, "name") ?? string.Empty,
                ["opacity"] = GetOptionalDoubleAttribute(objectElement, "opacity") ?? 1d,
                ["rotation"] = GetOptionalDoubleAttribute(objectElement, "rotation") ?? 0d,
                ["visible"] = GetOptionalBoolAttribute(objectElement, "visible") ?? true,
                ["x"] = GetOptionalDoubleAttribute(objectElement, "x") ?? 0d,
                ["y"] = GetOptionalDoubleAttribute(objectElement, "y") ?? 0d,
                ["width"] = GetOptionalDoubleAttribute(objectElement, "width") ?? 0d,
                ["height"] = GetOptionalDoubleAttribute(objectElement, "height") ?? 0d,
            };

            var type = GetAttribute(objectElement, "type");
            if (!string.IsNullOrWhiteSpace(type))
            {
                obj["type"] = type;
            }

            var objectClass = GetAttribute(objectElement, "class");
            if (!string.IsNullOrWhiteSpace(objectClass))
            {
                obj["class"] = objectClass;
            }

            if (GetOptionalBoolAttribute(objectElement, "point") == true)
            {
                obj["point"] = true;
            }

            if (GetOptionalBoolAttribute(objectElement, "ellipse") == true)
            {
                obj["ellipse"] = true;
            }

            var polygon = objectElement.Element("polygon");
            if (polygon is not null)
            {
                obj["polygon"] = ParsePointList(polygon);
            }

            var polyline = objectElement.Element("polyline");
            if (polyline is not null)
            {
                obj["polyline"] = ParsePointList(polyline);
            }

            var properties = ParseProperties(objectElement.Element("properties"));
            if (properties.Count > 0)
            {
                obj["properties"] = properties;
            }

            objects.Add(obj);
        }

        objectGroup["objects"] = objects;
        return objectGroup;
    }

    private static JsonArray ParseAnimation(XElement animationElement)
    {
        var frames = new JsonArray();
        foreach (var frameElement in animationElement.Elements("frame"))
        {
            frames.Add(new JsonObject
            {
                ["tileid"] = GetOptionalIntAttribute(frameElement, "tileid") ?? 0,
                ["duration"] = GetOptionalIntAttribute(frameElement, "duration") ?? 100,
            });
        }

        return frames;
    }

    private static JsonArray ParsePointList(XElement listElement)
    {
        var points = new JsonArray();
        var rawPoints = GetAttribute(listElement, "points") ?? string.Empty;
        var pointEntries = rawPoints.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pointEntry in pointEntries)
        {
            var parts = pointEntry.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            points.Add(new JsonObject
            {
                ["x"] = double.Parse(parts[0], CultureInfo.InvariantCulture),
                ["y"] = double.Parse(parts[1], CultureInfo.InvariantCulture),
            });
        }

        return points;
    }

    private static string? GetAttribute(XElement element, string name)
    {
        return element.Attribute(name)?.Value;
    }

    private static int GetIntAttribute(XElement element, string name)
    {
        return GetOptionalIntAttribute(element, name) ?? 0;
    }

    private static int? GetOptionalIntAttribute(XElement element, string name)
    {
        var value = GetAttribute(element, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? GetOptionalDoubleAttribute(XElement element, string name)
    {
        var value = GetAttribute(element, name);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool? GetOptionalBoolAttribute(XElement element, string name)
    {
        var value = GetAttribute(element, name);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }
}
