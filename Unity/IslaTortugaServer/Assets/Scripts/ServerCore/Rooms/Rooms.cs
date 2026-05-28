using System;
using System.Collections.Generic;
using System.Linq;
using IslaTortuga.Server.Core.Sessions;
using IslaTortuga.Server.Core.World;
using IslaTortuga.Server.Core.World.Tiled;

namespace IslaTortuga.Server.Core.Rooms
{
    public enum RoomState
    {
        Initializing = 1,
        Running = 2,
        Closing = 3,
    }

    public sealed class RoomPlayer
    {
        public RoomPlayer(GameRoom room, PlayerSession session, PlayerEntity playerEntity)
        {
            Room = room;
            Session = session;
            PlayerEntity = playerEntity;
        }

        public GameRoom Room { get; }

        public PlayerSession Session { get; }

        public PlayerEntity PlayerEntity { get; }
    }

    public sealed class GameRoom
    {
        private readonly Dictionary<string, RoomPlayer> _playersBySessionId = new Dictionary<string, RoomPlayer>();
        private readonly Dictionary<string, string> _activeSessionIdByUserId = new Dictionary<string, string>();
        private readonly object _sync = new object();

        public GameRoom(string roomId, GameWorld world)
        {
            RoomId = roomId;
            World = world;
            State = RoomState.Running;
        }

        public string RoomId { get; }

        public RoomState State { get; private set; }

        public GameWorld World { get; }

        public IReadOnlyCollection<RoomPlayer> Players
        {
            get
            {
                lock (_sync)
                {
                    return _playersBySessionId.Values.ToArray();
                }
            }
        }

        public RoomPlayer AddOrGetPlayer(PlayerSession session)
        {
            lock (_sync)
            {
                RoomPlayer existingPlayer;
                if (_playersBySessionId.TryGetValue(session.SessionId, out existingPlayer))
                {
                    return existingPlayer;
                }

                if (_activeSessionIdByUserId.TryGetValue(session.UserId, out var previousSessionId) &&
                    !string.IsNullOrWhiteSpace(previousSessionId) &&
                    previousSessionId != session.SessionId)
                {
                    _playersBySessionId.Remove(previousSessionId);
                }

                var sceneInstance = World.GetOrCreateSceneInstance(session.SceneId, session.SceneInstanceId);
                var playerEntity = World.Spawner.GetOrSpawnPlayer(session, RoomId, sceneInstance);
                session.BindToRoom(RoomId, playerEntity.EntityId);
                session.BindSceneContext(playerEntity.SceneId, playerEntity.SceneInstanceId);

                var roomPlayer = new RoomPlayer(this, session, playerEntity);
                _playersBySessionId[session.SessionId] = roomPlayer;
                _activeSessionIdByUserId[session.UserId] = session.SessionId;
                return roomPlayer;
            }
        }

        public void HandleSessionDisconnected(PlayerSession session, bool despawnDisconnectedPlayers)
        {
            lock (_sync)
            {
                if (session == null)
                {
                    return;
                }

                _playersBySessionId.Remove(session.SessionId);

                if (_activeSessionIdByUserId.TryGetValue(session.UserId, out var activeSessionId) &&
                    activeSessionId == session.SessionId)
                {
                    _activeSessionIdByUserId.Remove(session.UserId);
                }

                World.Spawner.HandlePlayerDisconnected(session, despawnDisconnectedPlayers);
            }
        }

        public T SpawnEntity<T>(
            string entityType,
            string entityId,
            SceneInstance sceneInstance,
            Action<UnityEngine.GameObject> configureObject,
            Action<T> initializeEntity)
            where T : NetworkEntity
        {
            return World.Spawner.SpawnEntity(entityType, entityId, sceneInstance, configureObject, initializeEntity);
        }

        public bool TryTransitionSessionToScene(PlayerSession session, string sceneId, string sceneInstanceId)
        {
            lock (_sync)
            {
                if (session == null)
                {
                    return false;
                }

                if (!_playersBySessionId.TryGetValue(session.SessionId, out var roomPlayer) || roomPlayer == null)
                {
                    return false;
                }

                var targetSceneInstance = World.MovePlayerToScene(roomPlayer.PlayerEntity, sceneId, sceneInstanceId);
                session.BindSceneContext(targetSceneInstance.SceneId, targetSceneInstance.SceneInstanceId);
                return true;
            }
        }

