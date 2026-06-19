using System;
using System.Diagnostics;
using System.Threading;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// El latido de una partida: un bucle en su PROPIO hilo de fondo que invoca el
    /// callback de simulación a ritmo fijo (tickRate Hz), con reloj de alta
    /// resolución. Cada partida tiene el suyo, así que los ticks son independientes
    /// y aislados entre partidas. Si se atrasa, reancla para no acumular deuda.
    /// </summary>
    public sealed class SimulationLoop
    {
        private readonly int _tickRate;
        private readonly Action<long> _onTick;
        private readonly IServerLogger _logger;
        private readonly object _gate = new object();

        private Thread _thread;
        private volatile bool _running;
        private long _currentTick;

        public long CurrentTick => Interlocked.Read(ref _currentTick);
        public bool IsRunning => _running;
        public int TickRate => _tickRate;

        public SimulationLoop(int tickRate, Action<long> onTick, IServerLogger logger = null)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), "tickRate debe ser > 0.");
            }
            _tickRate = tickRate;
            _onTick = onTick ?? throw new ArgumentNullException(nameof(onTick));
            _logger = logger;
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_running)
                {
                    return;
                }
                _running = true;
                _thread = new Thread(RunLoop)
                {
                    IsBackground = true,
                    Name = "match-tick"
                };
                _thread.Start();
            }
        }

        public void Stop()
        {
            Thread thread;
            lock (_gate)
            {
                if (!_running)
                {
                    return;
                }
                _running = false;
                thread = _thread;
                _thread = null;
            }
            thread?.Join(TimeSpan.FromSeconds(2));
        }

        private void RunLoop()
        {
            var interval = TimeSpan.FromSeconds(1.0 / _tickRate);
            var sw = Stopwatch.StartNew();
            var next = sw.Elapsed;

            while (_running)
            {
                try
                {
                    _onTick(Interlocked.Read(ref _currentTick));
                }
                catch (Exception ex)
                {
                    _logger?.Error("Error en el tick de simulación.", ex);
                }

                Interlocked.Increment(ref _currentTick);

                next += interval;
                var delay = next - sw.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    var ms = (int)delay.TotalMilliseconds;
                    if (ms > 0)
                    {
                        Thread.Sleep(ms);
                    }
                }
                else
                {
                    // Vamos atrasados: reanclamos al ahora para no entrar en espiral.
                    next = sw.Elapsed;
                }
            }
        }
    }
}
