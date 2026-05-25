using IslaTortuga.Server.Core.Protocol;
using IslaTortuga.Server.Core.Replication;
using IslaTortuga.Server.Core.Rooms;
using IslaTortuga.Server.Core.Sessions;
using IslaTortuga.Server.Core.World.Tiled;

namespace IslaTortuga.Server.Core.Embedded;

public sealed class EmbeddedGameServerHostOptions
{
    public string DefaultMapPath { get; set; } = string.Empty;

    public string DefaultRoomId { get; set; } = "room.default";

    public string DefaultWorldId { get; set; } = "world.default";

    public string? TicketSecret { get; set; }

    public float TickDeltaSeconds { get; set; } = 0.05f;
}

public sealed class EmbeddedGameServerJoinResult
{
    public EmbeddedGameServerJoinResult(AuthAcceptedPayload auth, WorldSnapshotPayload snapshot)
    {
        Auth = auth;
        Snapshot = snapshot;
    }

    public AuthAcceptedPayload Auth { get; }

    public WorldSnapshotPayload Snapshot { get; }
}

public sealed class EmbeddedGameServerTickResult
{
    public EmbeddedGameServerTickResult(
        string sessionId,
        string userId,
        string roomId,
        string playerEntityId,
        WorldSnapshotPayload snapshot)
    {
        SessionId = sessionId;
        UserId = userId;
        RoomId = roomId;
        PlayerEntityId = playerEntityId;
        Snapshot = snapshot;
    }

    public string SessionId { get; }

    public string UserId { get; }

    public string RoomId { get; }

    public string PlayerEntityId { get; }

    public WorldSnapshotPayload Snapshot { get; }
}

public sealed class EmbeddedGameServerHost
{
    private readonly GameTicketService _gameTicketService;
    private readonly SessionManager _sessionManager;
    private readonly GameRoomManager _gameRoomManager;
    private readonly SnapshotBuilder _snapshotBuilder;

    public EmbeddedGameServerHost(EmbeddedGameServerHostOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultMapPath))
        {
            throw new ArgumentException("DefaultMapPath is required to bootstrap the embedded game server.", nameof(options));
        }

        TickDeltaSeconds = options.TickDeltaSeconds <= 0 ? 0.05f : options.TickDeltaSeconds;

        _gameTicketService = new GameTicketService(options.TicketSecret);
        _sessionManager = new SessionManager();

        var tiledWorldBuilder = new TiledWorldBuilder();
        _gameRoomManager = new GameRoomManager(
            new GameRoomManagerOptions
            {
                DefaultMapPath = options.DefaultMapPath,
                DefaultRoomId = options.DefaultRoomId,
                DefaultWorldId = options.DefaultWorldId,
            },
            tiledWorldBuilder);

        _snapshotBuilder = new SnapshotBuilder(new InterestManager(), new EntityReplicator());
    }

    public float TickDeltaSeconds { get; }

    public GameTicket CreateJoinTicket(string userId, string displayName)
    {
        return _gameTicketService.CreateJoinTicket(userId, displayName);
    }

    public GameTicket CreateReconnectTicket(string userId, string displayName, string? previousSessionId)
    {
        return _gameTicketService.CreateReconnectTicket(userId, displayName, previousSessionId);
    }

    public bool TryJoin(
        string signedTicket,
        string connectionId,
        out EmbeddedGameServerJoinResult? result,
        out string errorCode)
    {
        result = null;

        if (!_gameTicketService.TryConsume(signedTicket, TicketPurpose.Join, out var ticket, out errorCode))
        {
            return false;
        }

        var session = _sessionManager.CreateSession(ticket!, connectionId);
        var roomPlayer = _gameRoomManager.AttachOrGetSession(session);
        result = BuildJoinResult(roomPlayer);
        return true;
    }

    public bool TryReconnect(
        string signedTicket,
        string connectionId,
        out EmbeddedGameServerJoinResult? result,
        out string errorCode)
    {
        result = null;

        if (!_gameTicketService.TryConsume(signedTicket, TicketPurpose.Reconnect, out var ticket, out errorCode))
        {
            return false;
        }

        var session = _sessionManager.ReconnectSession(ticket!, connectionId);
        var roomPlayer = _gameRoomManager.AttachOrGetSession(session);
        result = BuildJoinResult(roomPlayer);
        return true;
    }

    public bool ApplyPlayerInput(string sessionId, float moveX, float moveY)
    {
        if (!_sessionManager.TryGetBySessionId(sessionId, out var session) || session is null)
        {
            return false;
        }

        var roomPlayer = _gameRoomManager.AttachOrGetSession(session);
        roomPlayer.PlayerEntity.ApplyInput(moveX, moveY);
        return true;
    }

    public IReadOnlyList<EmbeddedGameServerTickResult> Tick()
    {
        _gameRoomManager.TickAll(TickDeltaSeconds);
        return BuildSnapshots();
    }

    public IReadOnlyList<EmbeddedGameServerTickResult> BuildSnapshots()
    {
        var snapshots = new List<EmbeddedGameServerTickResult>();

        foreach (var room in _gameRoomManager.GetAllRooms())
        {
            foreach (var roomPlayer in room.Players)
            {
                var snapshot = _snapshotBuilder.Build(room, roomPlayer);
                snapshots.Add(new EmbeddedGameServerTickResult(
                    roomPlayer.Session.SessionId,
                    roomPlayer.Session.UserId,
                    room.RoomId,
                    roomPlayer.PlayerEntity.EntityId,
                    snapshot));
            }
        }

        return snapshots;
    }

    public void MarkDisconnected(string connectionId)
    {
        _sessionManager.MarkDisconnected(connectionId);
    }

    private EmbeddedGameServerJoinResult BuildJoinResult(RoomPlayer roomPlayer)
    {
        return new EmbeddedGameServerJoinResult(
            new AuthAcceptedPayload(
                roomPlayer.Session.SessionId,
                roomPlayer.Session.UserId,
                roomPlayer.Session.DisplayName,
                roomPlayer.Room.RoomId,
                roomPlayer.PlayerEntity.EntityId),
            _snapshotBuilder.Build(roomPlayer.Room, roomPlayer));
    }
}
