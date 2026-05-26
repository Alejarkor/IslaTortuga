using System;
using System.Collections.Generic;
using IslaTortuga.Server.Core.Sessions;
using IslaTortuga.Server.Core.World.Tiled;
using UnityEngine;

namespace IslaTortuga.Server.Core.World
{
    [CreateAssetMenu(menuName = "Isla Tortuga/Networking/Network Entity Definition", fileName = "NetworkEntityDefinition")]
    public sealed class NetworkEntityDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string entityType = string.Empty;
        [SerializeField] private GameObject prefab;

        public string EntityType
        {
            get { return entityType; }
        }

        public GameObject Prefab
        {
            get { return prefab; }
        }
    }

    public sealed class NetworkEntityPrefabDefinition
    {
        public string EntityType { get; set; } = string.Empty;

        public GameObject Prefab { get; set; }
    }

    public sealed class ServerNetworkSpawnerOptions
    {
        public NetworkEntityPrefabDefinition PlayerDefinition { get; set; }

        public IReadOnlyList<NetworkEntityPrefabDefinition> PrefabDefinitions { get; set; } = Array.Empty<NetworkEntityPrefabDefinition>();
    }

    public sealed class ServerNetworkSpawner
    {
        private readonly Transform _entityRoot;
        private readonly EntityManager _entityManager;
        private readonly Dictionary<string, GameObject> _prefabsByEntityType;
        private readonly Dictionary<string, PlayerEntity> _playersByUserId = new Dictionary<string, PlayerEntity>();
        private readonly object _sync = new object();
        private readonly NetworkEntityPrefabDefinition _playerDefinition;

        public ServerNetworkSpawner(Transform entityRoot, EntityManager entityManager, ServerNetworkSpawnerOptions options)
        {
            _entityRoot = entityRoot;
            _entityManager = entityManager;
            _prefabsByEntityType = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            _playerDefinition = options?.PlayerDefinition;

            var definitions = options?.PrefabDefinitions ?? Array.Empty<NetworkEntityPrefabDefinition>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.EntityType) || definition.Prefab == null)
                {
                    continue;
                }

                _prefabsByEntityType[definition.EntityType] = definition.Prefab;
            }

            if (_playerDefinition != null &&
                !string.IsNullOrWhiteSpace(_playerDefinition.EntityType) &&
                _playerDefinition.Prefab != null)
            {
                _prefabsByEntityType[_playerDefinition.EntityType] = _playerDefinition.Prefab;
            }
        }

        public PlayerEntity GetOrSpawnPlayer(PlayerSession session, string roomId, Func<(float X, float Y)> spawnResolver)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (spawnResolver == null)
            {
                throw new ArgumentNullException(nameof(spawnResolver));
            }

            lock (_sync)
            {
                if (_playerDefinition == null || string.IsNullOrWhiteSpace(_playerDefinition.EntityType))
                {
                    throw new InvalidOperationException("PlayerDefinition must be configured before spawning player entities.");
                }

                if (_playersByUserId.TryGetValue(session.UserId, out var existingPlayer) && existingPlayer != null)
                {
                    existingPlayer.AttachSession(session.SessionId);
                    return existingPlayer;
                }

                var spawn = spawnResolver();
                var entityId = "player_" + session.UserId;
                var playerEntity = SpawnEntityInternal<PlayerEntity>(
                    _playerDefinition.EntityType,
                    entityId,
                    go =>
                    {
                        EnsurePlayerDefaults(go);
                    },
                    entity =>
                    {
                        entity.Initialize(entityId, _playerDefinition.EntityType, roomId, session.UserId, session.DisplayName, session.VisualId, spawn.X, spawn.Y);
                        entity.AttachSession(session.SessionId);
                    });

                _playersByUserId[session.UserId] = playerEntity;
                return playerEntity;
            }
        }

        public T SpawnEntity<T>(
            string entityType,
            string entityId,
            Action<GameObject> configureObject,
            Action<T> initializeEntity)
            where T : NetworkEntity
        {
            lock (_sync)
            {
                return SpawnEntityInternal(entityType, entityId, configureObject, initializeEntity);
            }
        }

        public void HandlePlayerDisconnected(PlayerSession session, bool despawnDisconnectedPlayers)
        {
            if (session == null)
            {
                return;
            }

            lock (_sync)
            {
                if (!_playersByUserId.TryGetValue(session.UserId, out var playerEntity) || playerEntity == null)
                {
                    return;
                }

                playerEntity.MarkDisconnected();

                if (!despawnDisconnectedPlayers)
                {
                    return;
                }

                _playersByUserId.Remove(session.UserId);
                DespawnEntityInternal(playerEntity.EntityId);
            }
        }

        public bool TryGetPlayerByUserId(string userId, out PlayerEntity playerEntity)
        {
            lock (_sync)
            {
                return _playersByUserId.TryGetValue(userId, out playerEntity);
            }
        }

        public bool DespawnEntity(string entityId)
        {
            lock (_sync)
            {
                return DespawnEntityInternal(entityId);
            }
        }

        private T SpawnEntityInternal<T>(
            string entityType,
            string entityId,
            Action<GameObject> configureObject,
            Action<T> initializeEntity)
            where T : NetworkEntity
        {
            if (_entityManager.TryGet(entityId, out var existingEntity))
            {
                var typedExistingEntity = existingEntity as T;
                if (typedExistingEntity == null)
                {
                    throw new InvalidOperationException("Entity '" + entityId + "' already exists with a different type.");
                }

                initializeEntity?.Invoke(typedExistingEntity);
                return typedExistingEntity;
            }

            var entityObject = CreateEntityObject(entityType, entityId);
            configureObject?.Invoke(entityObject);

            var entity = entityObject.GetComponent<T>();
            if (entity == null)
            {
                entity = entityObject.AddComponent<T>();
            }

            initializeEntity?.Invoke(entity);
            _entityManager.Add(entity);
            return entity;
        }

        private GameObject CreateEntityObject(string entityType, string entityId)
        {
            GameObject entityObject;
            if (_prefabsByEntityType.TryGetValue(entityType, out var prefab) && prefab != null)
            {
                entityObject = UnityEngine.Object.Instantiate(prefab, _entityRoot);
            }
            else
            {
                entityObject = new GameObject(entityType + ":" + entityId);
                entityObject.transform.SetParent(_entityRoot, false);
            }

            entityObject.name = entityType + ":" + entityId;
            return entityObject;
        }

        private bool DespawnEntityInternal(string entityId)
        {
            if (!_entityManager.Remove(entityId, out var entity) || entity == null)
            {
                return false;
            }

            var playerEntity = entity as PlayerEntity;
            if (playerEntity != null && !string.IsNullOrWhiteSpace(playerEntity.UserId))
            {
                _playersByUserId.Remove(playerEntity.UserId);
            }

            UnityEngine.Object.Destroy(entity.gameObject);
            return true;
        }

        private static void EnsurePlayerDefaults(GameObject entityObject)
        {
            var characterController = entityObject.GetComponent<CharacterController>();
            var createdNow = false;
            if (characterController == null)
            {
                characterController = entityObject.AddComponent<CharacterController>();
                createdNow = true;
            }

            if (createdNow)
            {
                characterController.height = 1.8f;
                characterController.radius = 0.35f;
                characterController.center = new Vector3(0f, 0.9f, 0f);
                characterController.minMoveDistance = 0f;
            }
        }
    }

    public sealed class GameWorld
    {
        private int _spawnCursor;
        private readonly GameObject _worldRoot;
        private readonly GameObject _entitiesRoot;

        public GameWorld(string worldId, TiledWorldMap map, ServerNetworkSpawnerOptions spawnerOptions)
        {
            WorldId = worldId;
            Map = map;
            _worldRoot = new GameObject("ServerWorld:" + worldId);
            _entitiesRoot = new GameObject("Entities");
            _entitiesRoot.transform.SetParent(_worldRoot.transform, false);
            UnityEngine.Object.DontDestroyOnLoad(_worldRoot);
            Spawner = new ServerNetworkSpawner(_entitiesRoot.transform, Entities, spawnerOptions);
        }

        public string WorldId { get; }

        public TiledWorldMap Map { get; }

        public EntityManager Entities { get; } = new EntityManager();

        public ServerNetworkSpawner Spawner { get; }

        public Transform EntityRoot
        {
            get { return _entitiesRoot.transform; }
        }

        public long CurrentTick { get; private set; }

        public (float X, float Y) GetNextSpawnPoint()
        {
            var spawnPoints = Map.GetSpawnPoints();

            if (spawnPoints.Count == 0)
            {
                return (
                    Map.Width * Map.TileWidth * 0.5f,
                    Map.Height * Map.TileHeight * 0.5f);
            }

            var spawn = spawnPoints[_spawnCursor % spawnPoints.Count];
            _spawnCursor++;
            return (spawn.X, spawn.Y);
        }

        public void Tick(float deltaSeconds)
        {
            CurrentTick++;

            foreach (var entity in Entities.GetAll())
            {
                entity.ServerTick(deltaSeconds);
            }
        }
    }
}
