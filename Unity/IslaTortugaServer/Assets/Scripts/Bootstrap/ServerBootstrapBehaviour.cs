using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IslaTortuga.Server.Core.Embedded;
using IslaTortuga.Server.Core.World;
using IslaTortuga.Unity.Networking;
using UnityEngine;

namespace IslaTortuga.Unity.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ServerBootstrapBehaviour : MonoBehaviour
    {
        private const string DefaultRoomId = "room.default";
        private const string DefaultWorldId = "world.default";
        private const string DefaultPreferredMap = "island_01.tmj";
        private const float DefaultServerTickRateHz = 20f;

        [Header("Network Gateway")]
        [SerializeField] private bool enableNetworkGateway = true;
        [SerializeField] private string listenHost = "127.0.0.1";
        [SerializeField] private int listenPort = 5055;
        [SerializeField] private bool serveContentOverHttp = true;

        [Header("Spawn Policy")]
        [SerializeField] private bool despawnDisconnectedPlayers = true;
        [SerializeField] private NetworkEntityDefinitionAsset playerEntityDefinition;
        [SerializeField] private NetworkEntityDefinitionAsset[] entityDefinitions = Array.Empty<NetworkEntityDefinitionAsset>();

        private EmbeddedGameServerHost _server;
        private EmbeddedServerNetworkingHost _networkGateway;
        private ServerTickRunner _tickRunner;
        private Exception _startupException;
        private string _contentRoot = string.Empty;
        private string _mapPath = string.Empty;
        private string _defaultSceneId = "scene.default";
        private string _statusMessage = "Booting embedded server...";

        public EmbeddedGameServerHost Server
        {
            get { return _server; }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            BootstrapServer();
        }

        private void Update()
        {
            if (_server == null || _tickRunner == null)
            {
                return;
            }

            _tickRunner.Advance(Time.unscaledDeltaTime, RunServerTick);
        }

        private void OnGUI()
        {
            GUI.color = _startupException == null ? Color.white : new Color(1f, 0.65f, 0.65f, 1f);
            GUILayout.BeginArea(new Rect(16f, 16f, 560f, 230f), "Isla Tortuga Bootstrap", GUI.skin.window);
            GUILayout.Label(_statusMessage);

            if (_server != null)
            {
                GUILayout.Label("Map: " + _server.MapName);
                GUILayout.Label("Default Scene: " + _defaultSceneId);
                GUILayout.Label("Map Path: " + _mapPath);
                GUILayout.Label("Content Root: " + _contentRoot);
                GUILayout.Label("Rooms: " + _server.RoomCount + " | Sessions: " + _server.SessionCount + " | Players: " + _server.PlayerCount);
                GUILayout.Label("Tick: " + _server.CurrentTick + " @ " + DefaultServerTickRateHz + " Hz");
                GUILayout.Label("Despawn Disconnected Players: " + (despawnDisconnectedPlayers ? "Yes" : "No"));
                GUILayout.Label("Player Entity Type: " + ResolvePlayerEntityTypeLabel());

                if (_networkGateway != null)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label("HTTP: " + _networkGateway.BaseHttpUrl + "/health");
                    GUILayout.Label("WebSocket: " + _networkGateway.WebSocketUrl);

                    if (serveContentOverHttp)
                    {
                        GUILayout.Label("Content: " + _networkGateway.BaseHttpUrl + "/content/");
                    }
                }
            }

            if (_startupException != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label(_startupException.GetType().Name + ": " + _startupException.Message);
            }

            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            ShutdownGateway();
        }

        private void OnApplicationQuit()
        {
            ShutdownGateway();
        }

        private void BootstrapServer()
        {
            try
            {
                _contentRoot = ResolveContentRoot();
                _mapPath = ResolveMapPath(_contentRoot);
                _defaultSceneId = ResolveDefaultSceneId(_contentRoot);

                _server = new EmbeddedGameServerHost(new EmbeddedGameServerHostOptions
                {
                    DefaultMapPath = _mapPath,
                    DefaultSceneId = _defaultSceneId,
                    DefaultRoomId = DefaultRoomId,
                    DefaultWorldId = DefaultWorldId,
                    TickDeltaSeconds = 1f / DefaultServerTickRateHz,
                    TicketSecret = Environment.GetEnvironmentVariable("GAME_TICKET_SECRET"),
                    DespawnDisconnectedPlayers = despawnDisconnectedPlayers,
                    PlayerDefinition = BuildPlayerDefinition(),
                    PrefabDefinitions = BuildEntityDefinitions(),
                    SceneTemplates = BuildSceneTemplates(_contentRoot, _mapPath, _defaultSceneId),
                });
                _tickRunner = new ServerTickRunner(DefaultServerTickRateHz);

                if (enableNetworkGateway)
                {
                    _networkGateway = new EmbeddedServerNetworkingHost(
                        _server,
                        _contentRoot,
                        listenHost,
                        listenPort,
                        serveContentOverHttp);
                    _networkGateway.Start();
                }

                _statusMessage = _networkGateway == null
                    ? "Embedded server running inside Unity."
                    : "Embedded server and networking gateway running inside Unity.";
                Debug.Log("[IslaTortuga] Embedded server bootstrapped using map: " + _mapPath);
            }
            catch (Exception exception)
            {
                _startupException = exception;
                _statusMessage = "Failed to bootstrap embedded server.";
                Debug.LogException(exception);
            }
        }

        private static string ResolveContentRoot()
        {
            var configuredRoot = Environment.GetEnvironmentVariable("CONTENT_PACKS_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredRoot) && Directory.Exists(configuredRoot))
            {
                return Path.GetFullPath(configuredRoot);
            }

            var streamingAssetsCandidate = Path.Combine(Application.streamingAssetsPath, "content-packs");
            if (Directory.Exists(streamingAssetsCandidate))
            {
                return streamingAssetsCandidate;
            }

            var current = new DirectoryInfo(Application.dataPath);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "content-packs");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("No se encontro la carpeta content-packs para arrancar el servidor embebido.");
        }

        private static string ResolveMapPath(string contentRoot)
        {
            var preferredCandidate = Path.Combine(contentRoot, "v001", "maps", DefaultPreferredMap);
            if (File.Exists(preferredCandidate))
            {
                return preferredCandidate;
            }

            var anyMap = Directory.GetFiles(contentRoot, "*.tmj", SearchOption.AllDirectories);
            if (anyMap.Length > 0)
            {
                return anyMap[0];
            }

            throw new FileNotFoundException("No se encontro ningun mapa .tmj para el bootstrap del servidor.", contentRoot);
        }

        private static string ResolveDefaultSceneId(string contentRoot)
        {
            var indexPath = Path.Combine(contentRoot, "index.json");
            if (!File.Exists(indexPath))
            {
                return "scene.default";
            }

            try
            {
                var index = JsonUtility.FromJson<ContentIndexDto>(File.ReadAllText(indexPath));
                if (index == null || index.packs == null || index.packs.Length == 0)
                {
                    return "scene.default";
                }

                var defaultPack = index.packs.FirstOrDefault(pack =>
                    pack != null &&
                    string.Equals(pack.contentPackId, index.defaultContentPackId, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(pack.sceneId));

                if (defaultPack != null)
                {
                    return defaultPack.sceneId;
                }

                return index.packs.FirstOrDefault(pack => pack != null && !string.IsNullOrWhiteSpace(pack.sceneId))?.sceneId
                    ?? "scene.default";
            }
            catch
            {
                return "scene.default";
            }
        }

        private void ShutdownGateway()
        {
            if (_networkGateway == null)
            {
                return;
            }

            _networkGateway.Dispose();
            _networkGateway = null;
        }

        private void RunServerTick()
        {
            if (_networkGateway != null)
            {
                _networkGateway.PumpInboundMessages();
            }

            var deltas = _server.Tick();

            if (_networkGateway != null)
            {
                _networkGateway.BroadcastDeltas(deltas);
            }
        }

        private NetworkEntityPrefabDefinition[] BuildEntityDefinitions()
        {
            if (entityDefinitions == null || entityDefinitions.Length == 0)
            {
                return Array.Empty<NetworkEntityPrefabDefinition>();
            }

            return entityDefinitions
                .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.EntityType))
                .Select(definition => new NetworkEntityPrefabDefinition
                {
                    EntityType = definition.EntityType,
                    DefaultArchetypeId = definition.DefaultArchetypeId,
                    DefaultVisualId = definition.DefaultVisualId,
                    Prefab = definition.Prefab,
                })
                .GroupBy(definition => definition.EntityType, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToArray();
        }

        private static SceneTemplateDefinition[] BuildSceneTemplates(string contentRoot, string defaultMapPath, string defaultSceneId)
        {
            var templates = new List<SceneTemplateDefinition>
            {
                new SceneTemplateDefinition
                {
                    SceneId = defaultSceneId,
                    MapPath = defaultMapPath,
                }
            };

            if (Directory.Exists(contentRoot))
            {
                foreach (var mapFile in Directory.GetFiles(contentRoot, "*.tmj", SearchOption.AllDirectories))
                {
                    templates.Add(new SceneTemplateDefinition
                    {
                        SceneId = "scene." + Path.GetFileNameWithoutExtension(mapFile),
                        MapPath = mapFile,
                    });
                }
            }

            return templates
                .Where(template => template != null && !string.IsNullOrWhiteSpace(template.SceneId) && !string.IsNullOrWhiteSpace(template.MapPath))
                .GroupBy(template => template.SceneId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private NetworkEntityPrefabDefinition BuildPlayerDefinition()
        {
            if (playerEntityDefinition == null || string.IsNullOrWhiteSpace(playerEntityDefinition.EntityType))
            {
                throw new InvalidOperationException("Player Entity Definition must be assigned in Server Bootstrap.");
            }

            return new NetworkEntityPrefabDefinition
            {
                EntityType = playerEntityDefinition.EntityType,
                DefaultArchetypeId = playerEntityDefinition.DefaultArchetypeId,
                DefaultVisualId = playerEntityDefinition.DefaultVisualId,
                Prefab = playerEntityDefinition.Prefab,
            };
        }

        private string ResolvePlayerEntityTypeLabel()
        {
            return playerEntityDefinition == null || string.IsNullOrWhiteSpace(playerEntityDefinition.EntityType)
                ? "Not configured"
                : playerEntityDefinition.EntityType;
        }

        [Serializable]
        private sealed class ContentIndexDto
        {
            public string defaultContentPackId;
            public ContentPackDto[] packs;
        }

        [Serializable]
        private sealed class ContentPackDto
        {
            public string contentPackId;
            public string sceneId;
        }
    }

    internal sealed class ServerTickRunner
    {
        private readonly float _tickIntervalSeconds;
        private readonly int _maxTicksPerFrame;
        private float _accumulatorSeconds;

        public ServerTickRunner(float tickRateHz, int maxTicksPerFrame = 5)
        {
            TickRateHz = tickRateHz <= 0f ? 20f : tickRateHz;
            _tickIntervalSeconds = 1f / TickRateHz;
            _maxTicksPerFrame = maxTicksPerFrame < 1 ? 1 : maxTicksPerFrame;
        }

        public float TickRateHz { get; }

        public int Advance(float deltaTimeSeconds, Action tickAction)
        {
            if (tickAction == null)
            {
                throw new ArgumentNullException(nameof(tickAction));
            }

            _accumulatorSeconds += Math.Max(0f, deltaTimeSeconds);

            var executedThisFrame = 0;
            while (_accumulatorSeconds >= _tickIntervalSeconds && executedThisFrame < _maxTicksPerFrame)
            {
                _accumulatorSeconds -= _tickIntervalSeconds;
                tickAction();
                executedThisFrame++;
            }

            var maxBufferedSeconds = _tickIntervalSeconds * _maxTicksPerFrame;
            if (_accumulatorSeconds > maxBufferedSeconds)
            {
                _accumulatorSeconds = maxBufferedSeconds;
            }

            return executedThisFrame;
        }
    }
}
