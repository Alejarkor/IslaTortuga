namespace IslaTortuga.Server.World;

public sealed class GameWorld
{
    private int _spawnCursor;

    public GameWorld(string worldId, SceneTemplateData sceneData)
    {
        WorldId = worldId;
        SceneData = sceneData;
    }

    public string WorldId { get; }

    public SceneTemplateData SceneData { get; }

    public EntityManager Entities { get; } = new();

    public long CurrentTick { get; private set; }

    public (float X, float Y) GetNextSpawnPoint()
    {
        var spawnPoints = SceneData.SpawnPoints;

        if (spawnPoints.Count == 0)
        {
            return (
                SceneData.BoundsWidth * 0.5f,
                SceneData.BoundsDepth * 0.5f);
        }

        var spawn = spawnPoints[_spawnCursor % spawnPoints.Count];
        _spawnCursor++;
        return (spawn.X, spawn.Z);
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
