namespace IslaTortuga.Server.Networking.Protocol.Payloads;

public sealed record AuthAcceptedPayload(
    string SessionId,
    string UserId,
    string DisplayName,
    string RoomId,
    string PlayerEntityId);
