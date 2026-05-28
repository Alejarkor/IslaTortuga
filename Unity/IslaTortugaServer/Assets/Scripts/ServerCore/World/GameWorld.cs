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
        [SerializeField] private string defaultArchetypeId = string.Empty;
        [SerializeField] private string defaultVisualId = string.Empty;
        [SerializeField] private GameObject prefab;

        public string EntityType
        {
            get { return entityType; }
        }

        public GameObject Prefab
        {
            get { return prefab; }
        }

        public string DefaultArchetypeId
        {
            get { return defaultArchetypeId; }
        }

        public string DefaultVisualId
        {
            get { return defaultVisualId; }
        }
    }

    public sealed class NetworkEntityPrefabDefinition
    {
        public string EntityType { get; set; } = string.Empty;

        public string DefaultArchetypeId { get; set; } = string.Empty;

        public string DefaultVisualId { get; set; } = string.Empty;

        public GameObject Prefab { get; set; }
    }

    public sealed class SceneTemplateDefinition
    {
        public string SceneId { get; set; } = string.Empty;

        public string MapPath { get; set; } = string.Empty;
    }

    public sealed class SceneContext
    {
        public SceneContext(string sceneId, string sceneInstanceId)
        {
            SceneId = sceneId ?? string.Empty;
            SceneInstanceId = sceneInstanceId ?? string.Empty;
        }

        public string SceneId { get; }

        public string SceneInstanceId { get; }
    }

    public sealed class SceneInstance
    {
        private int _spawnCursor;

        public SceneInstance(
            string sceneId,
            string sceneInstanceId,
            TiledWorldMap map,
            GameObject rootObject,
            GameObject entitiesRoot)
        {
            SceneId = sceneId ?? string.Empty;
            SceneInstanceId = sceneInstanceId ?? string.Empty;
            Map = map;
            RootObject = rootObject;
            EntitiesRoot = entitiesRoot;
        }

        public string SceneId { get; }

        public string SceneInstanceId { get; }

        public TiledWorldMap Map { get; }

        public GameObject RootObject { get; }

        public GameObject EntitiesRoot { get; }

        public Transform EntityRoot
        {
            get { return EntitiesRoot.transform; }
        }

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
    }

    public sealed class SceneInstanceManager
    {
        private const float SceneSpacing = 1000f;
        private readonly object _sync = new object();
        private readonly GameObject _scenesRoot;
        private readonly TiledWorldBuilder _tiledWorldBuilder;
        private readonly Dictionary<string, SceneTemplateDefinition> _templatesBySceneId;
        private readonly Dictionary<string, SceneInstance> _instancesByKey = new Dictionary<string, SceneInstance>();
        private readonly string _defaultSceneId;
        private int _nextInstanceIndex;

        public SceneInstanceManager(
            GameObject scenesRoot,
            TiledWorldBuilder tiledWorldBuilder,
            string defaultSceneId,
            IReadOnlyList<SceneTemplateDefinition> sceneTemplates)
        {
            _scenesRoot = scenesRoot;
            _tiledWorldBuilder = tiledWorldBuilder;
            _defaultSceneId = string.IsNullOrWhiteSpace(defaultSceneId) ? "scene.default" : defaultSceneId;
            _templatesBySceneId = new Dictionary<string, SceneTemplateDefinition>(StringComparer.OrdinalIgnoreCase);

            var templates = sceneTemplates ?? Array.Empty<SceneTemplateDefinition>();
            for (var index = 0; index < templates.Count; index++)
            {
                var template = templates[index];
                if (template == null || string.IsNullOrWhiteSpace(template.SceneId) || string.IsNullOrWhiteSpace(template.MapPath))
                {
                    continue;
                }

                _templatesBySceneId[template.SceneId] = template;
            }

            DefaultSceneInstance = GetOrCreate(_defaultSceneId, "shared");
        }

        public SceneInstance DefaultSceneInstance { get; }

        public SceneInstance GetOrCreate(string sceneId, string sceneInstanceId)
        {
            lock (_sync)
            {
                var resolvedSceneId = ResolveSceneId(sceneId);
                var resolvedInstanceId = string.IsNullOrWhiteSpace(sceneInstanceId) ? "shared" : sceneInstanceId;
                var key = BuildKey(resolvedSceneId, resolvedInstanceId);

                if (_instancesByKey.TryGetValue(key, out var existingInstance))
                {
                    return existingInstance;
                }

                var template = ResolveTemplate(resolvedSceneId);
                var map = _tiledWorldBuilder.BuildFromFile(template.MapPath);
                var rootObject = new GameObject("SceneInstance:" + resolvedSceneId + ":" + resolvedInstanceId);
                rootObject.transform.SetParent(_scenesRoot.transform, false);
                rootObject.transform.position = ResolveSceneOffset(_nextInstanceIndex++);

                var entitiesRoot = new GameObject("Entities");
                entitiesRoot.transform.SetParent(rootObject.transform, false);

                var instance = new SceneInstance(resolvedSceneId, resolvedInstanceId, map, rootObject, entitiesRoot);
                _instancesByKey[key] = instance;
                return instance;
            }
        }

        public string ResolveSceneId(string sceneId)
        {
            return string.IsNullOrWhiteSpace(sceneId) ? _defaultSceneId : sceneId;
        }

        private SceneTemplateDefinition ResolveTemplate(string sceneId)
        {
            if (_templatesBySceneId.TryGetValue(sceneId, out var template))
            {
                return template;
            }

            if (_templatesBySceneId.TryGetValue(_defaultSceneId, out template))
            {
                return template;
            }

            throw new InvalidOperationException("No scene template has been registered for scene '" + sceneId + "'.");
        }

        private static string BuildKey(string sceneId, string sceneInstanceId)
        {
            return sceneId + "::" + sceneInstanceId;
        }

        private static Vector3 ResolveSceneOffset(int instanceIndex)
        {
            var column = instanceIndex % 8;
            var row = instanceIndex / 8;
            return new Vector3(column * SceneSpacing, 0f, row * SceneSpacing);
        }
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

        public PlayerEntity GetOrSpawnPlayer(PlayerSession session, string roomId, SceneInstance sceneInstance)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (sceneInstance == null)
            {
                throw new ArgumentNullException(nameof(sceneInstance));
            }

            lock (_sync)
            {
                if (_playerDefinition == null || string.IsNullOrWhiteSpace(_playerDefinition.EntityType))
                {
                    throw new InvalidOperationException("PlayerDefinition must be configured before spawning player entities.");
                }

                if (_playersByUserId.TryGetValue(session.UserId, out var existingPlayer) && existingPlayer != null)
                {
                    if (!string.Equals(existingPlayer.SceneId, sceneInstance.SceneId, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(existingPlayer.SceneInstanceId, sceneInstance.SceneInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        var relocationSpawn = sceneInstance.GetNextSpawnPoint();
                        existingPlayer.RelocateToScene(
                            sceneInstance.SceneId,
                            sceneInstance.SceneInstanceId,
                            sceneInstance.EntityRoot,
                            relocationSpawn.X,
                            relocationSpawn.Y);
                    }

                    existingPlayer.AttachSession(session.SessionId);
                    return existingPlayer;
                }

                var spawn = sceneInstance.GetNextSpawnPoint();
                var entityId = "player_" + session.UserId;
                var visualId = string.IsNullOrWhiteSpace(session.VisualId)
                    ? _playerDefinition.DefaultVisualId
                    : session.VisualId;
                var playerEntity = SpawnEntityInternal<PlayerEntity>(
                    _playerDefinition.EntityType,
                    entityId,
                    sceneInstance,
                    go =>
                    {
                        EnsurePlayerDefaults(go);
                    },
                    entity =>
                    {
                        entity.Initialize(
                            entityId,
                            _playerDefinition.EntityType,
                            _playerDefinition.DefaultArchetypeId,
                            roomId,
                            sceneInstance.SceneId,
                            sceneInstance.SceneInstanceId,
                            sceneInstance.EntityRoot,
                            session.UserId,
                            session.DisplayName,
                            visualId,
                            spawn.X,
                            spawn.Y);
                        entity.AttachSession(session.SessionId);
                    });

                _playersByUserId[session.UserId] = playerEntity;
                return playerEntity;
            }
        }

        public T SpawnEntity<T>(
            string entityType,
            string entityId,
            SceneInstance sceneInstance,
            Action<GameObject> configureObject,
            Action<T> initializeEntity)
            where T : NetworkEntity
        {
            lock (_sync)
            {
                return SpawnEntityInternal(entityType, entityId, sceneInstance, configureObject, initializeEntity);
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
            SceneInstance sceneInstance,
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

            var entityObject = CreateEntityObject(entityType, entityId, sceneInstance);
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

        private GameObject CreateEntityObject(string entityType, string entityId, SceneInstance sceneInstance)
        {
            GameObject entityObject;
            if (_prefabsByEntityType.TryGetValue(entityType, out var prefab) && prefab != null)
            {
                entityObject = UnityEngine.Object.Instantiate(prefab, sceneInstance != null ? sceneInstance.EntityRoot : _entityRoot);
            }
            else
            {
                entityObject = new GameObject(entityType + ":" + entityId);
                entityObject.transform.SetParent(sceneInstance != null ? sceneInstance.EntityRoot : _entityRoot, false);
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
        private readonly GameObject _worldRoot;
        private readonly GameObject _scenesRoot;

        public GameWorld(
            string worldId,
            string defaultSceneId,
            IReadOnlyList<SceneTemplateDefinition> sceneTemplates,
            TiledWorldBuilder tiledWorldBuilder,
            ServerNetworkSpawnerOptions spawnerOptions)
        {
            WorldId = worldId;
            _worldRoot = new GameObject("ServerWorld:" + worldId);
            _scenesRoot = new GameObject("Scenes");
            _scenesRoot.transform.SetParent(_worldRoot.transform, false);
            UnityEngine.Object.DontDestroyOnLoad(_worldRoot);
            SceneInstances = new SceneInstanceManager(
                _scenesRoot,
                tiledWorldBuilder,
                defaultSceneId,
                sceneTemplates);
            Spawner = new ServerNetworkSpawner(_scenesRoot.transform, Entities, spawnerOptions);
        }

        public string WorldId { get; }

        public TiledWorldMap Map
        {
            get { return SceneInstances.DefaultSceneInstance.Map; }
        }

        public EntityManager Entities { get; } = new EntityManager();

        public ServerNetworkSpawner Spawner { get; }

        public SceneInstanceManager SceneInstances { get; }

        public SceneInstance DefaultSceneInstance
        {
            get { return SceneInstances.DefaultSceneInstance; }
        }

        public long CurrentTick { get; private set; }

        public SceneInstance GetOrCreateSceneInstance(string sceneId, string sceneInstanceId)
        {
            return SceneInstances.GetOrCreate(sceneId, sceneInstanceId);
        }

        public SceneInstance MovePlayerToScene(PlayerEntity playerEntity, string sceneId, string sceneInstanceId)
        {
            if (playerEntity == null)
            {
                throw new ArgumentNullException(nameof(playerEntity));
            }

            var targetSceneInstance = SceneInstances.GetOrCreate(sceneId, sceneInstanceId);
            var spawnPoint = targetSceneInstance.GetNextSpawnPoint();
            playerEntity.RelocateToScene(
                targetSceneInstance.SceneId,
                targetSceneInstance.SceneInstanceId,
                targetSceneInstance.EntityRoot,
                spawnPoint.X,
                spawnPoint.Y);
            return targetSceneInstance;
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
