namespace IslaTortuga.Server.World;

public abstract class NetworkEntity
{
    protected NetworkEntity(string entityId, string entityType, float x, float y)
    {
        EntityId = entityId;
        EntityType = entityType;
        X = x;
        Y = y;
    }

    public string EntityId { get; }

    public string EntityType { get; }

    public float X { get; protected set; }

    public float Y { get; protected set; }
}
