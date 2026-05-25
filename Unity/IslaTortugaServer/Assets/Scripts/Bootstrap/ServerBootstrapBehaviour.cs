using System;
using System.IO;
using IslaTortuga.Server.Core.Embedded;
using UnityEngine;

namespace IslaTortuga.Unity.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ServerBootstrapBehaviour : MonoBehaviour
    {
        private const string DefaultRoomId = "room.default";
        private const string DefaultWorldId = "world.default";
        private const string DefaultPreferredMap = "island_01.tmj";

        private EmbeddedGameServerHost _server;
        private Exception _startupException;
        private string _contentRoot = string.Empty;
        private string _mapPath = string.Empty;
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

        private void FixedUpdate()
        {
            if (_server == null)
            {
                return;
            }

            _server.Tick();
        }

        private void OnGUI()
        {
            GUI.color = _startupException == null ? Color.white : new Color(1f, 0.65f, 0.65f, 1f);
            GUILayout.BeginArea(new Rect(16f, 16f, 520f, 180f), "Isla Tortuga Bootstrap", GUI.skin.window);
            GUILayout.Label(_statusMessage);

            if (_server != null)
            {
                GUILayout.Label("Map: " + _server.MapName);
                GUILayout.Label("Map Path: " + _mapPath);
                GUILayout.Label("Content Root: " + _contentRoot);
                GUILayout.Label("Rooms: " + _server.RoomCount + " | Sessions: " + _server.SessionCount + " | Players: " + _server.PlayerCount);
                GUILayout.Label("Tick: " + _server.CurrentTick);
            }

            if (_startupException != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label(_startupException.GetType().Name + ": " + _startupException.Message);
            }

            GUILayout.EndArea();
        }

        private void BootstrapServer()
        {
            try
            {
                _contentRoot = ResolveContentRoot();
                _mapPath = ResolveMapPath(_contentRoot);

                _server = new EmbeddedGameServerHost(new EmbeddedGameServerHostOptions
                {
                    DefaultMapPath = _mapPath,
                    DefaultRoomId = DefaultRoomId,
                    DefaultWorldId = DefaultWorldId,
                    TickDeltaSeconds = Time.fixedDeltaTime,
                    TicketSecret = Environment.GetEnvironmentVariable("GAME_TICKET_SECRET"),
                });

                _statusMessage = "Embedded server running inside Unity.";
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
    }
}
