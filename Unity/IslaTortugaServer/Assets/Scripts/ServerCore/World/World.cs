using System;
using System.Collections.Generic;
using System.Linq;

namespace IslaTortuga.Server.Core.World
{
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

    public sealed class PlayerEntity : NetworkEntity
    {
        private const float Speed = 140f;
        private float _moveX;
        private float _moveY;

        public PlayerEntity(string entityId, string userId, string displayName, float x, float y)
            : base(entityId, "player", x, y)
        {
            UserId = userId;
            DisplayName = displayName;
        }

        public string UserId { get; }

        public string DisplayName { get; }

        public string Facing { get; private set; } = "down";

        public void ApplyInput(float moveX, float moveY)
        {
            _moveX = Clamp(moveX, -1f, 1f);
            _moveY = Clamp(moveY, -1f, 1f);

            if (Math.Abs(_moveX) > Math.Abs(_moveY))
            {
                Facing = _moveX >= 0 ? "right" : "left";
            }
            else if (Math.Abs(_moveY) > 0)
            {
                Facing = _moveY >= 0 ? "down" : "up";
            }
        }

        public void Tick(float deltaSeconds)
        {
            X += _moveX * Speed * deltaSeconds;
            Y += _moveY * Speed * deltaSeconds;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    public sealed class EntityManager
    {
        private readonly Dictionary<string, NetworkEntity> _entities = new Dictionary<string, NetworkEntity>();
        private readonly object _sync = new object();

        public void Add(NetworkEntity entity)
        {
            lock (_sync)
            {
                _entities[entity.EntityId] = entity;
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
}
