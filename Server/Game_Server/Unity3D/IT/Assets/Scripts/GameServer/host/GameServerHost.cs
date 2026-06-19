using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Match;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Cimiento del Game Server. Compone y posee las piezas de infraestructura del
    /// host (config, logger, métricas, capacidad, orquestador, plano de control y, a
    /// partir de la Fase 2, el PlayerGateway realtime) y gobierna su ciclo de vida:
    /// arranque ordenado y apagado limpio.
    ///
    /// No depende de UnityEngine: el MonoBehaviour GameServerBootstrap es solo un
    /// envoltorio fino que lo arranca dentro de Unity.
    /// </summary>
    public sealed class GameServerHost
    {
        private const string StartsCounter = "server_starts_total";

        private readonly object _gate = new object();
        private readonly Stopwatch _uptime = new Stopwatch();

        private bool _started;
        private bool _shuttingDown;

        public ServerConfig Config { get; }
        public IServerLogger Logger { get; }
        public MetricsRegistry Metrics { get; }
        public CapacityManager Capacity { get; }
        public MatchOrchestrator Matches { get; }
        public ControlApi ControlApi { get; }
        public PlayerSessionManager Sessions { get; }
        public ITicketValidator TicketValidator { get; }
        public PlayerGateway Gateway { get; }

        public bool IsRunning
        {
            get { lock (_gate) { return _started && !_shuttingDown; } }
        }

        public double UptimeSeconds => _uptime.Elapsed.TotalSeconds;

        public GameServerHost(
            ServerConfig config,
            IServerLogger logger = null,
            MetricsRegistry metrics = null,
            ITicketValidator ticketValidator = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Logger = logger ?? new ConsoleServerLogger();
            Metrics = metrics ?? new MetricsRegistry();
            Capacity = new CapacityManager(Config, Metrics);
            Matches = new MatchOrchestrator(Capacity, Logger, Metrics, Config.TickRate, Config.MatchMaxSeconds);
            ControlApi = new ControlApi(Config, Capacity, Logger, () => UptimeSeconds, Matches);
            Sessions = new PlayerSessionManager();
            TicketValidator = ticketValidator ?? new HttpTicketValidator(Config.GameApiUrl, Logger);
            Gateway = new PlayerGateway(Config, Matches, TicketValidator, Sessions, Logger, Metrics);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_started)
                {
                    return Task.CompletedTask;
                }
                _started = true;
                _shuttingDown = false;
            }

            _uptime.Restart();
            Metrics.IncrementCounter(StartsCounter);

            Logger.Info("== Game Server arrancando ==");
            Logger.Info(Config.ToString());

            ControlApi.Start();
            Gateway.Start();

            Logger.Info("Game Server listo. Control y gateway en marcha.");
            return Task.CompletedTask;
        }

        public async Task ShutdownGracefullyAsync()
        {
            lock (_gate)
            {
                if (!_started || _shuttingDown)
                {
                    return;
                }
                _shuttingDown = true;
            }

            Logger.Info("== Game Server apagándose (shutdown ordenado) ==");

            try
            {
                await Gateway.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error("Error al detener el PlayerGateway durante el apagado.", ex);
            }

            try
            {
                await ControlApi.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error("Error al detener la ControlApi durante el apagado.", ex);
            }

            _uptime.Stop();

            lock (_gate)
            {
                _started = false;
                _shuttingDown = false;
            }

            Logger.Info("Game Server apagado limpiamente.");
        }
    }
}
