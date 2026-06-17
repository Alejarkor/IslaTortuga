using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Match
{
    /// <summary>
    /// Crea, registra y destruye MatchInstances dentro del host. Es el puente entre el
    /// plano de control (create-match / stop-match) y las partidas vivas. Respeta la
    /// capacidad: cada creación reserva un hueco en el CapacityManager y cada parada lo
    /// libera, de modo que /capacity refleja la realidad en todo momento.
    /// </summary>
    public sealed class MatchOrchestrator
    {
        private const string MatchesStartedCounter = "matches_started_total";
        private const string MatchesStoppedCounter = "matches_stopped_total";

        private readonly CapacityManager _capacity;
        private readonly IServerLogger _logger;
        private readonly MetricsRegistry _metrics;
        private readonly ConcurrentDictionary<string, MatchInstance> _matches =
            new ConcurrentDictionary<string, MatchInstance>();

        public MatchOrchestrator(CapacityManager capacity, IServerLogger logger, MetricsRegistry metrics = null)
        {
            _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics;
        }

        public int ActiveMatchCount => _matches.Count;

        /// <summary>
        /// Crea una partida si hay capacidad. Devuelve la MatchInstance o null si el host
        /// está lleno (el llamante debe responder 409 en ese caso).
        /// </summary>
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
                var instance = new MatchInstance(matchId, config);

                if (!_matches.TryAdd(matchId, instance))
                {
                    // Colisión de id prácticamente imposible; si ocurre, liberamos el hueco.
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

        /// <summary>Detiene y elimina una partida. Devuelve false si no existe.</summary>
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

        private static string GenerateMatchId()
        {
            return "match_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }
    }
}