        public void Tick(float deltaSeconds)
        {
            World.Tick(deltaSeconds);
        }
    }

    public sealed class GameRoomManagerOptions
    {
        public string DefaultMapPath { get; set; } = string.Empty;

        public string DefaultSceneId { get; set; } = "scene.default";

        public string DefaultRoomId { get; set; } = "room.default";

        public string DefaultWorldId { get; set; } = "world.default";

        public bool DespawnDisconnectedPlayers { get; set; } = true;

        public NetworkEntityPrefabDefinition PlayerDefinition { get; set; }

        public IReadOnlyList<NetworkEntityPrefabDefinition> PrefabDefinitions { get; set; } = Array.Empty<NetworkEntityPrefabDefinition>();

        public IReadOnlyList<SceneTemplateDefinition> SceneTemplates { get; set; } = Array.Empty<SceneTemplateDefinition>();
    }

    public sealed class GameRoomManager
    {
        private readonly Dictionary<string, GameRoom> _rooms = new Dictionary<string, GameRoom>();
        private readonly object _sync = new object();
        private readonly GameRoomManagerOptions _options;

        public GameRoomManager(GameRoomManagerOptions options, TiledWorldBuilder tiledWorldBuilder)
        {
            if (string.IsNullOrWhiteSpace(options.DefaultMapPath))
            {
                throw new ArgumentException("DefaultMapPath is required to bootstrap the embedded game server.", nameof(options));
            }

            var world = new GameWorld(
                options.DefaultWorldId,
                options.DefaultSceneId,
                options.SceneTemplates,
                tiledWorldBuilder,
                new ServerNetworkSpawnerOptions
                {
                    PlayerDefinition = options.PlayerDefinition,
                    PrefabDefinitions = options.PrefabDefinitions,
                });
            var room = new GameRoom(options.DefaultRoomId, world);

            _rooms[room.RoomId] = room;
            DefaultRoom = room;
            _options = options;
        }

        public GameRoom DefaultRoom { get; }

        public IReadOnlyCollection<GameRoom> GetAllRooms()
        {
            lock (_sync)
            {
                return _rooms.Values.ToArray();
            }
        }

        public RoomPlayer AttachOrGetSession(PlayerSession session)
        {
            lock (_sync)
            {
                GameRoom room;
                if (!string.IsNullOrWhiteSpace(session.RoomId) &&
                    _rooms.TryGetValue(session.RoomId, out room))
                {
                    return room.AddOrGetPlayer(session);
                }

                if (string.IsNullOrWhiteSpace(session.SceneId))
                {
                    session.BindSceneContext(DefaultRoom.World.DefaultSceneInstance.SceneId, DefaultRoom.World.DefaultSceneInstance.SceneInstanceId);
                }

                return DefaultRoom.AddOrGetPlayer(session);
            }
        }

        public void HandleSessionDisconnected(PlayerSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (_sync)
            {
                GameRoom room;
                if (!string.IsNullOrWhiteSpace(session.RoomId) &&
                    _rooms.TryGetValue(session.RoomId, out room))
                {
                    room.HandleSessionDisconnected(session, _options.DespawnDisconnectedPlayers);
                    return;
                }

                DefaultRoom.HandleSessionDisconnected(session, _options.DespawnDisconnectedPlayers);
            }
        }

        public bool TryTransitionSessionToScene(PlayerSession session, string sceneId, string sceneInstanceId)
        {
            if (session == null)
            {
                return false;
            }

            lock (_sync)
            {
                GameRoom room;
                if (!string.IsNullOrWhiteSpace(session.RoomId) &&
                    _rooms.TryGetValue(session.RoomId, out room))
                {
                    return room.TryTransitionSessionToScene(session, sceneId, sceneInstanceId);
                }

                return DefaultRoom.TryTransitionSessionToScene(session, sceneId, sceneInstanceId);
            }
        }

        public void TickAll(float deltaSeconds)
        {
            lock (_sync)
            {
                foreach (var room in _rooms.Values)
                {
                    room.Tick(deltaSeconds);
                }
            }
        }
    }
}
