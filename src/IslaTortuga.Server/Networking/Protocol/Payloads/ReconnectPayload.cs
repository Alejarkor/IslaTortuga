namespace IslaTortuga.Server.Networking.Protocol.Payloads;

public sealed record ReconnectPayload(string GameTicket, string? PreviousSessionId);
