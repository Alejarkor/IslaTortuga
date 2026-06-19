using System;
using System.Threading;
using NUnit.Framework;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Tests
{
    public class SimulationLoopTests
    {
        [Test]
        public void Tickea_Y_ElContadorIncrementa()
        {
            int count = 0;
            var loop = new SimulationLoop(50, _ => Interlocked.Increment(ref count));
            loop.Start();
            Thread.Sleep(300);
            loop.Stop();

            Assert.Greater(loop.CurrentTick, 2, "debería haber ticado varias veces");
            Assert.Greater(count, 2);
        }

        [Test]
        public void Stop_DetieneElBucle()
        {
            var loop = new SimulationLoop(50, _ => { });
            loop.Start();
            Thread.Sleep(150);
            loop.Stop();

            var atStop = loop.CurrentTick;
            Thread.Sleep(150);

            Assert.AreEqual(atStop, loop.CurrentTick, "no debería seguir ticando tras Stop");
            Assert.IsFalse(loop.IsRunning);
        }

        [Test]
        public void TickRateInvalido_Lanza()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationLoop(0, _ => { }));
        }
    }
}
