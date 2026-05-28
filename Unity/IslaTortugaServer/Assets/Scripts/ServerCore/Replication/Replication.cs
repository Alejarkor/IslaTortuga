using System.Collections.Concurrent;
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
            return room.World.Entities.GetAll()
                .Where(entity =>
                    string.Equals(entity.SceneId, viewer.PlayerEntity.SceneId, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entity.SceneInstanceId, viewer.PlayerEntity.SceneInstanceId, System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public sealed class EntityReplicator
    {
        public EntitySpawnPayload ReplicateSpawn(NetworkEntity entity)
        {
            var displayName = entity is PlayerEntity player ? player.DisplayName : null;
            return new EntitySpawnPayload(
                entity.EntityId,
                entity.EntityType,
                EmptyToNull(entity.ArchetypeId),
                EmptyToNull(entity.VisualId),
                entity.X,
                entity.Y,
                ResolveFacing(entity),
                displayName);
        }

        public EntityUpdatePayload ReplicateUpdate(NetworkEntity entity)
        {
            return new EntityUpdatePayload(
                entity.EntityId,
                entity.X,
                entity.Y,
                ResolveFacing(entity));
        }

        private static string ResolveFacing(NetworkEntity entity)
        {
            var player = entity as PlayerEntity;
            return player != null ? player.Facing : "down";
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public sealed class ReplicationStateStore
    {
        private readonly ConcurrentDictionary<string, DeltaBuilder.ClientReplicationState> _statesBySessionId =
            new ConcurrentDictionary<string, DeltaBuilder.ClientReplicationState>();

        public DeltaBuilder.ClientReplicationState GetOrCreate(string sessionId)
        {
            return _statesBySessionId.GetOrAdd(sessionId, _ => new DeltaBuilder.ClientReplicationState());
        }

        public void Reset(string sessionId)
        {
            _statesBySessionId[sessionId] = new DeltaBuilder.ClientReplicationState();
        }
    }

    public sealed class DeltaBuilder
    {
        private readonly InterestManager _interestManager;
        private readonly EntityReplicator _entityReplicator;
        private readonly ReplicationStateStore _stateStore;

        public DeltaBuilder(
            InterestManager interestManager,
            EntityReplicator entityReplicator,
            ReplicationStateStore stateStore)
        {
            _interestManager = interestManager;
            _entityReplicator = entityReplicator;
            _stateStore = stateStore;
        }

        public void ResetSession(string sessionId)
        {
            _stateStore.Reset(sessionId);
        }

        public WorldDeltaPayload Build(GameRoom room, RoomPlayer viewer)
        {
            var spawns = new List<EntitySpawnPayload>();
            var updates = new List<EntityUpdatePayload>();
            var despawns = new List<EntityDespawnPayload>();

            var visibleEntities = _interestManager.GetVisibleEntities(room, viewer);
            var visibleIds = new HashSet<string>(visibleEntities.Select(entity => entity.EntityId));
            var state = _stateStore.GetOrCreate(viewer.Session.SessionId);

            foreach (var entity in visibleEntities)
            {
                var spawnPayload = _entityReplicator.ReplicateSpawn(entity);
                var updatePayload = _entityReplicator.ReplicateUpdate(entity);

                if (!state.TryGet(entity.EntityId, out var knownEntity))
                {
                    spawns.Add(spawnPayload);
                    state.Remember(spawnPayload, updatePayload);
                    continue;
                }

                if (knownEntity.RequiresRespawn(spawnPayload))
                {
                    despawns.Add(new EntityDespawnPayload(entity.EntityId));
                    spawns.Add(spawnPayload);
                    state.Remember(spawnPayload, updatePayload);
                    continue;
                }

                if (knownEntity.HasStateChanged(updatePayload))
                {
                    updates.Add(updatePayload);
                    knownEntity.Apply(updatePayload);
                }
            }

            foreach (var missingEntityId in state.GetKnownEntityIds().Where(entityId => !visibleIds.Contains(entityId)).ToArray())
            {
                despawns.Add(new EntityDespawnPayload(missingEntityId));
                state.Forget(missingEntityId);
            }

            return new WorldDeltaPayload(
                room.World.CurrentTick,
                room.RoomId,
                spawns,
                updates,
                despawns);
        }

        public sealed class ClientReplicationState
        {
            private readonly Dictionary<string, KnownEntityState> _knownEntities =
                new Dictionary<string, KnownEntityState>();

            public bool TryGet(string entityId, out KnownEntityState knownEntity)
            {
                return _knownEntities.TryGetValue(entityId, out knownEntity);
            }

            public void Remember(EntitySpawnPayload spawn, EntityUpdatePayload update)
            {
                _knownEntities[spawn.EntityId] = new KnownEntityState(
                    spawn.EntityType,
                    spawn.ArchetypeId,
                    spawn.VisualId,
                    update.X,
                    update.Y,
                    update.Facing);
            }

            public void Forget(string entityId)
            {
                _knownEntities.Remove(entityId);
            }

            public IEnumerable<string> GetKnownEntityIds()
            {
                return _knownEntities.Keys;
            }
        }

        public sealed class KnownEntityState
        {
            private string _entityType;
            private string _archetypeId;
            private string _visualId;
            private float _x;
            private float _y;
            private string _facing;

            public KnownEntityState(
                string entityType,
                string archetypeId,
                string visualId,
                float x,
                float y,
                string facing)
            {
                _entityType = entityType ?? string.Empty;
                _archetypeId = archetypeId ?? string.Empty;
                _visualId = visualId ?? string.Empty;
                _x = x;
                _y = y;
                _facing = facing ?? "down";
            }

            public bool RequiresRespawn(EntitySpawnPayload spawn)
            {
                return _entityType != (spawn.EntityType ?? string.Empty) ||
                       _archetypeId != (spawn.ArchetypeId ?? string.Empty) ||
                       _visualId != (spawn.VisualId ?? string.Empty);
            }

            public bool HasStateChanged(EntityUpdatePayload update)
            {
                return _x != update.X ||
                       _y != update.Y ||
                       _facing != (update.Facing ?? "down");
            }

            public void Apply(EntityUpdatePayload update)
            {
                _x = update.X;
                _y = update.Y;
                _facing = update.Facing ?? "down";
            }
        }
    }
}
