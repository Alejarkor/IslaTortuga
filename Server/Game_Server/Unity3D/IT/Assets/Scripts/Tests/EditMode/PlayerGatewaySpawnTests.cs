using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Match;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Tests
{
    /// <summary>Validador que mapea ticketId -> jugador (para simular varios jugadores).</summary>
    internal sealed class MapTicketValidator : ITicketValidator
    {
        private readonly Dictionary<string, ValidatedTicket> _map = new Dictionary<string, ValidatedTicket>();
        public void Set(string ticketId, ValidatedTicket t) => _map[ticketId] = t;
        public Task<ValidatedTicket> ValidateAndConsumeAsync(string ticketId)
        {
            return Task.FromResult(_map.TryGetValue(ticketId, out var t) ? t : null);
        }
    }

    public class PlayerGatewaySpawnTests
    {
        private MatchOrchestrator _orch;
        private MatchInstance _match;
        private PlayerSessionManager _sessions;
        private PlayerGateway _gateway;
        private MapTicketValidator _validator;

        [SetUp]
        public void SetUp()
        {
            var config = TestSupport.ConfigWithControlPort(TestSupport.GetFreeTcpPort());
            var metrics = new MetricsRegistry();
            var capacity = new CapacityManager(config, metrics);
            _orch = new MatchOrchestrator(capacity, new CapturingLogger(), metrics, 30); // tickRate>0 => runtime real
            _match = _orch.CreateMatch(new MatchConfig(8, "beach_map_01", new List<string> { "p1", "p2" }));
            _sessions = new PlayerSessionManager();
            _validator = new MapTicketValidator();
            _validator.Set("tA", new ValidatedTicket("tA", _match.MatchId, "p1"));
            _validator.Set("tB", new ValidatedTicket("tB", _match.MatchId, "p2"));
            _gateway = new PlayerGateway(config, _orch, _validator, _sessions, new CapturingLogger(), metrics);
        }

        [TearDown]
        public void TearDown()
        {
            _orch.StopMatch(_match.MatchId);
        }

        private static bool WaitFor(Func<bool> cond, int ms = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                if (cond()) return true;
                Thread.Sleep(20);
            }
            return cond();
        }

        private void CompleteHandshake(FakeTransport t)
        {
            WaitFor(() => t.Sent.Any(m => m.Contains(PlayerGateway.MsgMatchWelcome)));
            t.QueueIncoming(MessageCodec.Encode(PlayerGateway.MsgClientReady));
        }

        [Test]
        public void AlConectar_SeSpawnea_SnapshotYBroadcast_Y_DespawnAlSalir()
        {
            var a = new FakeTransport();
            var taskA = _gateway.HandleSessionAsync(a, "tA");
            CompleteHandshake(a);

            // A recibe su propio spawn (snapshot con su entidad, ownerId p1).
            Assert.IsTrue(
                WaitFor(() => a.Sent.Any(m => m.Contains("SPAWN_ENTITY") && m.Contains("\"ownerId\":\"p1\""))),
                "A no recibió el spawn de su entidad");

            var b = new FakeTransport();
            var taskB = _gateway.HandleSessionAsync(b, "tB");
            CompleteHandshake(b);

            // B (que entra después) recibe en su snapshot a AMBOS jugadores.
            Assert.IsTrue(
                WaitFor(() => b.Sent.Any(m => m.Contains("\"ownerId\":\"p1\"")) &&
                              b.Sent.Any(m => m.Contains("\"ownerId\":\"p2\""))),
                "B no recibió el snapshot con ambos jugadores");

            // A es notificado del alta de B (broadcast).
            Assert.IsTrue(
                WaitFor(() => a.Sent.Any(m => m.Contains("SPAWN_ENTITY") && m.Contains("\"ownerId\":\"p2\""))),
                "A no fue notificado del spawn de B");

            // A se desconecta -> B recibe un DESPAWN_ENTITY.
            a.SimulateClientClose();
            taskA.GetAwaiter().GetResult();
            Assert.IsTrue(
                WaitFor(() => b.Sent.Any(m => m.Contains("DESPAWN_ENTITY"))),
                "B no recibió el despawn de A");

            b.SimulateClientClose();
            taskB.GetAwaiter().GetResult();
        }
    }
}
