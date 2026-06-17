using System;
using UnityEngine;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Punto de entrada del Game Server dentro de Unity. Es un envoltorio fino:
    /// construye la configuración, crea el GameServerHost y lo arranca. Toda la
    /// lógica vive en clases POCO probables; este MonoBehaviour solo conecta el
    /// ciclo de vida de Unity (Awake/OnApplicationQuit) y las señales del SO con el
    /// host.
    ///
    /// Uso: colócalo en un GameObject de la escena de arranque del servidor
    /// dedicado. En una build headless (-batchmode -nographics) una señal SIGTERM
    /// provoca el quit de Unity, que dispara OnApplicationQuit y el apagado ordenado.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameServerBootstrap : MonoBehaviour
    {
        [Tooltip("Si está activo, la configuración se lee de variables de entorno (GS_*) con caída a valores por defecto.")]
        [SerializeField] private bool _useEnvironmentConfig = true;

        public GameServerHost Host { get; private set; }

        private bool _signalsHooked;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            ServerConfig config;
            try
            {
                config = _useEnvironmentConfig
                    ? ServerConfig.FromEnvironment()
                    : ServerConfig.Default();
            }
            catch (ServerConfigException ex)
            {
                Debug.LogError($"[GameServer] Configuración inválida, abortando arranque: {ex.Message}");
                Application.Quit(1);
                return;
            }

            // En el Editor enviamos los logs a la ventana Console (UnityDebugLogger).
            // En una build dedicada headless usamos la consola estándar (Player.log).
            IServerLogger logger = Application.isEditor
                ? new UnityDebugLogger()
                : (IServerLogger)new ConsoleServerLogger();

            Host = new GameServerHost(config, logger);

            HookProcessSignals();

            try
            {
                Host.StartAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameServer] Fallo en el arranque: {ex}");
                Application.Quit(1);
            }
        }

        private void OnApplicationQuit()
        {
            ShutdownBlocking();
        }

        private void OnDestroy()
        {
            ShutdownBlocking();
        }

        private void HookProcessSignals()
        {
            if (_signalsHooked)
            {
                return;
            }
            _signalsHooked = true;

            // Ctrl+C en consola: apaga ordenadamente en lugar de matar el proceso.
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                ShutdownBlocking();
                Application.Quit();
            };

            // Último recurso si el proceso termina sin pasar por OnApplicationQuit.
            AppDomain.CurrentDomain.ProcessExit += (_, __) => ShutdownBlocking();
        }

        private void ShutdownBlocking()
        {
            var host = Host;
            if (host == null)
            {
                return;
            }

            try
            {
                host.ShutdownGracefullyAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameServer] Error durante el apagado: {ex}");
            }
        }
    }
}
