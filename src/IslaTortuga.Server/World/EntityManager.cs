namespace IslaTortuga.Server.World;

public sealed class EntityManager
{
    private readonly Dictionary<string, NetworkEntity> _entities = new();
    private readonly object _sync = new();

    public void Add(NetworkEntity entity)
    {
        lock (_sync)
        {
            _entities[entity.EntityId] = entity;
        }
    }

    public bool Remove(string entityId)
    {
        lock (_sync)
        {
            return _entities.Remove(entityId);
        }
    }

    public IReadOnlyCollection<NetworkEntity> GetAll()
    {
        lock (_sync)
        {
            return _entities.Values.ToArray();
        }
    }

    public IEnumerable<T> GetByType<T>() where T : NetworkEntity
    {
        lock (_sync)
        {
            return _entities.Values.OfType<T>().ToArray();
        }
    }
}
