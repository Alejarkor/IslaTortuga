using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using IslaTortuga.Server.Core.Protocol;
using IslaTortuga.Server.Core.Replication;
using IslaTortuga.Server.Core.Rooms;
using IslaTortuga.Server.Core.Sessions;
using IslaTortuga.Server.Core.World;
using IslaTortuga.Server.Core.World.Scenes;

namespace IslaTortuga.Server.Core.Embedded
{
    public sealed class EmbeddedGameServerHostOptions
    {
        public string DefaultScenePath { get; set; } = string.Empty;

        public string DefaultSceneId { get; set; } = "scene.default";

        public string DefaultRoomId { get; set; } = "room.default";

        public string DefaultWorldId { get; set; } = "world.default";

        public string TicketSecret { get; set; }

        public float TickDeltaSeconds { get; set; } = 0.05f;

        public bool DespawnDisconnectedPlayers { get; set; } = true;

        public NetworkEntityPrefabDefinition PlayerDefinition { get; set; }

        public IReadOnlyList<NetworkEntityPrefabDefinition> PrefabDefinitions { get; set; } = Array.Empty<NetworkEntityPrefabDefinition>();

        public IReadOnlyList<SceneTemplateDefinition> SceneTemplates { get; set; } = Array.Empty<SceneTemplateDefinition>();
    }

    public sealed class EmbeddedGameServerJoinResult
    {
        public EmbeddedGameServerJoinResult(AuthAcceptedPayload auth, SceneContextPayload scene, WorldDeltaPayload delta)
        {
            Auth = auth;
            Scene = scene;
            Delta = delta;
        }

        public AuthAcceptedPayload Auth { get; }

        public SceneContextPayload Scene { get; }

        public WorldDeltaPayload Delta { get; }
    }

    public sealed class EmbeddedGameServerTickResult
    {
        public EmbeddedGameServerTickResult(
            string sessionId,
            string userId,
            string roomId,
            string playerEntityId,
            SceneContextPayload sceneChange,
            WorldDeltaPayload delta)
        {
            SessionId = sessionId;
            UserId = userId;
            RoomId = roomId;
            PlayerEntityId = playerEntityId;
            SceneChange = sceneChange;
            Delta = delta;
        }

        public string SessionId { get; }

        public string UserId { get; }

        public string RoomId { get; }

        public string PlayerEntityId { get; }

        public SceneContextPayload SceneChange { get; }

        public WorldDeltaPayload Delta { get; }
    }

    public sealed class EmbeddedGameServerHost
    {
        private readonly GameTicketService _gameTicketService;
        private readonly SessionManager _sessionManager;
        private readonly GameRoomManager _gameRoomManager;
        private readonly DeltaBuilder _deltaBuilder;
        private readonly ConcurrentDictionary<string, string> _sceneContextBySessionId = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentQueue<string> _pendingDisconnectedConnectionIds = new ConcurrentQueue<string>();

        public EmbeddedGameServerHost(EmbeddedGameServerHostOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.DefaultScenePath))
            {
                throw new ArgumentException("DefaultScenePath is required to bootstrap the embedded game server.", nameof(options));
            }

            TickDeltaSeconds = options.TickDeltaSeconds <= 0 ? 0.05f : options.TickDeltaSeconds;
            ScenePath = options.DefaultScenePath;

            _gameTicketService = new GameTicketService(options.TicketSecret);
            _sessionManager = new SessionManager();

            var sceneTemplateBuilder = new SceneTemplateBuilder();
            _gameRoomManager = new GameRoomManager(
                new GameRoomManagerOptions
                {
                    DefaultScenePath = options.DefaultScenePath,
                    DefaultSceneId = options.DefaultSceneId,
                    DefaultRoomId = options.DefaultRoomId,
                    DefaultWorldId = options.DefaultWorldId,
                    DespawnDisconnectedPlayers = options.DespawnDisconnectedPlayers,
                    PlayerDefinition = options.PlayerDefinition,
                    PrefabDefinitions = options.PrefabDefinitions,
                    SceneTemplates = options.SceneTemplates,
                },
                sceneTemplateBuilder);

