using System.Collections.Generic;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Catálogo lógico de networkPrefabIds válidos. El servidor solo maneja el id;
    /// el cliente resuelve el binario por su manifest. Sirve para validar que no se
    /// spawnea un prefab desconocido.
    /// </summary>
    public sealed class NetworkPrefabRegistry
    {
        private readonly HashSet<string> _prefabs = new HashSet<string>();

        public NetworkPrefabRegistry(IEnumerable<string> prefabs = null)
        {
            if (prefabs != null)
            {
                foreach (var p in prefabs)
                {
                    Register(p);
                }
            }
        }

        public void Register(string prefabId)
        {
            if (!string.IsNullOrEmpty(prefabId))
            {
                _prefabs.Add(prefabId);
            }
        }

        public bool IsRegistered(string prefabId)
        {
            return prefabId != null && _prefabs.Contains(prefabId);
        }

        public int Count => _prefabs.Count;
    }
}
