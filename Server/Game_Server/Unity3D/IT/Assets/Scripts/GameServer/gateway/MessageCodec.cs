using System;
using System.Collections.Generic;
using System.Text;
using IslaTortuga.GameServer.Control;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>Mensaje de red con la forma { type, payload }.</summary>
    public sealed class NetMessage
    {
        public string Type { get; }
        public IDictionary<string, object> Payload { get; }

        public NetMessage(string type, IDictionary<string, object> payload)
        {
            Type = type;
            Payload = payload;
        }
    }

    /// <summary>
    /// Codifica y decodifica mensajes realtime { type, payload } usando el escritor
    /// (Json) y el parser (JsonReader) thread-safe del Game Server. Sin dependencias
    /// externas y usable desde el hilo del socket.
    /// </summary>
    public static class MessageCodec
    {
        /// <summary>
        /// Construye un mensaje. payloadJson debe ser ya un objeto JSON válido (o null).
        /// </summary>
        public static string Encode(string type, string payloadJson = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":").Append(Json.Str(type));
            if (payloadJson != null)
            {
                sb.Append(",\"payload\":").Append(payloadJson);
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static NetMessage Decode(string text)
        {
            if (!(JsonReader.Parse(text) is Dictionary<string, object> obj))
            {
                throw new FormatException("El mensaje no es un objeto JSON.");
            }
            var type = JsonReader.GetString(obj, "type");
            if (string.IsNullOrEmpty(type))
            {
                throw new FormatException("Mensaje sin campo 'type'.");
            }
            var payload = obj.TryGetValue("payload", out var p)
                ? p as IDictionary<string, object>
                : null;
            return new NetMessage(type, payload);
        }
    }
}