            _deltaBuilder = new DeltaBuilder(
                new InterestManager(),
                new EntityReplicator(),
                new ReplicationStateStore());
        }

        public float TickDeltaSeconds { get; }

        public string ScenePath { get; }

        public string SceneName
        {
            get { return _gameRoomManager.DefaultRoom.World.SceneData.DisplayName; }
        }

        public int RoomCount
        {
            get { return _gameRoomManager.GetAllRooms().Count; }
        }

        public int SessionCount
        {
            get { return _sessionManager.Count; }
        }

        public int PlayerCount
        {
            get { return _gameRoomManager.DefaultRoom.Players.Count; }
        }

        public long CurrentTick
        {
            get { return _gameRoomManager.DefaultRoom.World.CurrentTick; }
        }

        public GameTicket CreateJoinTicket(string userId, string displayName, string visualId)
        {
            return _gameTicketService.CreateJoinTicket(userId, displayName, visualId);
        }

        public GameRoom DefaultRoom
        {
            get { return _gameRoomManager.DefaultRoom; }
        }

        public ServerNetworkSpawner Spawner
        {
            get { return _gameRoomManager.DefaultRoom.World.Spawner; }
        }

        public bool TryJoin(
            string signedTicket,
            string connectionId,
            out EmbeddedGameServerJoinResult result,
            out string errorCode)
        {
            result = null;

            GameTicket ticket;
            if (!_gameTicketService.TryConsume(signedTicket, TicketPurpose.Join, out ticket, out errorCode))
            {
                return false;
            }

            var session = _sessionManager.CreateSession(ticket, connectionId);
            var roomPlayer = _gameRoomManager.AttachOrGetSession(session);
            result = BuildJoinResult(roomPlayer);
            return true;
        }

        public bool TryReconnect(
            string signedTicket,
            string connectionId,
            out EmbeddedGameServerJoinResult result,
            out string errorCode)
        {
            result = null;

            GameTicket ticket;
            if (!_gameTicketService.TryConsume(signedTicket, TicketPurpose.Reconnect, out ticket, out errorCode))
            {
                return false;
            }

            var session = _sessionManager.ReconnectSession(ticket, connectionId);
            var roomPlayer = _gameRoomManager.AttachOrGetSession(session);
            result = BuildJoinResult(roomPlayer);
            return true;
        }

        public bool ApplyPlayerInput(string sessionId, float moveX, float moveY)
        {
            PlayerSession session;
            if (!_sessionManager.TryGetBySessionId(sessionId, out session))
            {
                return false;
            }

            var roomPlayer = _gameRoomManager.AttachOrGetSession(session);
            roomPlayer.PlayerEntity.ApplyInput(moveX, moveY);
            return true;
        }

        public bool TryTransitionSessionToScene(string sessionId, string sceneId, string sceneInstanceId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            if (!_sessionManager.TryGetBySessionId(sessionId, out var session))
            {
                return false;
            }

            return _gameRoomManager.TryTransitionSessionToScene(session, sceneId, sceneInstanceId);
        }

        public IReadOnlyList<EmbeddedGameServerTickResult> Tick()
        {
            FlushDisconnectedConnections();
            _gameRoomManager.TickAll(TickDeltaSeconds);
            return BuildDeltas();
        }

        public IReadOnlyList<EmbeddedGameServerTickResult> BuildDeltas()
        {
            var deltas = new List<EmbeddedGameServerTickResult>();

            foreach (var room in _gameRoomManager.GetAllRooms())
            {
                foreach (var roomPlayer in room.Players)
                {
                    var sceneChange = ResolveSceneChange(roomPlayer);
                    if (sceneChange != null)
                    {
                        _deltaBuilder.ResetSession(roomPlayer.Session.SessionId);
                    }

                    var delta = _deltaBuilder.Build(room, roomPlayer);
                    deltas.Add(new EmbeddedGameServerTickResult(
                        roomPlayer.Session.SessionId,
                        roomPlayer.Session.UserId,
                        room.RoomId,
                        roomPlayer.PlayerEntity.EntityId,
                        sceneChange,
                        delta));
                }
            }

            return deltas;
        }

        public void MarkDisconnected(string connectionId)
        {
            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                _pendingDisconnectedConnectionIds.Enqueue(connectionId);
            }
        }

        private EmbeddedGameServerJoinResult BuildJoinResult(RoomPlayer roomPlayer)
        {
            _deltaBuilder.ResetSession(roomPlayer.Session.SessionId);
            var sceneContext = BuildSceneContextPayload(roomPlayer.PlayerEntity);
            RememberSceneContext(roomPlayer.Session.SessionId, sceneContext);

            return new EmbeddedGameServerJoinResult(
                new AuthAcceptedPayload(
                    roomPlayer.Session.SessionId,
                    roomPlayer.Session.UserId,
                    roomPlayer.Session.DisplayName,
                    roomPlayer.Room.RoomId,
                    roomPlayer.PlayerEntity.EntityId),
                sceneContext,
                _deltaBuilder.Build(roomPlayer.Room, roomPlayer));
        }

        private void FlushDisconnectedConnections()
        {
            while (_pendingDisconnectedConnectionIds.TryDequeue(out var connectionId))
            {
                var disconnectedSession = _sessionManager.MarkDisconnected(connectionId);
                if (disconnectedSession != null)
                {
                    _sceneContextBySessionId.TryRemove(disconnectedSession.SessionId, out _);
                    _gameRoomManager.HandleSessionDisconnected(disconnectedSession);
                }
            }
        }

        private SceneContextPayload ResolveSceneChange(RoomPlayer roomPlayer)
        {
            var sceneContext = BuildSceneContextPayload(roomPlayer.PlayerEntity);
            var key = BuildSceneContextKey(sceneContext);

            if (_sceneContextBySessionId.TryGetValue(roomPlayer.Session.SessionId, out var knownSceneKey) &&
                string.Equals(knownSceneKey, key, StringComparison.Ordinal))
            {
                return null;
            }

            RememberSceneContext(roomPlayer.Session.SessionId, sceneContext);
            return sceneContext;
        }

        private static SceneContextPayload BuildSceneContextPayload(PlayerEntity playerEntity)
        {
            return new SceneContextPayload(
                playerEntity.SceneId,
                playerEntity.SceneInstanceId);
        }

        private void RememberSceneContext(string sessionId, SceneContextPayload sceneContext)
        {
            _sceneContextBySessionId[sessionId] = BuildSceneContextKey(sceneContext);
        }

        private static string BuildSceneContextKey(SceneContextPayload sceneContext)
        {
            return (sceneContext.SceneId ?? string.Empty) + "::" + (sceneContext.SceneInstanceId ?? string.Empty);
        }
    }
}
