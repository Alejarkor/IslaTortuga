using System.Collections.Generic;
using System.Linq;
using IslaTortuga.Server.Core.Protocol;
using IslaTortuga.Server.Core.Rooms;
using IslaTortuga.Server.Core.World;

namespace IslaTortuga.Server.Core.Replication
{
    public sealed class InterestManager
    {
        public IReadOnlyList<NetworkEntity> GetVisibleEntities(GameRoom room, RoomPlayer viewer)
        {
            return room.World.Entities.GetAll().ToArray();
        }
    }

    public sealed class EntityReplicator
    {
        public EntityStatePayload Replicate(NetworkEntity entity)
        {
            var player = entity as PlayerEntity;
            if (player != null)
            {
                return new EntityStatePayload(
                    player.EntityId,
                    player.EntityType,
                    player.X,
                    player.Y,
                    player.Facing,
                    player.DisplayName,
                    player.VisualId);
            }

            return new EntityStatePayload(
                entity.EntityId,
                entity.EntityType,
                entity.X,
                entity.Y,
                "down",
                null,
                null);
        }
    }

    public sealed class SnapshotBuilder
    {
        private readonly InterestManager _interestManager;
        private readonly EntityReplicator _entityReplicator;

        public SnapshotBuilder(InterestManager interestManager, EntityReplicator entityReplicator)
        {
            _interestManager = interestManager;
            _entityReplicator = entityReplicator;
        }

        public WorldSnapshotPayload Build(GameRoom room, RoomPlayer viewer)
        {
            var entities = _interestManager.GetVisibleEntities(room, viewer)
                .Select(_entityReplicator.Replicate)
                .ToArray();

            return new WorldSnapshotPayload(room.World.CurrentTick, room.RoomId, entities);
        }
    }
}
