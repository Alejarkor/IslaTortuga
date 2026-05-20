namespace IslaTortuga.Server.Content;

public sealed record ContentIndex(
    string DefaultContentPackId,
    IReadOnlyList<ContentPackDescriptor> Packs);
