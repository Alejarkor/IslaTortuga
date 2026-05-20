namespace IslaTortuga.Server.Content;

public sealed record ContentPackDescriptor(
    string ContentPackId,
    string Version,
    string MapId,
    string ManifestUrl);
