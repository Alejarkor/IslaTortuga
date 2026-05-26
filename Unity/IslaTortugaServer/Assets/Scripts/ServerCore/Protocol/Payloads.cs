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

    public sealed class EntityStatePayload
    {
        public EntityStatePayload(
            string entityId,
            string entityType,
            float x,
            float y,
            string facing,
            string displayName,
            string visualId)
        {
            EntityId = entityId;
            EntityType = entityType;
            X = x;
            Y = y;
            Facing = facing;
            DisplayName = displayName;
            VisualId = visualId;
        }

        public string EntityId { get; }

        public string EntityType { get; }

        public float X { get; }

        public float Y { get; }

        public string Facing { get; }

        public string DisplayName { get; }

        public string VisualId { get; }
    }

    public sealed class WorldSnapshotPayload
    {
        public WorldSnapshotPayload(
            long serverTick,
            string roomId,
            IReadOnlyList<EntityStatePayload> entities)
        {
            ServerTick = serverTick;
            RoomId = roomId;
            Entities = entities;
        }

        public long ServerTick { get; }

        public string RoomId { get; }

        public IReadOnlyList<EntityStatePayload> Entities { get; }
    }
}
