using IslaTortuga.Server.Sessions;
using IslaTortuga.Server.World;
using IslaTortuga.Server.Content;

namespace IslaTortuga.Server.Rooms;

public sealed class GameRoomManager
{
    private readonly Dictionary<string, GameRoom> _rooms = new();
    private readonly object _sync = new();

    public GameRoomManager(IHostEnvironment hostEnvironment, SceneTemplateBuilder sceneTemplateBuilder)
    {
        var scenePath = ResolveDefaultScenePath(hostEnvironment);
        var sceneData = sceneTemplateBuilder.BuildFromFile(scenePath);
        var world = new GameWorld("world.default", sceneData);
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

    private static string ResolveDefaultScenePath(IHostEnvironment hostEnvironment)
    {
        var contentRoot = ContentPathResolver.ResolveContentRoot(hostEnvironment.ContentRootPath);
        var contentPackScene = Path.Combine(contentRoot, "v001", "scenes", "scene.test.plain.json");
        if (File.Exists(contentPackScene))
        {
            return contentPackScene;
        }

        var current = new DirectoryInfo(hostEnvironment.ContentRootPath);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "content-packs", "v001", "scenes", "scene.test.plain.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return contentPackScene;
    }
}
