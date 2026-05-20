using System.Text.Json;

namespace IslaTortuga.Server.Networking.Protocol;

public sealed record NetworkEnvelope
{
    public string Op { get; init; } = string.Empty;

    public string? RequestId { get; init; }

    public long? SentAt { get; init; }

    public JsonElement? Payload { get; init; }
}
