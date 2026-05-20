using System.Text.Json;

namespace IslaTortuga.Server.Content;

public sealed class ContentIndexLoader
{
    public ContentIndex Load(string contentRootPath)
    {
        var indexPath = Path.Combine(contentRootPath, "index.json");

        if (!File.Exists(indexPath))
        {
            return new ContentIndex(string.Empty, Array.Empty<ContentPackDescriptor>());
        }

        var json = File.ReadAllText(indexPath);
        var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var defaultContentPackId = root.GetProperty("defaultContentPackId").GetString() ?? string.Empty;

        var packs = root.GetProperty("packs")
            .EnumerateArray()
            .Select(pack => new ContentPackDescriptor(
                pack.GetProperty("contentPackId").GetString() ?? string.Empty,
                pack.GetProperty("version").GetString() ?? string.Empty,
                pack.GetProperty("mapId").GetString() ?? string.Empty,
                pack.GetProperty("manifestUrl").GetString() ?? string.Empty))
            .ToArray();

        return new ContentIndex(defaultContentPackId, packs);
    }
}
