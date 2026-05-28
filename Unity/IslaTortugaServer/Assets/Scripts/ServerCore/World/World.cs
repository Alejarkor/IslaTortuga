using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IslaTortuga.Server.Core.World
{
    public abstract class NetworkEntity : MonoBehaviour
    {
        private string _entityId = string.Empty;
        private string _entityType = string.Empty;
        private string _archetypeId = string.Empty;
        private string _visualId = string.Empty;
        private string _roomId = string.Empty;
        private string _sceneId = string.Empty;
        private string _sceneInstanceId = string.Empty;

        public string EntityId
        {
            get { return _entityId; }
        }

        public string EntityType
        {
            get { return _entityType; }
        }

        public string RoomId
        {
            get { return _roomId; }
        }

        public string ArchetypeId
        {
            get { return _archetypeId; }
        }

        public string SceneId
        {
            get { return _sceneId; }
        }

        public string SceneInstanceId
        {
            get { return _sceneInstanceId; }
        }

        public string VisualId
        {
            get { return _visualId; }
        }

        public float X
        {
            get { return transform.localPosition.x; }
        }

        public float Y
        {
            get { return transform.localPosition.z; }
        }

        protected void InitializeEntity(
            string entityId,
            string entityType,
            string archetypeId,
            string visualId,
            string roomId,
            string sceneId,
            string sceneInstanceId,
            Transform sceneEntityRoot,
            float x,
            float y)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException("EntityId is required.", nameof(entityId));
            }

            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("EntityType is required.", nameof(entityType));
            }

            _entityId = entityId;
            _entityType = entityType;
            _archetypeId = archetypeId ?? string.Empty;
            _visualId = visualId ?? string.Empty;
            _roomId = roomId ?? string.Empty;
            SetSceneContext(sceneId, sceneInstanceId, sceneEntityRoot, x, y);
            gameObject.name = entityType + ":" + entityId;
        }

        protected void SetSceneContext(
            string sceneId,
            string sceneInstanceId,
            Transform sceneEntityRoot,
            float x,
            float y)
        {
            _sceneId = sceneId ?? string.Empty;
            _sceneInstanceId = sceneInstanceId ?? string.Empty;

            if (sceneEntityRoot != null && transform.parent != sceneEntityRoot)
            {
                transform.SetParent(sceneEntityRoot, false);
            }

            transform.localPosition = new Vector3(x, 0f, y);
        }

        public abstract void ServerTick(float deltaSeconds);
    }

    public sealed class PlayerEntity : NetworkEntity
    {
        private const float DefaultSpeed = 7f;
        private float _moveX;
        private float _moveY;
        private CharacterController _characterController;

        public string UserId { get; private set; } = string.Empty;

        public string DisplayName { get; private set; } = string.Empty;

        public string Facing { get; private set; } = "down";

        public string SessionId { get; private set; } = string.Empty;

        public bool IsConnected { get; private set; }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        public void Initialize(
            string entityId,
            string entityType,
            string archetypeId,
            string roomId,
            string sceneId,
            string sceneInstanceId,
            Transform sceneEntityRoot,
            string userId,
            string displayName,
            string visualId,
            float x,
            float y)
        {
            InitializeEntity(
                entityId,
                entityType,
                archetypeId,
                visualId,
                roomId,
                sceneId,
                sceneInstanceId,
                sceneEntityRoot,
                x,
                y);
            UserId = userId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public void RelocateToScene(
            string sceneId,
            string sceneInstanceId,
            Transform sceneEntityRoot,
            float x,
            float y)
        {
            var wasControllerEnabled = _characterController != null && _characterController.enabled;
            if (wasControllerEnabled)
            {
                _characterController.enabled = false;
            }

            SetSceneContext(sceneId, sceneInstanceId, sceneEntityRoot, x, y);

            if (wasControllerEnabled)
            {
                _characterController.enabled = true;
            }

            _moveX = 0f;
            _moveY = 0f;
        }

        public void AttachSession(string sessionId)
        {
            SessionId = sessionId ?? string.Empty;
            IsConnected = true;
        }

        public void MarkDisconnected()
        {
            SessionId = string.Empty;
            IsConnected = false;
            _moveX = 0f;
            _moveY = 0f;
        }

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

        public override void ServerTick(float deltaSeconds)
        {
            var motion = new Vector3(_moveX, 0f, _moveY) * DefaultSpeed * deltaSeconds;

            if (_characterController != null)
            {
                _characterController.Move(motion);
                return;
            }

            transform.position += motion;
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

        public bool TryGet(string entityId, out NetworkEntity entity)
        {
            lock (_sync)
            {
                return _entities.TryGetValue(entityId, out entity);
            }
        }

        public bool Remove(string entityId, out NetworkEntity entity)
        {
            lock (_sync)
            {
                return _entities.TryGetValue(entityId, out entity) && _entities.Remove(entityId);
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
