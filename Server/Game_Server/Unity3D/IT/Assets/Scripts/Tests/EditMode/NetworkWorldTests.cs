using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Tests
{
    public class NetworkWorldTests
    {
        [Test]
        public void EntityManager_GeneraIdsUnicosSinColisiones()
        {
            var manager = new NetworkEntityManager();
            var ids = new HashSet<string>();
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsTrue(ids.Add(manager.NewId()), "id duplicado");
            }
            Assert.AreEqual(1000, ids.Count);
        }

        [Test]
        public void World_Add_Get_Remove()
        {
            var world = new NetworkWorld();
            var e = new NetworkEntity("ent_1", "chest_wood_01");

            world.Add(e);
            Assert.AreEqual(1, world.Count);
            Assert.AreSame(e, world.Get("ent_1"));
            Assert.IsTrue(world.Contains("ent_1"));

            Assert.IsTrue(world.Remove("ent_1"));
            Assert.IsNull(world.Get("ent_1"));
            Assert.AreEqual(0, world.Count);
        }

        [Test]
        public void Entity_PorDefecto_RotacionIdentidad_Y_PosicionCero()
        {
            var e = new NetworkEntity("ent_1", "p");
            Assert.AreEqual(Quaternion.Identity, e.Rotation);
            Assert.AreEqual(Vector3.Zero, e.Position);
            Assert.AreEqual(Authority.Server, e.Authority);
        }

        [Test]
        public void Entity_Posicion3D_Y_Cuaternion()
        {
            var e = new NetworkEntity("ent_1", "p")
            {
                Position = new Vector3(1f, 2f, 3f),
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.0f)
            };
            Assert.AreEqual(1f, e.Position.X);
            Assert.AreEqual(2f, e.Position.Y);
            Assert.AreEqual(3f, e.Position.Z);
            Assert.AreNotEqual(Quaternion.Identity, e.Rotation);
        }
    }
}
