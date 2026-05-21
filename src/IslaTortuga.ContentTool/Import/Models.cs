using System.Text.Json.Nodes;

namespace IslaTortuga.ContentTool.Import;

internal sealed class ImportScanResult
{
    public required string SourceMapPath { get; init; }

    public required string SuggestedMapId { get; init; }

    public required JsonObject SourceMapRoot { get; init; }

    public required IReadOnlyList<DependencyDescriptor> Dependencies { get; init; }

    public required IReadOnlyList<ResolvedTileset> ResolvedTilesets { get; init; }

    public bool HasMissingDependencies => Dependencies.Any(item => item.IsMissing);
}

internal sealed class ResolvedTileset
{
    public required int Index { get; init; }

    public required string Name { get; init; }

    public required JsonObject TilesetJson { get; init; }

    public required string ImageSourcePath { get; init; }

    public required string ImageReference { get; init; }
}

internal sealed class DependencyDescriptor
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public required string Kind { get; init; }

    public required string Reference { get; init; }

    public string? ResolvedPath { get; init; }

    public bool IsMissing => string.IsNullOrWhiteSpace(ResolvedPath) || !File.Exists(ResolvedPath);
}

internal sealed class ImportRequest
{
    public required string SourceMapPath { get; init; }

    public required string ContentPacksRootPath { get; init; }

    public required string Version { get; init; }

    public required string ContentPackId { get; init; }

    public required string MapId { get; init; }

    public bool SetAsDefaultPack { get; init; }

    public required IReadOnlyDictionary<string, string> DependencyOverrides { get; init; }
}

internal sealed class ImportResult
{
    public required string ManifestPath { get; init; }

    public required string MapOutputPath { get; init; }

    public required IReadOnlyList<string> CopiedFiles { get; init; }
}

internal sealed class ContentSelection
{
    public required string ContentPacksRootPath { get; init; }

    public string? SuggestedVersion { get; init; }
}
