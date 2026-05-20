using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslaTortuga.Server.Networking.Protocol;

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
