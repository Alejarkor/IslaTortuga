namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>Destruye entidades de red del mundo por su id.</summary>
    public sealed class DespawnSystem
    {
        private readonly NetworkWorld _world;

        public DespawnSystem(NetworkWorld world)
        {
            _world = world;
        }

        /// <summary>Quita la entidad del mundo. Devuelve true si existía.</summary>
        public bool Despawn(string entityId)
        {
            return _world.Remove(entityId);
        }
    }
}
