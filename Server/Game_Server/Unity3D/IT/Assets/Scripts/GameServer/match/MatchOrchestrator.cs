using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Match
{
    /// <summary>
    /// Crea, registra y destruye MatchInstances dentro del host. Respeta la capacidad,
    /// dota a cada partida de su NetworkRuntime (Fase 3) y, si se configura un tiempo
    /// de vida máximo (matchMaxSeconds), arranca un reaper que autodestruye las
    /// partidas que lo superan.
    /// </summary>
    public sealed class MatchOrchestrator
    {
        private const string MatchesStartedCounter = "matches_started_total";
        private const string MatchesStoppedCounter = "matches_stopped_total";
        private const string MatchesExpiredCounter = "matches_expired_total";
        private const int ReapIntervalMs = 5000;

        private readonly CapacityManager _capacity;
        private readonly IServerLogger _logger;
        private readonly MetricsRegistry _metrics;
        private readonly int _tickRate;
        private readonly int _matchMaxSeconds;
        private readonly ConcurrentDictionary<string, MatchInstance> _matches =
            new ConcurrentDictionary<string, MatchInstance>();

        /// <param name="tickRate">Ritmo de tick para el NetworkRuntime; 0 = sin runtime (tests).</param>
        /// <param name="matchMaxSeconds">Vida máxima de una partida en segundos; 0 = sin límite.</param>
        public MatchOrchestrator(
            CapacityManager capacity,
            IServerLogger logger,
            MetricsRegistry metrics = null,
            int tickRate = 0,
            int matchMaxSeconds = 0)
        {
            _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics;
            _tickRate = tickRate;
            _matchMaxSeconds = matchMaxSeconds;

            if (_matchMaxSeconds > 0)
            {
                var reaper = new Thread(ReaperLoop) { IsBackground = true, Name = "match-reaper" };
                reaper.Start();
            }
        }

        public int ActiveMatchCount => _matches.Count;

        public MatchInstance CreateMatch(MatchConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!_capacity.TryReserveMatch())
            {
                _logger.Warn("create-match rechazado: sin capacidad.");
                return null;
            }

            try
            {
                var matchId = GenerateMatchId();
                var runtime = _tickRate > 0 ? new NetworkRuntime(_tickRate, _logger) : null;
                var instance = new MatchInstance(matchId, config, runtime);

                if (!_matches.TryAdd(matchId, instance))
                {
                    _capacity.ReleaseMatch();
                    throw new InvalidOperationException($"matchId duplicado: {matchId}");
                }

                instance.Start();
                _metrics?.IncrementCounter(MatchesStartedCounter);
                _logger.Info($"Partida creada: {matchId} (mapId={config.MapId}, jugadores={config.Players.Count}).");
                return instance;
            }
            catch
            {
                _capacity.ReleaseMatch();
                throw;
            }
        }

        public bool StopMatch(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return false;
            }
            if (!_matches.TryRemove(matchId, out var instance))
            {
                return false;
            }

            instance.Stop();
            _capacity.ReleaseMatch();
            _metrics?.IncrementCounter(MatchesStoppedCounter);
            _logger.Info($"Partida detenida: {matchId}.");
            return true;
        }

        public MatchInstance GetMatch(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return null;
            }
            return _matches.TryGetValue(matchId, out var instance) ? instance : null;
        }

        public IReadOnlyCollection<string> MatchIds => new List<string>(_matches.Keys);

        /// <summary>
        /// Detiene las partidas cuya antigüedad supera maxAge. Devuelve cuántas detuvo.
        /// Lo usa el reaper periódicamente; expuesto para poder probarlo sin esperar.
        /// </summary>
        public int ReapExpiredOlderThan(TimeSpan maxAge)
        {
            var now = DateTime.UtcNow;
            var expired = new List<string>();
            foreach (var kv in _matches)
            {
                if (now - kv.Value.CreatedAtUtc >= maxAge)
                {
                    expired.Add(kv.Key);
                }
            }
            foreach (var id in expired)
            {
                _logger.Info($"Partida {id} destruida por tiempo (límite {_matchMaxSeconds}s).");
                if (StopMatch(id))
                {
                    _metrics?.IncrementCounter(MatchesExpiredCounter);
                }
            }
            return expired.Count;
        }

        private void ReaperLoop()
        {
            var maxAge = TimeSpan.FromSeconds(_matchMaxSeconds);
            while (true)
            {
                Thread.Sleep(ReapIntervalMs);
                try
                {
                    ReapExpiredOlderThan(maxAge);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error en el reaper de partidas.", ex);
                }
            }
        }

        private static string GenerateMatchId()
        {
            return "match_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }
    }
}
