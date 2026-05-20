using IslaTortuga.Server.Sessions;
using IslaTortuga.Server.World;
using IslaTortuga.Server.World.Tiled;

namespace IslaTortuga.Server.Rooms;

public sealed class GameRoomManager
{
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly object _sync = new();

    public GameRoomManager(IHostEnvironment hostEnvironment, TiledWorldBuilder tiledWorldBuilder)
    {
        var mapPath = ResolveDefaultMapPath(hostEnvironment);
        var tiledMap = tiledWorldBuilder.BuildFromFile(mapPath);
        var world = new GameWorld("world.default", tiledMap);
        var room = new GameRoom("room.default", world);

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

    private static string ResolveDefaultMapPath(IHostEnvironment hostEnvironment)
    {
        var current = new DirectoryInfo(hostEnvironment.ContentRootPath);

        while (current is not null)
        {
            var contentPackCandidate = Path.Combine(
                current.FullName,
                "content-packs",
                "v001",
                "maps",
                "island_01.tmj");
            if (File.Exists(contentPackCandidate))
            {
                return contentPackCandidate;
            }

            var candidate = Path.Combine(current.FullName, "assets", "maps", "test_map.tmj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(hostEnvironment.ContentRootPath, "content-packs", "v001", "maps", "island_01.tmj");
    }
}
