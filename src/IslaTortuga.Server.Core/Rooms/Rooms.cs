using IslaTortuga.Server.Core.Sessions;
using IslaTortuga.Server.Core.World;
using IslaTortuga.Server.Core.World.Tiled;

namespace IslaTortuga.Server.Core.Rooms;

public enum RoomState
{
    Initializing = 1,
    Running = 2,
    Closing = 3,
}

public sealed class RoomPlayer
{
    public RoomPlayer(GameRoom room, PlayerSession session, PlayerEntity playerEntity)
    {
        Room = room;
        Session = session;
        PlayerEntity = playerEntity;
    }

    public GameRoom Room { get; }

    public PlayerSession Session { get; }

    public PlayerEntity PlayerEntity { get; }
}

public sealed class GameRoom
{
    private readonly Dictionary<string, RoomPlayer> _players = new();
    private readonly object _sync = new();

    public GameRoom(string roomId, GameWorld world)
    {
        RoomId = roomId;
        World = world;
        State = RoomState.Running;
    }

    public string RoomId { get; }

    public RoomState State { get; private set; }

    public GameWorld World { get; }

    public IReadOnlyCollection<RoomPlayer> Players
    {
        get
        {
            lock (_sync)
            {
                return _players.Values.ToArray();
            }
        }
    }

    public RoomPlayer AddOrGetPlayer(PlayerSession session)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(session.SessionId, out var existingPlayer))
            {
                return existingPlayer;
            }

            var spawn = World.GetNextSpawnPoint();
            var playerEntity = new PlayerEntity(
                $"player_{session.UserId}",
                session.UserId,
                session.DisplayName,
                spawn.X,
                spawn.Y);

            World.Entities.Add(playerEntity);
            session.BindToRoom(RoomId, playerEntity.EntityId);

            var roomPlayer = new RoomPlayer(this, session, playerEntity);
            _players[session.SessionId] = roomPlayer;
            return roomPlayer;
        }
    }

    public void Tick(float deltaSeconds)
    {
        World.Tick(deltaSeconds);
    }
}

public sealed class GameRoomManagerOptions
{
    public string DefaultMapPath { get; set; } = string.Empty;

    public string DefaultRoomId { get; set; } = "room.default";

    public string DefaultWorldId { get; set; } = "world.default";
}

public sealed class GameRoomManager
{
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly object _sync = new();

    public GameRoomManager(GameRoomManagerOptions options, TiledWorldBuilder tiledWorldBuilder)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultMapPath))
        {
            throw new ArgumentException("DefaultMapPath is required to bootstrap the embedded game server.", nameof(options));
        }

        var tiledMap = tiledWorldBuilder.BuildFromFile(options.DefaultMapPath);
        var world = new GameWorld(options.DefaultWorldId, tiledMap);
        var room = new GameRoom(options.DefaultRoomId, world);

        _rooms[room.RoomId] = room;
        DefaultRoom = room;
    }

    public GameRoom DefaultRoom { get; }

    public IReadOnlyCollection<GameRoom> GetAllRooms()
    {
        lock (_sync)
        {
            return _rooms.Values.ToArray();
        }
    }

    public RoomPlayer AttachOrGetSession(PlayerSession session)
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(session.RoomId) &&
                _rooms.TryGetValue(session.RoomId, out var room))
            {
                return room.AddOrGetPlayer(session);
            }

            return DefaultRoom.AddOrGetPlayer(session);
        }
    }

    public void TickAll(float deltaSeconds)
    {
        lock (_sync)
        {
            foreach (var room in _rooms.Values)
            {
                room.Tick(deltaSeconds);
            }
        }
    }
}
