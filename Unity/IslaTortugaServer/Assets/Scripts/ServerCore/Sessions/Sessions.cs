using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace IslaTortuga.Server.Core.Sessions
{
    public enum TicketPurpose
    {
        Join = 1,
        Reconnect = 2,
    }

    public sealed class GameTicket
    {
        public GameTicket(
            string signedTicket,
            string userId,
            string displayName,
            string visualId,
            TicketPurpose purpose,
            string previousSessionId,
            DateTimeOffset expiresAt)
        {
            SignedTicket = signedTicket;
            UserId = userId;
            DisplayName = displayName;
            VisualId = visualId ?? string.Empty;
            Purpose = purpose;
            PreviousSessionId = previousSessionId;
            ExpiresAt = expiresAt;
        }

        public string SignedTicket { get; }

        public string UserId { get; }

        public string DisplayName { get; }

        public string VisualId { get; }

        public TicketPurpose Purpose { get; }

        public string PreviousSessionId { get; }

        public DateTimeOffset ExpiresAt { get; }
    }

    public sealed class PlayerSession
    {
        public PlayerSession(string sessionId, string userId, string displayName, string visualId)
        {
            SessionId = sessionId;
            UserId = userId;
            DisplayName = displayName;
            VisualId = visualId ?? string.Empty;
            CreatedAt = DateTimeOffset.UtcNow;
            LastSeenAt = CreatedAt;
        }

        public string SessionId { get; }

        public string UserId { get; }

        public string DisplayName { get; }

        public string VisualId { get; }

        public string ConnectionId { get; private set; }

        public string RoomId { get; private set; }

        public string PlayerEntityId { get; private set; }

        public string SceneId { get; private set; } = string.Empty;

        public string SceneInstanceId { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset LastSeenAt { get; private set; }

        public bool IsConnected { get; private set; }

        public void AttachConnection(string connectionId)
        {
            ConnectionId = connectionId;
            LastSeenAt = DateTimeOffset.UtcNow;
            IsConnected = true;
        }

        public void BindToRoom(string roomId, string playerEntityId)
        {
            RoomId = roomId;
            PlayerEntityId = playerEntityId;
            LastSeenAt = DateTimeOffset.UtcNow;
        }

        public void BindSceneContext(string sceneId, string sceneInstanceId)
        {
            SceneId = sceneId ?? string.Empty;
            SceneInstanceId = sceneInstanceId ?? string.Empty;
            LastSeenAt = DateTimeOffset.UtcNow;
        }

        public void MarkDisconnected()
        {
            ConnectionId = null;
            LastSeenAt = DateTimeOffset.UtcNow;
            IsConnected = false;
        }
    }

    public sealed class SessionManager
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new ConcurrentDictionary<string, PlayerSession>();
        private readonly ConcurrentDictionary<string, string> _sessionIdsByConnection = new ConcurrentDictionary<string, string>();

        public PlayerSession CreateSession(GameTicket ticket, string connectionId)
        {
            var session = new PlayerSession(
                Guid.NewGuid().ToString("N"),
                ticket.UserId,
                ticket.DisplayName,
                ticket.VisualId);

            session.AttachConnection(connectionId);
            _sessions[session.SessionId] = session;
            _sessionIdsByConnection[connectionId] = session.SessionId;
            return session;
        }

        public PlayerSession ReconnectSession(GameTicket ticket, string connectionId)
        {
            PlayerSession existingSession;
            if (!string.IsNullOrWhiteSpace(ticket.PreviousSessionId) &&
                _sessions.TryGetValue(ticket.PreviousSessionId, out existingSession) &&
                existingSession.UserId == ticket.UserId)
            {
                existingSession.AttachConnection(connectionId);
                _sessionIdsByConnection[connectionId] = existingSession.SessionId;
                return existingSession;
            }

            return CreateSession(ticket, connectionId);
        }

        public bool TryGetBySessionId(string sessionId, out PlayerSession session)
        {
            return _sessions.TryGetValue(sessionId, out session);
        }

        public int Count
        {
            get { return _sessions.Count; }
        }

        public PlayerSession MarkDisconnected(string connectionId)
        {
            string sessionId;
            if (!_sessionIdsByConnection.TryRemove(connectionId, out sessionId))
            {
                return null;
            }

            PlayerSession session;
            if (_sessions.TryGetValue(sessionId, out session))
            {
                session.MarkDisconnected();
                return session;
            }

            return null;
        }
    }

    public sealed class GameTicketService
    {
        private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(30);
        private readonly ConcurrentDictionary<string, byte> _consumedTicketIds = new ConcurrentDictionary<string, byte>();
        private readonly string _ticketSecret;

        public GameTicketService(string ticketSecret)
        {
            _ticketSecret = string.IsNullOrWhiteSpace(ticketSecret)
                ? "dev_game_ticket_secret_change_me"
                : ticketSecret;
        }

        public GameTicket CreateJoinTicket(string userId, string displayName, string visualId)
        {
            return CreateSignedTicket(userId, displayName, visualId, TicketPurpose.Join, null);
        }

        public GameTicket CreateReconnectTicket(string userId, string displayName, string visualId, string previousSessionId)
        {
            return CreateSignedTicket(userId, displayName, visualId, TicketPurpose.Reconnect, previousSessionId);
        }

        public bool TryConsume(
            string signedTicket,
            TicketPurpose expectedPurpose,
            out GameTicket ticket,
            out string errorCode)
        {
            ticket = null;
            errorCode = "ticket_invalid";

            SignedGameTicketPayload payload;
            if (!TryValidateSignedTicket(signedTicket, out payload))
            {
                return false;
            }

            if (payload.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                errorCode = "ticket_expired";
                return false;
            }

            if (!string.Equals(payload.Purpose, ToPurposeValue(expectedPurpose), StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "ticket_purpose_mismatch";
                return false;
            }

            if (!_consumedTicketIds.TryAdd(payload.TicketId, 0))
            {
                errorCode = "ticket_already_used";
                return false;
            }

            ticket = new GameTicket(
                signedTicket,
                payload.UserId,
                payload.DisplayName,
                payload.VisualId,
                expectedPurpose,
                payload.PreviousSessionId,
                DateTimeOffset.FromUnixTimeMilliseconds(payload.ExpiresAt));
            errorCode = string.Empty;
            return true;
        }

        private GameTicket CreateSignedTicket(
            string userId,
            string displayName,
            string visualId,
            TicketPurpose purpose,
            string previousSessionId)
        {
            var payload = new SignedGameTicketPayload(
                Guid.NewGuid().ToString("N"),
                userId,
                displayName,
                visualId,
                ToPurposeValue(purpose),
                previousSessionId,
                DateTimeOffset.UtcNow.Add(TicketLifetime).ToUnixTimeMilliseconds());

            var serializedPayload = SerializePayload(payload);
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(serializedPayload));
            var signature = ComputeSignature(encodedPayload);
            var signedTicket = encodedPayload + "." + signature;

            return new GameTicket(
                signedTicket,
                userId,
                displayName,
                visualId,
                purpose,
                previousSessionId,
                DateTimeOffset.FromUnixTimeMilliseconds(payload.ExpiresAt));
        }

        private bool TryValidateSignedTicket(string signedTicket, out SignedGameTicketPayload payload)
        {
            payload = null;

            var parts = signedTicket.Split(new[] { '.' }, 2);
            if (parts.Length != 2)
            {
                return false;
            }

            var encodedPayload = parts[0];
            var expectedSignature = ComputeSignature(encodedPayload);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(parts[1]),
                    Encoding.UTF8.GetBytes(expectedSignature)))
            {
                return false;
            }

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(encodedPayload));
            payload = DeserializePayload(payloadJson);
            return payload != null;
        }

        private string ComputeSignature(string encodedPayload)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_ticketSecret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload));
                return Base64UrlEncode(hash);
            }
        }

        private static string ToPurposeValue(TicketPurpose purpose)
        {
            return purpose == TicketPurpose.Reconnect ? "reconnect" : "join";
        }

        private static string SerializePayload(SignedGameTicketPayload payload)
        {
            return "{"
                + "\"ticketId\":\"" + EscapeJson(payload.TicketId) + "\","
                + "\"userId\":\"" + EscapeJson(payload.UserId) + "\","
                + "\"displayName\":\"" + EscapeJson(payload.DisplayName) + "\","
                + "\"visualId\":\"" + EscapeJson(payload.VisualId) + "\","
                + "\"purpose\":\"" + EscapeJson(payload.Purpose) + "\","
                + "\"previousSessionId\":" + ToJsonStringOrNull(payload.PreviousSessionId) + ","
                + "\"expiresAt\":" + payload.ExpiresAt
                + "}";
        }

        private static SignedGameTicketPayload DeserializePayload(string payloadJson)
        {
            return UnityEngine.JsonUtility.FromJson<SignedGameTicketPayload>(payloadJson);
        }

        private static string ToJsonStringOrNull(string value)
        {
            return string.IsNullOrEmpty(value) ? "null" : "\"" + EscapeJson(value) + "\"";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var padded = value
                .Replace('-', '+')
                .Replace('_', '/');

            padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
            return Convert.FromBase64String(padded);
        }

        [Serializable]
        private sealed class SignedGameTicketPayload
        {
            public SignedGameTicketPayload(
                string ticketId,
                string userId,
                string displayName,
                string visualId,
                string purpose,
                string previousSessionId,
                long expiresAt)
            {
                this.ticketId = ticketId;
                this.userId = userId;
                this.displayName = displayName;
                this.visualId = visualId;
                this.purpose = purpose;
                this.previousSessionId = previousSessionId;
                this.expiresAt = expiresAt;
            }

            public string ticketId;
            public string userId;
            public string displayName;
            public string visualId;
            public string purpose;
            public string previousSessionId;
            public long expiresAt;

            public string TicketId { get { return ticketId; } }
            public string UserId { get { return userId; } }
            public string DisplayName { get { return displayName; } }
            public string VisualId { get { return visualId; } }
            public string Purpose { get { return purpose; } }
            public string PreviousSessionId { get { return previousSessionId; } }
            public long ExpiresAt { get { return expiresAt; } }
        }
    }
}
