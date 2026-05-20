namespace IslaTortuga.Server.Networking.Protocol.Payloads;

public sealed record WorldSnapshotPayload(
    long ServerTick,
    string RoomId,
    IReadOnlyList<EntityStatePayload> Entities);
