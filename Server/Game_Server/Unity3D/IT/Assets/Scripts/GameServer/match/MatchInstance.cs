using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Match
{
    public enum MatchState
    {
        Created = 0,
        Running = 1,
        Stopped = 2
    }

    /// <summary>
    /// Una partida aislada. Cascarón en Fase 1 (id, config, estado), jugadores
    /// conectados en Fase 2, y desde la Fase 3 posee su propio NetworkRuntime: al
    /// arrancar la partida late su mundo a ritmo de tick, independiente del resto.
    /// </summary>
    public sealed class MatchInstance
    {
        private readonly object _gate = new object();
        private readonly List<string> _expectedPlayers;
        private readonly ConcurrentDictionary<string, string> _connected =
            new ConcurrentDictionary<string, string>();

        public string MatchId { get; }
        public MatchConfig Config { get; }
        public DateTime CreatedAtUtc { get; }
        public MatchState State { get; private set; }

        /// <summary>Runtime de red de esta partida (mundo + tick). Puede ser null en tests.</summary>
        public NetworkRuntime Runtime { get; }

        public IReadOnlyList<string> ExpectedPlayers => _expectedPlayers;
        public int ConnectedPlayerCount => _connected.Count;

        public MatchInstance(string matchId, MatchConfig config, NetworkRuntime runtime = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("matchId requerido", nameof(matchId));
            }
            MatchId = matchId;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Runtime = runtime;
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
                    Runtime?.Start();
                }
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                State = MatchState.Stopped;
            }
            Runtime?.Stop();
        }

        public void AddPlayer(string playerId, string sessionId)
        {
            if (!string.IsNullOrEmpty(playerId))
            {
                _connected[playerId] = sessionId;
            }
        }

        public bool RemovePlayer(string playerId)
        {
            return playerId != null && _connected.TryRemove(playerId, out _);
        }

        public bool IsConnected(string playerId)
        {
            return playerId != null && _connected.ContainsKey(playerId);
        }
    }
}
