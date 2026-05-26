using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IslaTortuga.Server.Core.Protocol;
using UnityEngine;

namespace IslaTortuga.Unity.Networking.Protocol
{
    public static class ProtocolTypes
    {
        public const string AuthJoin = "auth.join";
        public const string AuthReconnect = "auth.reconnect";
        public const string AuthAccepted = "auth.accepted";
        public const string AuthRejected = "auth.rejected";
        public const string PlayerInput = "player.input";
        public const string WorldSnapshot = "world.snapshot";
        public const string Error = "error";
        public const string Ping = "ping";
        public const string Pong = "pong";
    }

    public sealed class IncomingEnvelope
    {
        public IncomingEnvelope(string op, string requestId, string payloadJson)
        {
            Op = op;
            RequestId = requestId;
            PayloadJson = payloadJson;
        }

        public string Op { get; }

        public string RequestId { get; }

        public string PayloadJson { get; }
    }

    public sealed class JoinGamePayload
    {
        public JoinGamePayload(string gameTicket)
        {
            GameTicket = gameTicket;
        }

        public string GameTicket { get; }
    }

    public sealed class ReconnectPayload
    {
        public ReconnectPayload(string gameTicket, string previousSessionId)
        {
            GameTicket = gameTicket;
            PreviousSessionId = previousSessionId;
        }

        public string GameTicket { get; }

        public string PreviousSessionId { get; }
    }

    public sealed class PlayerInputPayload
    {
        public PlayerInputPayload(float moveX, float moveY, int sequence)
        {
            MoveX = moveX;
            MoveY = moveY;
            Sequence = sequence;
        }

        public float MoveX { get; }

        public float MoveY { get; }

        public int Sequence { get; }
    }

    internal static class ProtocolEnvelopeJson
    {
        public static bool TryParseEnvelope(
            string json,
            out IncomingEnvelope envelope,
            out string errorCode,
            out string errorMessage)
        {
            envelope = null;
            errorCode = string.Empty;
            errorMessage = string.Empty;

            Dictionary<string, string> properties;
            if (!TryParseTopLevelObject(json, out properties))
            {
                errorCode = "invalid_json";
                errorMessage = "The message is not valid JSON.";
                return false;
            }

            string rawOp;
            if (!properties.TryGetValue("op", out rawOp) || !TryReadStringValue(rawOp, out var op) || string.IsNullOrWhiteSpace(op))
            {
                errorCode = "invalid_envelope";
                errorMessage = "The network envelope is incomplete.";
                return false;
            }

            properties.TryGetValue("requestId", out var rawRequestId);
            string requestId = null;
            if (!string.IsNullOrWhiteSpace(rawRequestId) && !string.Equals(rawRequestId, "null", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadStringValue(rawRequestId, out requestId))
                {
                    errorCode = "invalid_envelope";
                    errorMessage = "The requestId field is invalid.";
                    return false;
                }
            }

            properties.TryGetValue("payload", out var payloadJson);
            envelope = new IncomingEnvelope(op, requestId, payloadJson);
            return true;
        }

        public static bool TryDeserializeJoinPayload(string payloadJson, out JoinGamePayload payload)
        {
            payload = null;
            var dto = TryDeserialize<JoinGamePayloadDto>(payloadJson);
            if (dto == null || string.IsNullOrWhiteSpace(dto.gameTicket))
            {
                return false;
            }

            payload = new JoinGamePayload(dto.gameTicket);
            return true;
        }

        public static bool TryDeserializeReconnectPayload(string payloadJson, out ReconnectPayload payload)
        {
            payload = null;
            var dto = TryDeserialize<ReconnectPayloadDto>(payloadJson);
            if (dto == null || string.IsNullOrWhiteSpace(dto.gameTicket))
            {
                return false;
            }

            payload = new ReconnectPayload(dto.gameTicket, dto.previousSessionId);
            return true;
        }

        public static bool TryDeserializePlayerInputPayload(string payloadJson, out PlayerInputPayload payload)
        {
            payload = null;
            var dto = TryDeserialize<PlayerInputPayloadDto>(payloadJson);
            if (dto == null)
            {
                return false;
            }

            payload = new PlayerInputPayload(dto.moveX, dto.moveY, dto.sequence);
            return true;
        }

        public static string SerializeEnvelope(string op, object payload, string requestId)
        {
            var builder = new StringBuilder(512);
            builder.Append('{');
            builder.Append("\"op\":");
            AppendQuoted(builder, op);

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                builder.Append(",\"requestId\":");
                AppendQuoted(builder, requestId);
            }

