using IslaTortuga.Server.Sessions;
using IslaTortuga.Server.World;

namespace IslaTortuga.Server.Rooms;

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
