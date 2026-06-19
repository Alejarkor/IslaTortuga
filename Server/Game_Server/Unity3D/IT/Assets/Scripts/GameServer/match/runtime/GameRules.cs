namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Punto de extensión de la lógica de juego: se ejecuta una vez por tick. En la
    /// Fase 3 la implementación por defecto no hace nada (solo late el mundo); la
    /// jugabilidad real (movimiento, etc.) entra como sistemas en la Fase 5.
    /// </summary>
    public interface IGameRules
    {
        void OnTick(GameState state, long tick);
    }

    public sealed class NoOpGameRules : IGameRules
    {
        public void OnTick(GameState state, long tick)
        {
        }
    }
}
