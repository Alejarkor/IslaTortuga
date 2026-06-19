using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using IslaTortuga.GameServer.Runtime;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Tests
{
    public class SpawnSystemTests
    {
        private static (SpawnSystem spawn, DespawnSystem despawn, NetworkWorld world) Build()
        {
            var world = new NetworkWorld();
            var entities = new NetworkEntityManager();
            var prefabs = new NetworkPrefabRegistry(new[] { "chest_wood_01", SpawnSystem.PlayerPrefab });
            return (new SpawnSystem(world, entities, prefabs), new DespawnSystem(world), world);
        }

        [Test]
        public void SpawnEntity_AsignaIdUnico_Estado_Y_Autoridad()
        {
            var (spawn, _, world) = Build();
            var state = new Dictionary<string, object> { { "opened", false }, { "locked", true } };

            var e = spawn.SpawnEntity("chest_wood_01", new Vector3(120, 0, 80), Quaternion.Identity, state, Authority.Server);

            Assert.IsNotNull(e.Id);
            Assert.AreEqual("chest_wood_01", e.PrefabId);
            Assert.AreEqual(Authority.Server, e.Authority);
            Assert.AreEqual(false, e.State["opened"]);
            Assert.AreEqual(80f, e.Position.Z);
            Assert.IsTrue(world.Contains(e.Id));

            var e2 = spawn.SpawnEntity("chest_wood_01", Vector3.Zero, Quaternion.Identity);
            Assert.AreNotEqual(e.Id, e2.Id);
        }

        [Test]
        public void SpawnPlayer_CreaConOwner_Y_OwnerId()
        {
            var (spawn, _, _) = Build();
            var e = spawn.SpawnPlayer("player_7");

            Assert.AreEqual(Authority.Owner, e.Authority);
            Assert.AreEqual("player_7", e.OwnerId);
            Assert.AreEqual(SpawnSystem.PlayerPrefab, e.PrefabId);
        }

        [Test]
        public void Despawn_EliminaDelMundo()
        {
            var (spawn, despawn, world) = Build();
            var e = spawn.SpawnPlayer("p1");
            Assert.IsTrue(world.Contains(e.Id));

            Assert.IsTrue(despawn.Despawn(e.Id));
            Assert.IsFalse(world.Contains(e.Id));
            Assert.IsFalse(despawn.Despawn(e.Id));
        }

        [Test]
        public void MensajeSpawn_LlevaPrefabIdsPosicion3D_Y_NingunBinario()
        {
            var e = new NetworkEntity("ent_9", "chest_wood_01", Authority.Server)
            {
                Position = new Vector3(120, 1, 80)
            };
            e.State["opened"] = false;

            var json = NetworkMessages.SpawnEntity(e);

            StringAssert.Contains("\"type\":\"SPAWN_ENTITY\"", json);
            StringAssert.Contains("\"networkPrefabId\":\"chest_wood_01\"", json);
            StringAssert.Contains("\"networkEntityId\":\"ent_9\"", json);
            StringAssert.Contains("\"z\":80", json);   // 3D
            StringAssert.Contains("\"w\":1", json);     // cuaternión identidad
            StringAssert.DoesNotContain(".glb", json);
            StringAssert.DoesNotContain("data:", json);
        }
    }
}
