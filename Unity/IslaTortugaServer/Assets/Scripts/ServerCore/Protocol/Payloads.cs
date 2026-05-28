using System.Collections.Generic;

namespace IslaTortuga.Server.Core.Protocol
{
    public sealed class AuthAcceptedPayload
    {
        public AuthAcceptedPayload(
            string sessionId,
            string userId,
            string displayName,
            string roomId,
            string playerEntityId)
        {
            SessionId = sessionId;
            UserId = userId;
            DisplayName = displayName;
            RoomId = roomId;
            PlayerEntityId = playerEntityId;
        }

        public string SessionId { get; }

        public string UserId { get; }

        public string DisplayName { get; }

        public string RoomId { get; }

        public string PlayerEntityId { get; }
    }

    public sealed class ErrorPayload
    {
        public ErrorPayload(string code, string message, bool retryable)
        {
            Code = code;
            Message = message;
            Retryable = retryable;
        }

        public string Code { get; }

        public string Message { get; }

        public bool Retryable { get; }
    }

    public sealed class SceneContextPayload
    {
        public SceneContextPayload(string sceneId, string sceneInstanceId)
        {
            SceneId = sceneId;
            SceneInstanceId = sceneInstanceId;
        }

        public string SceneId { get; }

        public string SceneInstanceId { get; }
    }

    public sealed class EntitySpawnPayload
    {
        public EntitySpawnPayload(
            string entityId,
            string entityType,
            string archetypeId,
            string visualId,
            float x,
            float y,
            string facing,
            string displayName)
        {
            EntityId = entityId;
            EntityType = entityType;
            ArchetypeId = archetypeId;
            VisualId = visualId;
            X = x;
            Y = y;
            Facing = facing;
            DisplayName = displayName;
        }

        public string EntityId { get; }

        public string EntityType { get; }

        public string ArchetypeId { get; }

        public string VisualId { get; }

        public float X { get; }

        public float Y { get; }

        public string Facing { get; }

        public string DisplayName { get; }
    }

    public sealed class EntityUpdatePayload
    {
        public EntityUpdatePayload(
            string entityId,
            float x,
            float y,
            string facing)
        {
            EntityId = entityId;
            X = x;
            Y = y;
            Facing = facing;
        }

        public string EntityId { get; }

        public float X { get; }

        public float Y { get; }

        public string Facing { get; }
    }

    public sealed class EntityDespawnPayload
    {
        public EntityDespawnPayload(string entityId)
        {
            EntityId = entityId;
        }

        public string EntityId { get; }
    }

    public sealed class WorldDeltaPayload
    {
        public WorldDeltaPayload(
            long serverTick,
            string roomId,
            IReadOnlyList<EntitySpawnPayload> spawns,
            IReadOnlyList<EntityUpdatePayload> updates,
            IReadOnlyList<EntityDespawnPayload> despawns)
        {
            ServerTick = serverTick;
            RoomId = roomId;
            Spawns = spawns;
            Updates = updates;
            Despawns = despawns;
        }

        public long ServerTick { get; }

        public string RoomId { get; }

        public IReadOnlyList<EntitySpawnPayload> Spawns { get; }

        public IReadOnlyList<EntityUpdatePayload> Updates { get; }

        public IReadOnlyList<EntityDespawnPayload> Despawns { get; }
    }
}
