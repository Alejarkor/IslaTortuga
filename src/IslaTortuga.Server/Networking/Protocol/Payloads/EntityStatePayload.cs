namespace IslaTortuga.Server.Networking.Protocol.Payloads;

public sealed record EntityStatePayload(
    string EntityId,
    string EntityType,
    float X,
    float Y,
    string Facing,
    string? DisplayName = null);
