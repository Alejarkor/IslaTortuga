using IslaTortuga.Server.Networking.Protocol.Payloads;
using IslaTortuga.Server.World;

namespace IslaTortuga.Server.Replication;

public sealed class EntityReplicator
{
    public EntityStatePayload Replicate(NetworkEntity entity)
    {
        return entity switch
        {
            PlayerEntity player => new EntityStatePayload(
                player.EntityId,
                player.EntityType,
                player.X,
                player.Y,
                player.Facing,
                player.DisplayName),

            _ => new EntityStatePayload(
                entity.EntityId,
                entity.EntityType,
                entity.X,
                entity.Y,
                "down"),
        };
    }
}
