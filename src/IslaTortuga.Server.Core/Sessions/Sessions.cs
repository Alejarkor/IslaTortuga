using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IslaTortuga.Server.Core.Sessions;

public enum TicketPurpose
{
    Join = 1,
    Reconnect = 2,
}

public sealed class GameTicket
{
    public GameTicket(
        string ticketId,
        string userId,
        string displayName,
        TicketPurpose purpose,
        string? previousSessionId,
        DateTimeOffset expiresAt)
    {
        TicketId = ticketId;
        UserId = userId;
        DisplayName = displayName;
        Purpose = purpose;
        PreviousSessionId = previousSessionId;
        ExpiresAt = expiresAt;
    }

    public string TicketId { get; }

    public string UserId { get; }

    public string DisplayName { get; }

    public TicketPurpose Purpose { get; }

    public string? PreviousSessionId { get; }

    public DateTimeOffset ExpiresAt { get; }
}

public sealed class PlayerSession
{
    public PlayerSession(string sessionId, string userId, string displayName)
    {
        SessionId = sessionId;
        UserId = userId;
        DisplayName = displayName;
        CreatedAt = DateTimeOffset.UtcNow;
        LastSeenAt = CreatedAt;
    }

    public string SessionId { get; }

    public string UserId { get; }

    public string DisplayName { get; }

    public string? ConnectionId { get; private set; }

    public string? RoomId { get; private set; }

    public string? PlayerEntityId { get; private set; }

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

    public void MarkDisconnected()
    {
        ConnectionId = null;
        LastSeenAt = DateTimeOffset.UtcNow;
        IsConnected = false;
    }
}

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _sessionIdsByConnection = new();

    public PlayerSession CreateSession(GameTicket ticket, string connectionId)
    {
        var session = new PlayerSession(
            Guid.NewGuid().ToString("N"),
            ticket.UserId,
            ticket.DisplayName);

        session.AttachConnection(connectionId);
        _sessions[session.SessionId] = session;
        _sessionIdsByConnection[connectionId] = session.SessionId;
        return session;
    }

    public PlayerSession ReconnectSession(GameTicket ticket, string connectionId)
    {
        if (!string.IsNullOrWhiteSpace(ticket.PreviousSessionId) &&
            _sessions.TryGetValue(ticket.PreviousSessionId, out var existingSession) &&
            existingSession.UserId == ticket.UserId)
        {
            existingSession.AttachConnection(connectionId);
            _sessionIdsByConnection[connectionId] = existingSession.SessionId;
            return existingSession;
        }

        return CreateSession(ticket, connectionId);
    }

    public bool TryGetBySessionId(string sessionId, out PlayerSession? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool TryGetByConnection(string connectionId, out PlayerSession? session)
    {
        session = null;

        if (!_sessionIdsByConnection.TryGetValue(connectionId, out var sessionId))
        {
            return false;
        }

        return _sessions.TryGetValue(sessionId, out session);
    }

    public void MarkDisconnected(string connectionId)
    {
        if (!_sessionIdsByConnection.TryRemove(connectionId, out var sessionId))
        {
            return;
        }

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.MarkDisconnected();
        }
    }
}

public sealed class GameTicketService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions TicketJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, byte> _consumedTicketIds = new();
    private readonly string _ticketSecret;

    public GameTicketService(string? ticketSecret = null)
    {
        _ticketSecret = string.IsNullOrWhiteSpace(ticketSecret)
            ? "dev_game_ticket_secret_change_me"
            : ticketSecret;
    }

    public GameTicket CreateJoinTicket(string userId, string displayName)
    {
        return CreateSignedTicket(userId, displayName, TicketPurpose.Join, previousSessionId: null);
    }

    public GameTicket CreateReconnectTicket(string userId, string displayName, string? previousSessionId)
    {
        return CreateSignedTicket(userId, displayName, TicketPurpose.Reconnect, previousSessionId);
    }

    public bool TryConsume(
        string signedTicket,
        TicketPurpose expectedPurpose,
        out GameTicket? ticket,
        out string errorCode)
    {
        ticket = null;
        errorCode = "ticket_invalid";

        var payload = ValidateSignedTicket(signedTicket);
        if (payload is null)
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
            payload.TicketId,
            payload.UserId,
            payload.DisplayName,
            expectedPurpose,
            payload.PreviousSessionId,
            DateTimeOffset.FromUnixTimeMilliseconds(payload.ExpiresAt));
        errorCode = string.Empty;
        return true;
    }

    private GameTicket CreateSignedTicket(
        string userId,
        string displayName,
        TicketPurpose purpose,
        string? previousSessionId)
    {
        var payload = new SignedGameTicketPayload(
            Guid.NewGuid().ToString("N"),
            userId,
            displayName,
            ToPurposeValue(purpose),
            previousSessionId,
            DateTimeOffset.UtcNow.Add(TicketLifetime).ToUnixTimeMilliseconds());

        var serializedPayload = JsonSerializer.Serialize(payload);
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(serializedPayload));
        var signature = ComputeSignature(encodedPayload);
        var signedTicket = $"{encodedPayload}.{signature}";

        return new GameTicket(
            signedTicket,
            userId,
            displayName,
            purpose,
            previousSessionId,
            DateTimeOffset.FromUnixTimeMilliseconds(payload.ExpiresAt));
    }

    private SignedGameTicketPayload? ValidateSignedTicket(string signedTicket)
    {
        var parts = signedTicket.Split(new[] { '.' }, 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var encodedPayload = parts[0];
        var expectedSignature = ComputeSignature(encodedPayload);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[1]),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return null;
        }

        var payloadBytes = Base64UrlDecode(encodedPayload);
        return JsonSerializer.Deserialize<SignedGameTicketPayload>(payloadBytes, TicketJsonOptions);
    }

    private string ComputeSignature(string encodedPayload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_ticketSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload));
        return Base64UrlEncode(hash);
    }

    private static string ToPurposeValue(TicketPurpose purpose)
    {
        return purpose == TicketPurpose.Reconnect ? "reconnect" : "join";
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

    private sealed class SignedGameTicketPayload
    {
        public SignedGameTicketPayload(
            string ticketId,
            string userId,
            string displayName,
            string purpose,
            string? previousSessionId,
            long expiresAt)
        {
            TicketId = ticketId;
            UserId = userId;
            DisplayName = displayName;
            Purpose = purpose;
            PreviousSessionId = previousSessionId;
            ExpiresAt = expiresAt;
        }

        public string TicketId { get; }

        public string UserId { get; }

        public string DisplayName { get; }

        public string Purpose { get; }

        public string? PreviousSessionId { get; }

        public long ExpiresAt { get; }
    }
}
