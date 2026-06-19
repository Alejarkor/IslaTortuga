using System.Collections.Concurrent;
using System.Collections.Generic;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// El mundo de entidades de una partida: contenedor en memoria, indexado por
    /// networkEntityId. Thread-safe: el tick lo recorre mientras spawn/despawn
    /// (Fase 4) y el gateway lo modifican concurrentemente.
    /// </summary>
    public sealed class NetworkWorld
    {
        private readonly ConcurrentDictionary<string, NetworkEntity> _entities =
            new ConcurrentDictionary<string, NetworkEntity>();

        public int Count => _entities.Count;

        public void Add(NetworkEntity entity)
        {
            _entities[entity.Id] = entity;
        }

        public NetworkEntity Get(string id)
        {
            return id != null && _entities.TryGetValue(id, out var e) ? e : null;
        }

        public bool Remove(string id)
        {
            return id != null && _entities.TryRemove(id, out _);
        }

        public bool Contains(string id)
        {
            return id != null && _entities.ContainsKey(id);
        }

        /// <summary>Copia inmutable de todas las entidades (segura para iterar).</summary>
        public IReadOnlyCollection<NetworkEntity> All()
        {
            return new List<NetworkEntity>(_entities.Values);
        }
    }
}
