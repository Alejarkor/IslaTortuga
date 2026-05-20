namespace IslaTortuga.Server.Sessions;

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
