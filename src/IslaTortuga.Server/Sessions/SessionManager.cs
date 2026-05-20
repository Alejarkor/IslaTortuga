using System.Collections.Concurrent;

namespace IslaTortuga.Server.Sessions;

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
