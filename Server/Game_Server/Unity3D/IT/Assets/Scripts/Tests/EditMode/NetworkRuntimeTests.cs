using System.Threading;
using NUnit.Framework;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Tests
{
    public class NetworkRuntimeTests
    {
        [Test]
        public void DosRuntimes_TickanDeFormaIndependiente()
        {
            var a = new NetworkRuntime(50);
            var b = new NetworkRuntime(50);
            a.Start();
            b.Start();
            Thread.Sleep(250);
            a.Stop();
            b.Stop();

            Assert.Greater(a.CurrentTick, 2);
            Assert.Greater(b.CurrentTick, 2);
        }

        [Test]
        public void EntidadAniadida_PersisteEntreTicks()
        {
            var r = new NetworkRuntime(50);
            var e = r.Entities.Create("chest_wood_01");
            r.World.Add(e);

            r.Start();
            Thread.Sleep(150);
            r.Stop();

            Assert.IsTrue(r.World.Contains(e.Id), "la entidad debería seguir tras varios ticks");
            Assert.AreEqual(1, r.World.Count);
        }

        [Test]
        public void Stop_DetieneElTick()
        {
            var r = new NetworkRuntime(50);
            r.Start();
            Thread.Sleep(120);
            r.Stop();

            var atStop = r.CurrentTick;
            Thread.Sleep(120);
            Assert.AreEqual(atStop, r.CurrentTick);
            Assert.IsFalse(r.IsRunning);
        }

        [Test]
        public void GameState_ReflejaElNumeroDeTick()
        {
            var r = new NetworkRuntime(50);
            r.Start();
            Thread.Sleep(150);
            r.Stop();

            Assert.Greater(r.State.Tick, 0);
        }
    }
}
