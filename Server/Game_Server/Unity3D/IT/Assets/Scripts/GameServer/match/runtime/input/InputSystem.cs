using System.Collections.Concurrent;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>Intención de movimiento que envía un cliente (nunca posición).</summary>
    public sealed class PlayerInput
    {
        public long Seq { get; }
        public float MoveX { get; }
        public float MoveZ { get; }

        public PlayerInput(long seq, float moveX, float moveZ)
        {
            Seq = seq;
            MoveX = moveX;
            MoveZ = moveZ;
        }
    }

    /// <summary>
    /// Buffer de inputs por jugador. Guarda el último input válido, respetando el
    /// orden por seq (descarta llegadas fuera de orden). El servidor solo acepta
    /// intención; la posición la decide el MovementSystem.
    /// </summary>
    public sealed class InputSystem
    {
        private readonly ConcurrentDictionary<string, PlayerInput> _latest =
            new ConcurrentDictionary<string, PlayerInput>();

        public void SetInput(string playerId, long seq, float moveX, float moveZ)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            var next = new PlayerInput(seq, moveX, moveZ);
            _latest.AddOrUpdate(
                playerId,
                next,
                (_, prev) => seq >= prev.Seq ? next : prev);
        }

        public PlayerInput Get(string playerId)
        {
            return playerId != null && _latest.TryGetValue(playerId, out var i) ? i : null;
        }

        public void Clear(string playerId)
        {
            if (playerId != null) _latest.TryRemove(playerId, out _);
        }
    }
}
