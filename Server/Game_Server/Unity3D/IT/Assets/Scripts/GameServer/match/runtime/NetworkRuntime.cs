using System;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Compone el runtime de red de UNA partida: mundo, prefabs, spawn/despawn, input,
    /// movimiento, replicación, estado, reglas y el bucle de tick. El orden del tick es
    /// processInputs -> movimiento -> reglas -> replicación, tal como pide el roadmap.
    /// El servidor es autoritativo: el cliente solo envía intención (input).
    /// </summary>
    public sealed class NetworkRuntime
    {
        public NetworkWorld World { get; }
        public NetworkEntityManager Entities { get; }
        public NetworkPrefabRegistry Prefabs { get; }
        public SpawnSystem Spawn { get; }
        public DespawnSystem Despawn { get; }
        public InputSystem Input { get; }
        public MovementSystem Movement { get; }
        public ReplicationSystem Replication { get; }
        public GameState State { get; }
        public IGameRules Rules { get; }

        /// <summary>
        /// Sumidero de salida: el gateway lo conecta para difundir los STATE_DELTA a
        /// las sesiones de la partida. Si es null, el runtime late igual pero no replica.
        /// </summary>
        public Action<string> Broadcaster { get; set; }

        private readonly SimulationLoop _loop;
        private readonly float _dt;

        public long CurrentTick => _loop.CurrentTick;
        public bool IsRunning => _loop.IsRunning;

        public NetworkRuntime(int tickRate, IServerLogger logger = null, IGameRules rules = null)
        {
            World = new NetworkWorld();
            Entities = new NetworkEntityManager();
            Prefabs = new NetworkPrefabRegistry(new[] { SpawnSystem.PlayerPrefab });
            Spawn = new SpawnSystem(World, Entities, Prefabs, logger);
            Despawn = new DespawnSystem(World);
            Input = new InputSystem();
            Movement = new MovementSystem();
            Replication = new ReplicationSystem();
            State = new GameState(World);
            Rules = rules ?? new NoOpGameRules();
            _dt = 1f / tickRate;
            _loop = new SimulationLoop(tickRate, Tick, logger);
        }

        private void Tick(long tickNumber)
        {
            State.Tick = tickNumber;
            // processInputs + updateSystems
            Movement.Apply(World, Input, _dt);
            Rules.OnTick(State, tickNumber);
            // replication
            var delta = Replication.BuildDelta(World, tickNumber);
            if (delta != null)
            {
                Broadcaster?.Invoke(delta);
            }
        }

        public void Start() => _loop.Start();
        public void Stop() => _loop.Stop();
    }
}
