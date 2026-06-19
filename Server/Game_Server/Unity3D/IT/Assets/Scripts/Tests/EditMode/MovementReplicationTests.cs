using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using NUnit.Framework;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Tests
{
    public class InputSystemTests
    {
        [Test]
        public void GuardaElUltimo_Y_RespetaElOrdenPorSeq()
        {
            var inp = new InputSystem();
            inp.SetInput("p1", 5, 1f, 0f);
            inp.SetInput("p1", 3, -1f, 0f); // seq menor: se descarta

            Assert.AreEqual(5, inp.Get("p1").Seq);
            Assert.AreEqual(1f, inp.Get("p1").MoveX);

            inp.SetInput("p1", 6, 0f, 1f);
            Assert.AreEqual(6, inp.Get("p1").Seq);
            Assert.AreEqual(1f, inp.Get("p1").MoveZ);
        }
    }

    public class MovementSystemTests
    {
        [Test]
        public void Mueve_AlOwner_Recalculando_IgnorandoCliente()
        {
            var world = new NetworkWorld();
            var inputs = new InputSystem();
            var e = new NetworkEntity("ent_1", "player_default", Authority.Owner, "p1");
            world.Add(e); // el servidor parte de su posición (0,0,0); el cliente no envía posición
            inputs.SetInput("p1", 1, 1f, 0f);

            new MovementSystem { Speed = 10f }.Apply(world, inputs, 0.1f); // +1 en X

            Assert.AreEqual(1f, e.Position.X, 1e-3f);
            Assert.AreEqual(0f, e.Position.Z, 1e-3f);
        }

        [Test]
        public void NoMueve_Entidades_DeAutoridadServer()
        {
            var world = new NetworkWorld();
            var inputs = new InputSystem();
            var e = new NetworkEntity("ent_1", "chest", Authority.Server, "p1");
            world.Add(e);
            inputs.SetInput("p1", 1, 1f, 1f);

            new MovementSystem { Speed = 10f }.Apply(world, inputs, 0.1f);

            Assert.AreEqual(Vector3.Zero, e.Position);
        }

        [Test]
        public void Diagonal_SeNormaliza()
        {
            var world = new NetworkWorld();
            var inputs = new InputSystem();
            var e = new NetworkEntity("ent_1", "player_default", Authority.Owner, "p1");
            world.Add(e);
            inputs.SetInput("p1", 1, 1f, 1f);

            new MovementSystem { Speed = 10f }.Apply(world, inputs, 0.1f);

            var dist = (float)System.Math.Sqrt(e.Position.X * e.Position.X + e.Position.Z * e.Position.Z);
            Assert.AreEqual(1f, dist, 1e-3f); // 10 * 0.1, no 10*0.1*sqrt(2)
        }
    }

    public class ReplicationSystemTests
    {
        [Test]
        public void Delta_SoloIncluye_EntidadesQueCambiaron()
        {
            var world = new NetworkWorld();
            var a = new NetworkEntity("a", "p");
            var b = new NetworkEntity("b", "p");
            world.Add(a);
            world.Add(b);
            var rep = new ReplicationSystem();

            var d1 = rep.BuildDelta(world, 1); // primera vez: ambas son "nuevas"
            Assert.IsNotNull(d1);
            StringAssert.Contains("STATE_DELTA", d1);
            StringAssert.Contains("\"a\"", d1);
            StringAssert.Contains("\"b\"", d1);

            Assert.IsNull(rep.BuildDelta(world, 2)); // nada cambió

            a.Position = new Vector3(5, 0, 0);
            var d3 = rep.BuildDelta(world, 3);
            Assert.IsNotNull(d3);
            StringAssert.Contains("\"a\"", d3);
            StringAssert.DoesNotContain("\"b\"", d3);
        }
    }

    public class RuntimeMovementTests
    {
        [Test]
        public void Runtime_MueveAlOwner_Y_ReplicaDeltas()
        {
            var rt = new NetworkRuntime(50);
            var deltas = new List<string>();
            rt.Broadcaster = m => { lock (deltas) { deltas.Add(m); } };

            var e = rt.Spawn.SpawnPlayer("p1"); // OWNER en el origen
            rt.Input.SetInput("p1", 1, 1f, 0f);

            rt.Start();
            Thread.Sleep(250);
            rt.Stop();

            Assert.Greater(e.Position.X, 0f, "el owner debería haberse movido en X");
            lock (deltas)
            {
                Assert.IsTrue(deltas.Count > 0, "debería haberse emitido algún STATE_DELTA");
            }
        }
    }
}
