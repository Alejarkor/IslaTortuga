using System;
using System.Collections.Generic;

namespace IslaTortuga.GameServer.Match
{
    public enum MatchState
    {
        Created = 0,
        Running = 1,
        Stopped = 2
    }

    /// <summary>
    /// Una partida aislada. En la Fase 1 es un cascarón: existe, tiene id único, su
    /// configuración y su lista de jugadores esperados, y un estado. Todavía sin
    /// realtime ni simulación (eso entra en las Fases 2 y 3, donde se le añadirán
    /// addPlayer/removePlayer y el NetworkRuntime). Lo importante ahora es que sea
    /// recuperable por id y que represente "una partida instanciada".
    /// </summary>
    public sealed class MatchInstance
    {
        private readonly object _gate = new object();
        private readonly List<string> _expectedPlayers;

        public string MatchId { get; }
        public MatchConfig Config { get; }
        public DateTime CreatedAtUtc { get; }
        public MatchState State { get; private set; }

        public IReadOnlyList<string> ExpectedPlayers => _expectedPlayers;

        public MatchInstance(string matchId, MatchConfig config)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("matchId requerido", nameof(matchId));
            }
            MatchId = matchId;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _expectedPlayers = new List<string>(config.Players);
            CreatedAtUtc = DateTime.UtcNow;
            State = MatchState.Created;
        }

        public void Start()
        {
            lock (_gate)
            {
                if (State == MatchState.Created)
                {
                    State = MatchState.Running;
                }
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                State = MatchState.Stopped;
            }
        }
    }
}