            builder.Append(",\"sentAt\":");
            builder.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"payload\":");
            SerializePayload(builder, payload);
            builder.Append('}');
            return builder.ToString();
        }

        private static T TryDeserialize<T>(string payloadJson) where T : class
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(payloadJson);
            }
            catch
            {
                return null;
            }
        }

        private static void SerializePayload(StringBuilder builder, object payload)
        {
            if (payload == null)
            {
                builder.Append("{}");
                return;
            }

            var authAccepted = payload as AuthAcceptedPayload;
            if (authAccepted != null)
            {
                builder.Append('{');
                AppendProperty(builder, "sessionId", authAccepted.SessionId, false);
                AppendProperty(builder, "userId", authAccepted.UserId, true);
                AppendProperty(builder, "displayName", authAccepted.DisplayName, true);
                AppendProperty(builder, "roomId", authAccepted.RoomId, true);
                AppendProperty(builder, "playerEntityId", authAccepted.PlayerEntityId, true);
                builder.Append('}');
                return;
            }

            var worldSnapshot = payload as WorldSnapshotPayload;
            if (worldSnapshot != null)
            {
                builder.Append('{');
                AppendNumberProperty(builder, "serverTick", worldSnapshot.ServerTick, false);
                AppendProperty(builder, "roomId", worldSnapshot.RoomId, true);
                builder.Append(",\"entities\":[");

                for (var index = 0; index < worldSnapshot.Entities.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    var entity = worldSnapshot.Entities[index];
                    builder.Append('{');
                    AppendProperty(builder, "entityId", entity.EntityId, false);
                    AppendProperty(builder, "entityType", entity.EntityType, true);
                    AppendFloatProperty(builder, "x", entity.X, true);
                    AppendFloatProperty(builder, "y", entity.Y, true);
                    AppendProperty(builder, "facing", entity.Facing, true);
                    AppendNullableProperty(builder, "displayName", entity.DisplayName, true);
                    AppendNullableProperty(builder, "visualId", entity.VisualId, true);
                    builder.Append('}');
                }

                builder.Append("]}");
                return;
            }

            var errorPayload = payload as ErrorPayload;
            if (errorPayload != null)
            {
                builder.Append('{');
                AppendProperty(builder, "code", errorPayload.Code, false);
                AppendProperty(builder, "message", errorPayload.Message, true);
                AppendBooleanProperty(builder, "retryable", errorPayload.Retryable, true);
                builder.Append('}');
                return;
            }

            builder.Append("{}");
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            AppendQuoted(builder, value ?? string.Empty);
        }

        private static void AppendNullableProperty(StringBuilder builder, string name, string value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            AppendQuoted(builder, value);
        }

        private static void AppendNumberProperty(StringBuilder builder, string name, long value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendFloatProperty(StringBuilder builder, string name, float value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendBooleanProperty(StringBuilder builder, string name, bool value, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            AppendQuoted(builder, name);
            builder.Append(':');
            builder.Append(value ? "true" : "false");
        }

        private static void AppendQuoted(StringBuilder builder, string value)
        {
            builder.Append('"');

            if (!string.IsNullOrEmpty(value))
            {
                for (var index = 0; index < value.Length; index++)
                {
                    switch (value[index])
                    {
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            builder.Append(value[index]);
                            break;
                    }
                }
            }

            builder.Append('"');
        }

        private static bool TryParseTopLevelObject(string json, out Dictionary<string, string> properties)
        {
            properties = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var index = 0;
            SkipWhitespace(json, ref index);

            if (index >= json.Length || json[index] != '{')
            {
                return false;
            }

            index++;

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);

                if (index < json.Length && json[index] == '}')
                {
                    index++;
                    SkipWhitespace(json, ref index);
                    return index == json.Length;
                }

                string key;
                if (!TryReadJsonString(json, ref index, out key))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    return false;
                }

                index++;
                SkipWhitespace(json, ref index);

                var valueStart = index;
                if (!TryAdvanceJsonValue(json, ref index))
                {
                    return false;
                }

                properties[key] = json.Substring(valueStart, index - valueStart);

                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    return false;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == '}')
                {
                    continue;
                }

                return false;
            }

            return false;
        }

        private static bool TryAdvanceJsonValue(string json, ref int index)
        {
            if (index >= json.Length)
            {
                return false;
            }

            switch (json[index])
            {
                case '"':
                    string ignored;
                    return TryReadJsonString(json, ref index, out ignored);
                case '{':
                    return TryAdvanceBalanced(json, ref index, '{', '}');
                case '[':
                    return TryAdvanceBalanced(json, ref index, '[', ']');
                default:
                    while (index < json.Length && json[index] != ',' && json[index] != '}' && json[index] != ']')
                    {
                        index++;
                    }

                    return true;
            }
        }

        private static bool TryAdvanceBalanced(string json, ref int index, char openChar, char closeChar)
        {
            var depth = 0;
            var inString = false;
            var isEscaped = false;

            while (index < json.Length)
            {
                var current = json[index++];

                if (inString)
                {
                    if (isEscaped)
                    {
                        isEscaped = false;
                        continue;
                    }

                    if (current == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == openChar)
                {
                    depth++;
                    continue;
                }

                if (current == closeChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadStringValue(string rawJson, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return false;
            }

            var index = 0;
            return TryReadJsonString(rawJson, ref index, out value);
        }

        private static bool TryReadJsonString(string json, ref int index, out string value)
        {
            value = null;

            if (index >= json.Length || json[index] != '"')
            {
                return false;
            }

            index++;

            var builder = new StringBuilder();
            while (index < json.Length)
            {
                var current = json[index++];
                if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (index >= json.Length)
                {
                    return false;
                }

                var escaped = json[index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (index + 4 > json.Length)
                        {
                            return false;
                        }

                        var hex = json.Substring(index, 4);
                        int charCode;
                        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out charCode))
                        {
                            return false;
                        }

                        builder.Append((char)charCode);
                        index += 4;
                        break;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        [Serializable]
        private sealed class JoinGamePayloadDto
        {
            public string gameTicket;
        }

        [Serializable]
        private sealed class ReconnectPayloadDto
        {
            public string gameTicket;
            public string previousSessionId;
        }

        [Serializable]
        private sealed class PlayerInputPayloadDto
        {
            public float moveX;
            public float moveY;
            public int sequence;
        }
    }
}
