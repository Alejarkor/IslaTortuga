using System.Text.Json;

namespace IslaTortuga.Server.World;

public sealed class SceneTemplateBuilder
{
    public SceneTemplateData BuildFromFile(string scenePath)
    {
        var fallbackSceneId = string.IsNullOrWhiteSpace(scenePath)
            ? "scene.missing"
            : Path.GetFileNameWithoutExtension(scenePath);

        if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
        {
            return new SceneTemplateData(
                scenePath ?? string.Empty,
                fallbackSceneId,
                fallbackSceneId,
                30f,
                30f,
                Array.Empty<SceneSpawnPointData>());
        }

        using var document = JsonDocument.Parse(File.ReadAllText(scenePath));
        var root = document.RootElement;

        var sceneId = ReadString(root, "sceneId") ?? fallbackSceneId;
        var displayName = ReadString(root, "displayName") ?? sceneId;
        var bounds = root.TryGetProperty("bounds", out var boundsElement) ? boundsElement : default;
        var boundsWidth = Math.Max(ReadSingle(bounds, "width") ?? 30f, 1f);
        var boundsDepth = Math.Max(ReadSingle(bounds, "depth") ?? 30f, 1f);
        var spawnPoints = ReadSpawnPoints(root);

        return new SceneTemplateData(
            scenePath,
            sceneId,
            displayName,
            boundsWidth,
            boundsDepth,
            spawnPoints);
    }

    private static IReadOnlyList<SceneSpawnPointData> ReadSpawnPoints(JsonElement root)
    {
        if (!root.TryGetProperty("spawnPoints", out var spawnPointsElement) ||
            spawnPointsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SceneSpawnPointData>();
        }

        var spawnPoints = new List<SceneSpawnPointData>();
        foreach (var spawnPoint in spawnPointsElement.EnumerateArray())
        {
            var position = spawnPoint.TryGetProperty("position", out var positionElement)
                ? positionElement
                : default;

            spawnPoints.Add(new SceneSpawnPointData(
                ReadString(spawnPoint, "spawnId") ?? $"spawn.{spawnPoints.Count}",
                ReadString(spawnPoint, "spawnType") ?? string.Empty,
                ReadString(spawnPoint, "facing") ?? string.Empty,
                ReadSingle(position, "x") ?? 0f,
                ReadSingle(position, "y") ?? 0f,
                ReadSingle(position, "z") ?? 0f));
        }

        return spawnPoints;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static float? ReadSingle(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number
            ? property.GetSingle()
            : null;
    }
}

public sealed class SceneTemplateData
{
    public SceneTemplateData(
        string sourcePath,
        string sceneId,
        string displayName,
        float boundsWidth,
        float boundsDepth,
        IReadOnlyList<SceneSpawnPointData> spawnPoints)
    {
        SourcePath = sourcePath;
        SceneId = sceneId;
        DisplayName = displayName;
        BoundsWidth = boundsWidth;
        BoundsDepth = boundsDepth;
        SpawnPoints = spawnPoints;
    }

    public string SourcePath { get; }

    public string SceneId { get; }

    public string DisplayName { get; }

    public float BoundsWidth { get; }

    public float BoundsDepth { get; }

    public IReadOnlyList<SceneSpawnPointData> SpawnPoints { get; }
}

public sealed class SceneSpawnPointData
{
    public SceneSpawnPointData(
        string spawnId,
        string spawnType,
        string facing,
        float x,
        float y,
        float z)
    {
        SpawnId = spawnId;
        SpawnType = spawnType;
        Facing = facing;
        X = x;
        Y = y;
        Z = z;
    }

    public string SpawnId { get; }

    public string SpawnType { get; }

    public string Facing { get; }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }
}
