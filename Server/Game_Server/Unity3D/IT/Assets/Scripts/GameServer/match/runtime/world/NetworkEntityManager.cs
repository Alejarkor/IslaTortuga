using System.Threading;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Genera identificadores de entidad únicos dentro de una partida y crea las
    /// entidades. La asignación de id es atómica (sin colisiones aunque varios hilos
    /// la pidan a la vez).
    /// </summary>
    public sealed class NetworkEntityManager
    {
        private long _counter;

        public string NewId()
        {
            return "ent_" + Interlocked.Increment(ref _counter);
        }

        public NetworkEntity Create(
            string prefabId,
            Authority authority = Authority.Server,
            string ownerId = null)
        {
            return new NetworkEntity(NewId(), prefabId, authority, ownerId);
        }
    }
}
