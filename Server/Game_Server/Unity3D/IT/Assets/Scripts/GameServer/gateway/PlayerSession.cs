namespace IslaTortuga.GameServer.Gateway
{
    public enum SessionState
    {
        Connecting = 0,
        Connected = 1,
        Disconnected = 2
    }

    /// <summary>
    /// Una sesión de jugador ligada a una partida. Vive mientras el WebSocket esté
    /// abierto. Guarda identidad, partida, transporte, estado del handshake y, desde
    /// la Fase 4, el id de la entidad de red del jugador (para despawnearla al salir).
    /// </summary>
    public sealed class PlayerSession
    {
        public string SessionId { get; }
        public string PlayerId { get; }
        public string MatchId { get; }
        public ITransport Transport { get; }
        public SessionState State { get; set; }

        /// <summary>Entidad de red del jugador (asignada al spawnear tras el handshake).</summary>
        public string EntityId { get; set; }

        public PlayerSession(string sessionId, string playerId, string matchId, ITransport transport)
        {
            SessionId = sessionId;
            PlayerId = playerId;
            MatchId = matchId;
            Transport = transport;
            State = SessionState.Connecting;
        }
    }
}
