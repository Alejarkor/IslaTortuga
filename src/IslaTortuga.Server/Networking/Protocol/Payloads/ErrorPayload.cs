namespace IslaTortuga.Server.Networking.Protocol.Payloads;

public sealed record ErrorPayload(string Code, string Message, bool Retryable = false);
