namespace IslaTortuga.Server.World;

public sealed class ItemEntity : NetworkEntity
{
    public ItemEntity(string entityId, string itemType, float x, float y)
        : base(entityId, itemType, x, y)
    {
    }
}
