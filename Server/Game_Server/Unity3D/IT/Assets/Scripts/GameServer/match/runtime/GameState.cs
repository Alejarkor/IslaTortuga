namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Estado de partida que ven las reglas en cada tick: el mundo de entidades y el
    /// número de tick actual. En fases posteriores colgarán de aquí más cosas
    /// (cola de inputs, eventos pendientes, etc.).
    /// </summary>
    public sealed class GameState
    {
        public NetworkWorld World { get; }
        public long Tick { get; set; }

        public GameState(NetworkWorld world)
        {
            World = world;
        }
    }
}
