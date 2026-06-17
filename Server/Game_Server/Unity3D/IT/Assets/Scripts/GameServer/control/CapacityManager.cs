using System;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Control
{
    /// <summary>
    /// Instantánea de capacidad del host en un momento dado. Es lo que responde la
    /// ControlApi en /capacity y lo que el backend consulta antes de pedir crear
    /// una partida.
    /// </summary>
    public readonly struct CapacitySnapshot
    {
        public int ActiveMatches { get; }
        public int MaxMatches { get; }
        public int MaxPlayersPerMatch { get; }
        public bool CanAcceptMatch { get; }

        public CapacitySnapshot(int activeMatches, int maxMatches, int maxPlayersPerMatch, bool canAcceptMatch)
        {
            ActiveMatches = activeMatches;
            MaxMatches = maxMatches;
            MaxPlayersPerMatch = maxPlayersPerMatch;
            CanAcceptMatch = canAcceptMatch;
        }

        public int AvailableSlots => Math.Max(0, MaxMatches - ActiveMatches);
    }

    /// <summary>
    /// Lleva la cuenta de cuántas partidas hay vivas en este host y decide si se
    /// puede aceptar una más, según los límites de la configuración. Depende de
    /// ServerConfig (límites) y de MetricsRegistry (publica el gauge de partidas
    /// activas). Thread-safe: las reservas/liberaciones pueden llegar desde el plano
    /// de control mientras la ControlApi lee la capacidad.
    /// </summary>
    public sealed class CapacityManager
    {
        public const string ActiveMatchesGauge = "active_matches";

        private readonly ServerConfig _config;
        private readonly MetricsRegistry _metrics;
        private readonly object _gate = new object();

        private int _activeMatches;

        public CapacityManager(ServerConfig config, MetricsRegistry metrics)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            PublishGauge();
        }

        public int ActiveMatches
        {
            get
            {
                lock (_gate)
                {
                    return _activeMatches;
                }
            }
        }

        public int MaxMatches => _config.MaxMatches;

        /// <summary>True si hay hueco para una partida más.</summary>
        public bool CanAcceptMatch()
        {
            lock (_gate)
            {
                return _activeMatches < _config.MaxMatches;
            }
        }

        /// <summary>
        /// Reserva atómicamente un hueco para una partida. Devuelve false si no hay
        /// capacidad, evitando la condición de carrera de "comprobar y luego reservar".
        /// </summary>
        public bool TryReserveMatch()
        {
            lock (_gate)
            {
                if (_activeMatches >= _config.MaxMatches)
                {
                    return false;
                }

                _activeMatches++;
                PublishGaugeUnlocked();
                return true;
            }
        }

        /// <summary>Libera un hueco al terminar/abortar una partida. No baja de cero.</summary>
        public void ReleaseMatch()
        {
            lock (_gate)
            {
                if (_activeMatches > 0)
                {
                    _activeMatches--;
                    PublishGaugeUnlocked();
                }
            }
        }

        public CapacitySnapshot Snapshot()
        {
            lock (_gate)
            {
                return new CapacitySnapshot(
                    activeMatches: _activeMatches,
                    maxMatches: _config.MaxMatches,
                    maxPlayersPerMatch: _config.MaxPlayersPerMatch,
                    canAcceptMatch: _activeMatches < _config.MaxMatches);
            }
        }

        private void PublishGauge()
        {
            lock (_gate)
            {
                PublishGaugeUnlocked();
            }
        }

        private void PublishGaugeUnlocked()
        {
            _metrics.SetGauge(ActiveMatchesGauge, _activeMatches);
        }
    }
}
