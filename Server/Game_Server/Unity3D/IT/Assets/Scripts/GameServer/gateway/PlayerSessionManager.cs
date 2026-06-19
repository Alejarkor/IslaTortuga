using System.Collections.Concurrent;
using System.Collections.Generic;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>
    /// Registro de sesiones activas, indexadas por su id de conexión (un id único
    /// por socket). Thread-safe: las conexiones entran y salen concurrentemente.
    /// </summary>
    public sealed class PlayerSessionManager
    {
        private readonly ConcurrentDictionary<string, PlayerSession> _bySessionId =
            new ConcurrentDictionary<string, PlayerSession>();

        public int Count => _bySessionId.Count;

        public void Add(PlayerSession session)
        {
            _bySessionId[session.SessionId] = session;
        }

        /// <summary>Resuelve la sesión a partir del id de su socket/conexión.</summary>
        public PlayerSession Get(string sessionId)
        {
            return _bySessionId.TryGetValue(sessionId, out var s) ? s : null;
        }

        public PlayerSession Remove(string sessionId)
        {
            _bySessionId.TryRemove(sessionId, out var s);
            return s;
        }

        public IReadOnlyCollection<PlayerSession> ForMatch(string matchId)
        {
            var list = new List<PlayerSession>();
            foreach (var s in _bySessionId.Values)
            {
                if (s.MatchId == matchId)
                {
                    list.Add(s);
                }
            }
            return list;
        }
    }
}
