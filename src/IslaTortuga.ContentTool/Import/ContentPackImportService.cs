using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IslaTortuga.ContentTool.Import;

internal sealed class ContentPackImportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
    };

    public ContentSelection NormalizeContentSelection(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            throw new InvalidOperationException("Selecciona una carpeta de content-packs.");
        }

        var fullPath = Path.GetFullPath(selectedPath);
        if (File.Exists(Path.Combine(fullPath, "manifest.json")))
        {
            var versionDirectory = new DirectoryInfo(fullPath);
            var root = versionDirectory.Parent?.FullName ?? fullPath;
            return new ContentSelection
            {
                ContentPacksRootPath = root,
                SuggestedVersion = versionDirectory.Name,
            };
        }

        return new ContentSelection
        {
            ContentPacksRootPath = fullPath,
            SuggestedVersion = null,
        };
    }

    public ImportScanResult Scan(string mapPath, IReadOnlyDictionary<string, string> dependencyOverrides)
    {
        if (!File.Exists(mapPath))
        {
            throw new FileNotFoundException("No existe el mapa seleccionado.", mapPath);
        }

        var fullMapPath = Path.GetFullPath(mapPath);
        var mapDirectory = Path.GetDirectoryName(fullMapPath)
            ?? throw new InvalidOperationException("No se ha podido resolver la carpeta del mapa.");

        var mapRoot = JsonNode.Parse(File.ReadAllText(fullMapPath))?.AsObject()
            ?? throw new InvalidOperationException("El mapa TMJ no es un JSON valido.");

        var tilesets = mapRoot["tilesets"]?.AsArray()
            ?? throw new InvalidOperationException("El mapa no contiene la seccion tilesets.");

        var dependencies = new List<DependencyDescriptor>();
        var resolvedTilesets = new List<ResolvedTileset>();

        for (var index = 0; index < tilesets.Count; index++)
        {
            if (tilesets[index] is not JsonObject tilesetNode)
            {
                continue;
            }

            var processed = ProcessTileset(index, tilesetNode, mapDirectory, dependencyOverrides);
            dependencies.AddRange(processed.Dependencies);

            if (processed.ResolvedTileset is not null)
            {
                resolvedTilesets.Add(processed.ResolvedTileset);
            }
        }

        return new ImportScanResult
        {
            SourceMapPath = fullMapPath,
            SuggestedMapId = SlugUtility.ToSlug(Path.GetFileNameWithoutExtension(fullMapPath)),
            SourceMapRoot = mapRoot,
            Dependencies = dependencies
                .GroupBy(item => item.Key)
                .Select(group => group.First())
                .ToArray(),
            ResolvedTilesets = resolvedTilesets,
        };
    }

    public ImportResult Import(ImportRequest request)
    {
        var scan = Scan(request.SourceMapPath, request.DependencyOverrides);
        if (scan.HasMissingDependencies)
        {
            throw new InvalidOperationException(
                "Todavia hay dependencias sin resolver. Resuelvelas antes de exportar el content pack.");
        }

        var contentRoot = NormalizeContentSelection(request.ContentPacksRootPath).ContentPacksRootPath;
        var versionDirectory = Path.Combine(contentRoot, request.Version);
        var mapsDirectory = Path.Combine(versionDirectory, "maps");
        var tilesetsDirectory = Path.Combine(versionDirectory, "tilesets");
        var definitionsDirectory = Path.Combine(versionDirectory, "definitions");

        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(versionDirectory);
        Directory.CreateDirectory(mapsDirectory);
        Directory.CreateDirectory(tilesetsDirectory);
        Directory.CreateDirectory(definitionsDirectory);

        var outputFileNames = BuildOutputImageNames(scan.ResolvedTilesets);
        var outputMapRoot = CloneJsonObject(scan.SourceMapRoot);
        var outputTilesets = outputMapRoot["tilesets"]?.AsArray()
            ?? throw new InvalidOperationException("El mapa no contiene tilesets en la exportacion.");

        var copiedFiles = new List<string>();
        var manifestEntries = new List<JsonObject>();
        var visualTilesets = new List<JsonObject>();

        var mapFileId = $"map.{request.MapId}";
        var mapOutputPath = Path.Combine(mapsDirectory, $"{request.MapId}.tmj");

        foreach (var resolvedTileset in scan.ResolvedTilesets)
        {
            var outputImageFileName = outputFileNames[resolvedTileset.Index];
            var outputImagePath = Path.Combine(tilesetsDirectory, outputImageFileName);

            File.Copy(resolvedTileset.ImageSourcePath, outputImagePath, overwrite: true);
            copiedFiles.Add(outputImagePath);

            var updatedTileset = CloneJsonObject(resolvedTileset.TilesetJson);
            updatedTileset["image"] = $"../tilesets/{outputImageFileName}";
            outputTilesets[resolvedTileset.Index] = updatedTileset;

            var fileId = $"tileset.{SlugUtility.ToSlug(resolvedTileset.Name)}";
            manifestEntries.Add(CreateManifestEntry(
                fileId,
                "image",
                $"/content/{request.Version}/tilesets/{outputImageFileName}",
                outputImagePath));

            visualTilesets.Add(new JsonObject
            {
                ["tilesetName"] = resolvedTileset.Name,
                ["textureKey"] = SlugUtility.ToTextureKey(resolvedTileset.Name),
                ["imageFileId"] = fileId,
            });
        }

        File.WriteAllText(mapOutputPath, outputMapRoot.ToJsonString(SerializerOptions));
        copiedFiles.Add(mapOutputPath);

        manifestEntries.Insert(0, CreateManifestEntry(
            mapFileId,
            "map",
            $"/content/{request.Version}/maps/{request.MapId}.tmj",
            mapOutputPath));

        UpdateManifest(versionDirectory, request, manifestEntries);
        UpdateVisualDefinitions(definitionsDirectory, request.MapId, mapFileId, visualTilesets);
        EnsureJsonFile(Path.Combine(definitionsDirectory, "entity-archetypes.json"), "{\n}\n");
        EnsureJsonFile(Path.Combine(definitionsDirectory, "item-definitions.json"), "{\n}\n");
        EnsureJsonFile(Path.Combine(definitionsDirectory, "rules.json"), "{\n}\n");
        UpdateContentIndex(contentRoot, request);

        return new ImportResult
        {
            ManifestPath = Path.Combine(versionDirectory, "manifest.json"),
            MapOutputPath = mapOutputPath,
            CopiedFiles = copiedFiles,
        };
    }

    private static Dictionary<int, string> BuildOutputImageNames(IReadOnlyList<ResolvedTileset> resolvedTilesets)
    {
        var usedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var outputNames = new Dictionary<int, string>();

        foreach (var tileset in resolvedTilesets)
        {
            var extension = Path.GetExtension(tileset.ImageSourcePath);
            var originalFileName = Path.GetFileName(tileset.ImageSourcePath);
            var candidate = originalFileName;
            var slugBase = SlugUtility.ToSlug(tileset.Name);
            var sequence = 1;

            while (usedNames.TryGetValue(candidate, out var existingPath) &&
                   !string.Equals(existingPath, tileset.ImageSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                candidate = $"{slugBase}-{sequence}{extension}";
                sequence++;
            }

            usedNames[candidate] = tileset.ImageSourcePath;
            outputNames[tileset.Index] = candidate;
        }

        return outputNames;
    }

    private static JsonObject CreateManifestEntry(string id, string type, string url, string filePath)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["type"] = type,
            ["url"] = url,
            ["hash"] = $"sha256-{ComputeSha256(filePath)}",
            ["size"] = new FileInfo(filePath).Length,
        };
    }

    private static void UpdateManifest(
        string versionDirectory,
        ImportRequest request,
        IReadOnlyList<JsonObject> importedEntries)
    {
        var manifestPath = Path.Combine(versionDirectory, "manifest.json");
        var manifestRoot = File.Exists(manifestPath)
            ? JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject() ?? new JsonObject()
            : new JsonObject();

        manifestRoot["contentPackId"] = request.ContentPackId;
        manifestRoot["version"] = request.Version;
        manifestRoot["mapId"] = request.MapId;

        var files = manifestRoot["files"]?.AsArray() ?? new JsonArray();
        manifestRoot["files"] = files;
        PruneStaleManifestEntries(files, importedEntries);

        foreach (var entry in importedEntries)
        {
            var entryId = entry["id"]?.GetValue<string>() ?? string.Empty;
            var existing = files
                .OfType<JsonObject>()
                .FirstOrDefault(item =>
                    string.Equals(item["id"]?.GetValue<string>(), entryId, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                files.Add(entry);
                continue;
            }

            var index = files.IndexOf(existing);
            files[index] = entry;
        }

        File.WriteAllText(manifestPath, manifestRoot.ToJsonString(SerializerOptions));
    }

    private static void PruneStaleManifestEntries(JsonArray files, IReadOnlyList<JsonObject> importedEntries)
    {
        var importedIds = importedEntries
            .Select(entry => entry["id"]?.GetValue<string>())
            .OfType<string>()
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var staleEntries = files
            .OfType<JsonObject>()
            .Where(item => ShouldPruneManifestEntry(item, importedIds))
            .ToArray();

        foreach (var staleEntry in staleEntries)
        {
            files.Remove(staleEntry);
        }
    }

    private static bool ShouldPruneManifestEntry(JsonObject entry, HashSet<string> importedIds)
    {
        var entryId = entry["id"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        if (importedIds.Contains(entryId))
        {
            return false;
        }

        return entryId.StartsWith("map.", StringComparison.OrdinalIgnoreCase) ||
               entryId.StartsWith("tileset.", StringComparison.OrdinalIgnoreCase);
    }

    private static void UpdateVisualDefinitions(
        string definitionsDirectory,
        string mapId,
        string mapFileId,
        IReadOnlyList<JsonObject> visualTilesets)
    {
        var visualDefinitionsPath = Path.Combine(definitionsDirectory, "visual-definitions.json");
        var visualDefinitions = File.Exists(visualDefinitionsPath)
            ? JsonNode.Parse(File.ReadAllText(visualDefinitionsPath))?.AsObject() ?? new JsonObject()
            : new JsonObject();

        var maps = visualDefinitions["maps"]?.AsObject() ?? new JsonObject();
        visualDefinitions["maps"] = maps;

        maps[mapId] = new JsonObject
        {
            ["mapFileId"] = mapFileId,
            ["tilesets"] = new JsonArray(visualTilesets.Select(item => CloneJsonObject(item)).ToArray()),
        };

        if (visualDefinitions["players"] is null)
        {
            visualDefinitions["players"] = new JsonObject
            {
                ["default"] = new JsonObject
                {
                    ["textureKey"] = "player",
                    ["imageFileId"] = "spritesheet.player",
                    ["frameWidth"] = 32,
                    ["frameHeight"] = 32,
                    ["animations"] = new JsonObject
                    {
                        ["idleDown"] = "player-idle-down",
                        ["idleUp"] = "player-idle-up",
                        ["idleSide"] = "player-idle-side",
                        ["walkDown"] = "player-walk-down",
                        ["walkUp"] = "player-walk-up",
                        ["walkSide"] = "player-walk-side",
                    },
                },
            };
        }

        File.WriteAllText(visualDefinitionsPath, visualDefinitions.ToJsonString(SerializerOptions));
    }

    private static void UpdateContentIndex(string contentRoot, ImportRequest request)
    {
        var indexPath = Path.Combine(contentRoot, "index.json");
        var indexRoot = File.Exists(indexPath)
            ? JsonNode.Parse(File.ReadAllText(indexPath))?.AsObject() ?? new JsonObject()
            : new JsonObject();

        var packs = indexRoot["packs"]?.AsArray() ?? new JsonArray();
        indexRoot["packs"] = packs;

        var manifestUrl = $"/content/{request.Version}/manifest.json";
        var existing = packs.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(item["contentPackId"]?.GetValue<string>(), request.ContentPackId, StringComparison.OrdinalIgnoreCase));

        var packEntry = new JsonObject
        {
            ["contentPackId"] = request.ContentPackId,
            ["version"] = request.Version,
            ["mapId"] = request.MapId,
            ["manifestUrl"] = manifestUrl,
        };

        if (existing is null)
        {
            packs.Add(packEntry);
        }
        else
        {
            packs[packs.IndexOf(existing)] = packEntry;
        }

        if (request.SetAsDefaultPack || indexRoot["defaultContentPackId"] is null)
        {
            indexRoot["defaultContentPackId"] = request.ContentPackId;
        }

        File.WriteAllText(indexPath, indexRoot.ToJsonString(SerializerOptions));
    }

    private static void EnsureJsonFile(string path, string fallbackContent)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, fallbackContent);
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static JsonObject CloneJsonObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString())?.AsObject()
            ?? throw new InvalidOperationException("No se ha podido clonar un nodo JSON.");
    }

    private ProcessedTileset ProcessTileset(
        int index,
        JsonObject tilesetNode,
        string mapDirectory,
        IReadOnlyDictionary<string, string> dependencyOverrides)
    {
        var dependencies = new List<DependencyDescriptor>();
        var firstGid = tilesetNode["firstgid"]?.GetValue<int>() ?? 0;

        if (tilesetNode["source"] is JsonValue sourceValue)
        {
            var sourceReference = sourceValue.GetValue<string>();
            var sourceKey = BuildDependencyKey("tsx", sourceReference);
            var resolvedSourcePath = ResolveDependencyPath(mapDirectory, sourceReference, sourceKey, dependencyOverrides);

            dependencies.Add(new DependencyDescriptor
            {
                Key = sourceKey,
                DisplayName = Path.GetFileName(sourceReference),
                Kind = "tileset-tsx",
                Reference = sourceReference,
                ResolvedPath = resolvedSourcePath,
            });

            if (string.IsNullOrWhiteSpace(resolvedSourcePath))
            {
                return new ProcessedTileset(dependencies, null);
            }

            var (tsxTilesetJson, imageReference) = TsxTilesetParser.Parse(resolvedSourcePath, firstGid);
            var imageKey = BuildDependencyKey("image", $"{tsxTilesetJson["name"]?.GetValue<string>()}:{imageReference}");
            var tsxDirectory = Path.GetDirectoryName(resolvedSourcePath) ?? mapDirectory;
            var resolvedImagePath = ResolveDependencyPath(tsxDirectory, imageReference, imageKey, dependencyOverrides);

            dependencies.Add(new DependencyDescriptor
            {
                Key = imageKey,
                DisplayName = Path.GetFileName(imageReference),
                Kind = "tileset-image",
                Reference = imageReference,
                ResolvedPath = resolvedImagePath,
            });

            if (string.IsNullOrWhiteSpace(resolvedImagePath))
            {
                return new ProcessedTileset(dependencies, null);
            }

            return new ProcessedTileset(
                dependencies,
                new ResolvedTileset
                {
                    Index = index,
                    Name = tsxTilesetJson["name"]?.GetValue<string>() ?? $"tileset-{index}",
                    TilesetJson = tsxTilesetJson,
                    ImageSourcePath = resolvedImagePath,
                    ImageReference = imageReference,
                });
        }

        var tilesetName = tilesetNode["name"]?.GetValue<string>() ?? $"tileset-{index}";
        var imageReferenceValue = tilesetNode["image"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(imageReferenceValue))
        {
            throw new InvalidOperationException(
                $"El tileset {tilesetName} no tiene imagen embebida ni referencia TSX externa.");
        }

        var imageKeyEmbedded = BuildDependencyKey("image", $"{tilesetName}:{imageReferenceValue}");
        var resolvedEmbeddedImage = ResolveDependencyPath(mapDirectory, imageReferenceValue, imageKeyEmbedded, dependencyOverrides);

        dependencies.Add(new DependencyDescriptor
        {
            Key = imageKeyEmbedded,
            DisplayName = Path.GetFileName(imageReferenceValue),
            Kind = "tileset-image",
            Reference = imageReferenceValue,
            ResolvedPath = resolvedEmbeddedImage,
        });

        if (string.IsNullOrWhiteSpace(resolvedEmbeddedImage))
        {
            return new ProcessedTileset(dependencies, null);
        }

        var embeddedTileset = CloneJsonObject(tilesetNode);
        embeddedTileset.Remove("source");

        return new ProcessedTileset(
            dependencies,
            new ResolvedTileset
            {
                Index = index,
                Name = tilesetName,
                TilesetJson = embeddedTileset,
                ImageSourcePath = resolvedEmbeddedImage,
                ImageReference = imageReferenceValue,
            });
    }

    private static string BuildDependencyKey(string prefix, string reference)
    {
        return $"{prefix}|{reference.Replace('\\', '/').ToLowerInvariant()}";
    }

    private static string? ResolveDependencyPath(
        string baseDirectory,
        string reference,
        string dependencyKey,
        IReadOnlyDictionary<string, string> dependencyOverrides)
    {
        if (dependencyOverrides.TryGetValue(dependencyKey, out var overriddenPath) &&
            File.Exists(overriddenPath))
        {
            return Path.GetFullPath(overriddenPath);
        }

        var candidatePath = Path.IsPathRooted(reference)
            ? reference
            : Path.GetFullPath(Path.Combine(baseDirectory, reference));

        return File.Exists(candidatePath)
            ? candidatePath
            : null;
    }

    private sealed record ProcessedTileset(
        IReadOnlyList<DependencyDescriptor> Dependencies,
        ResolvedTileset? ResolvedTileset);
}
