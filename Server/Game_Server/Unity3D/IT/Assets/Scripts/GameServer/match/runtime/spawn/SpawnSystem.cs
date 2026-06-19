using System.Collections.Generic;
using System.Numerics;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Instancia entidades de red en el mundo con id único, posición 3D y autoridad.
    /// El servidor solo crea ids y estado lógico (nunca binarios de asset). La
    /// difusión a clientes (SPAWN_ENTITY) la hace el gateway.
    /// </summary>
    public sealed class SpawnSystem
    {
        public const string PlayerPrefab = "player_default";

        private readonly NetworkWorld _world;
        private readonly NetworkEntityManager _entities;
        private readonly NetworkPrefabRegistry _prefabs;
        private readonly IServerLogger _logger;

        public SpawnSystem(
            NetworkWorld world,
            NetworkEntityManager entities,
            NetworkPrefabRegistry prefabs,
            IServerLogger logger = null)
        {
            _world = world;
            _entities = entities;
            _prefabs = prefabs;
            _logger = logger;
        }

        public NetworkEntity SpawnEntity(
            string prefabId,
            Vector3 position,
            Quaternion rotation,
            IDictionary<string, object> initialState = null,
            Authority authority = Authority.Server,
            string ownerId = null)
        {
            if (_prefabs != null && !_prefabs.IsRegistered(prefabId))
            {
                _logger?.Warn($"Spawn de prefab no registrado: {prefabId} (se permite igualmente).");
            }

            var entity = _entities.Create(prefabId, authority, ownerId);
            entity.Position = position;
            entity.Rotation = rotation;
            if (initialState != null)
            {
                foreach (var kv in initialState)
                {
                    entity.State[kv.Key] = kv.Value;
                }
            }
            _world.Add(entity);
            return entity;
        }

        /// <summary>Spawnea la entidad de un jugador: autoridad OWNER y su ownerId.</summary>
        public NetworkEntity SpawnPlayer(string ownerId, string prefabId = PlayerPrefab)
        {
            return SpawnEntity(prefabId, Vector3.Zero, Quaternion.Identity, null, Authority.Owner, ownerId);
        }
    }
}
