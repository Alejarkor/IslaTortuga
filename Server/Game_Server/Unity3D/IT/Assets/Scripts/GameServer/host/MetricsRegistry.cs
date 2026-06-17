using System.Collections.Generic;
using System.Collections.Concurrent;

namespace IslaTortuga.GameServer.Host
{
    /// <summary>
    /// Registro central de métricas en memoria. Lo usa todo el resto del servidor
    /// para exponer contadores acumulativos (eventos que solo crecen) y gauges
    /// (valores instantáneos como partidas activas). Thread-safe: el tick, el
    /// gateway y la ControlApi pueden escribir/leer concurrentemente.
    /// </summary>
    public sealed class MetricsRegistry
    {
        private readonly ConcurrentDictionary<string, long> _counters =
            new ConcurrentDictionary<string, long>();

        private readonly ConcurrentDictionary<string, double> _gauges =
            new ConcurrentDictionary<string, double>();

        public void IncrementCounter(string name, long by = 1)
        {
            _counters.AddOrUpdate(name, by, (_, current) => current + by);
        }

        public long GetCounter(string name)
        {
            return _counters.TryGetValue(name, out var value) ? value : 0L;
        }

        public void SetGauge(string name, double value)
        {
            _gauges[name] = value;
        }

        public double GetGauge(string name)
        {
            return _gauges.TryGetValue(name, out var value) ? value : 0d;
        }

        /// <summary>Copia inmutable de todos los contadores, ordenada por nombre.</summary>
        public IReadOnlyDictionary<string, long> CountersSnapshot()
        {
            var result = new SortedDictionary<string, long>();
            foreach (var kvp in _counters)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        /// <summary>Copia inmutable de todos los gauges, ordenada por nombre.</summary>
        public IReadOnlyDictionary<string, double> GaugesSnapshot()
        {
            var result = new SortedDictionary<string, double>();
            foreach (var kvp in _gauges)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }
    }
}
