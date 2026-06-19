using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Match;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Tests
{
    /// <summary>
    /// Prueba el handshake del gateway a través de HandleSessionAsync con un
    /// transporte en memoria (sin WebSocket real: el ClientWebSocket de Mono no
    /// conecta en el editor, pero la lógica es la misma sobre ITransport).
    /// </summary>
    public class PlayerGatewayTests
    {
        private ServerConfig _config;
        private MetricsRegistry _metrics;
        private MatchOrchestrator _orch;
        private MatchInstance _match;
        private PlayerSessionManager _sessions;

        [SetUp]
        public void SetUp()
        {
            _config = TestSupport.ConfigWithControlPort(TestSupport.GetFreeTcpPort());
            _metrics = new MetricsRegistry();
            var capacity = new CapacityManager(_config, _metrics);
            _orch = new MatchOrchestrator(capacity, new CapturingLogger(), _metrics);
            _match = _orch.CreateMatch(new MatchConfig(8, "beach_map_01", new List<string> { "p1" }));
            _sessions = new PlayerSessionManager();
        }

        private PlayerGateway Gateway(ValidatedTicket ticket)
        {
            return new PlayerGateway(
                _config, _orch, new FakeTicketValidator(ticket), _sessions, new CapturingLogger(), _metrics);
        }

        private static bool WaitFor(Func<bool> cond, int ms = 2000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                if (cond()) return true;
                Thread.Sleep(15);
            }
            return cond();
        }

        private bool SessionConnected()
        {
            foreach (var s in _sessions.ForMatch(_match.MatchId))
            {
                if (s.State == SessionState.Connected) return true;
            }
            return false;
        }

        [Test]
        public void TicketValido_RecibeWelcome_Handshake_YDesconexion()
        {
            var gateway = Gateway(new ValidatedTicket("t1", _match.MatchId, "p1"));
            var transport = new FakeTransport();

            var task = gateway.HandleSessionAsync(transport, "t1");

            Assert.IsTrue(WaitFor(() => transport.Sent.Count >= 1), "no se envió MATCH_WELCOME");
            var welcome = MessageCodec.Decode(transport.Sent[0]);
            Assert.AreEqual(PlayerGateway.MsgMatchWelcome, welcome.Type);
            Assert.AreEqual(_match.MatchId, welcome.Payload["matchId"]);
            Assert.AreEqual(1, _match.ConnectedPlayerCount);

            transport.QueueIncoming(MessageCodec.Encode(PlayerGateway.MsgClientReady));
            Assert.IsTrue(WaitFor(SessionConnected), "el handshake no completó (sesión no quedó Connected)");

            transport.SimulateClientClose();
            task.GetAwaiter().GetResult();

            Assert.AreEqual(0, _match.ConnectedPlayerCount);
            Assert.AreEqual(0, _sessions.Count);
            Assert.IsTrue(transport.Closed);
        }

        [Test]
        public void TicketInvalido_CierraSinWelcome()
        {
            var gateway = Gateway(null); // validador devuelve null
            var transport = new FakeTransport();

            gateway.HandleSessionAsync(transport, "t1").GetAwaiter().GetResult();

            Assert.IsTrue(transport.Closed);
            Assert.AreEqual(0, transport.Sent.Count);
            Assert.AreEqual(0, _match.ConnectedPlayerCount);
        }

        [Test]
        public void PartidaInexistente_CierraLaConexion()
        {
            var gateway = Gateway(new ValidatedTicket("t1", "match_no_existe", "p1"));
            var transport = new FakeTransport();

            gateway.HandleSessionAsync(transport, "t1").GetAwaiter().GetResult();

            Assert.IsTrue(transport.Closed);
            Assert.AreEqual(0, transport.Sent.Count);
            Assert.AreEqual(0, _sessions.Count);
        }
    }
}
