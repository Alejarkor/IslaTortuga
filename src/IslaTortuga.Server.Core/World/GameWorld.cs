using IslaTortuga.Server.Core.World.Tiled;

namespace IslaTortuga.Server.Core.World;

public sealed class GameWorld
{
    private int _spawnCursor;

    public GameWorld(string worldId, TiledWorldMap map)
    {
        WorldId = worldId;
        Map = map;
    }

    public string WorldId { get; }

    public TiledWorldMap Map { get; }

    public EntityManager Entities { get; } = new();

    public long CurrentTick { get; private set; }

    public (float X, float Y) GetNextSpawnPoint()
    {
        var spawnPoints = Map.GetSpawnPoints();

        if (spawnPoints.Count == 0)
        {
            return (
                Map.Width * Map.TileWidth * 0.5f,
                Map.Height * Map.TileHeight * 0.5f);
        }

        var spawn = spawnPoints[_spawnCursor % spawnPoints.Count];
        _spawnCursor++;
        return ((float)spawn.X, (float)spawn.Y);
    }

    public void Tick(float deltaSeconds)
    {
        CurrentTick++;

        foreach (var player in Entities.GetByType<PlayerEntity>())
        {
            player.Tick(deltaSeconds);
        }
    }
}
