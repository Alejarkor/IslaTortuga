using NUnit.Framework;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Match;
using System.Collections.Generic;

namespace IslaTortuga.GameServer.Tests
{
    public class MatchOrchestratorTests
    {
        private static MatchOrchestrator Build(int maxMatches, out CapacityManager capacity)
        {
            var config = new ServerConfig("localhost", 8080, 9090, 30, maxMatches, 8);
            var metrics = new MetricsRegistry();
            capacity = new CapacityManager(config, metrics);
            return new MatchOrchestrator(capacity, new CapturingLogger(), metrics);
        }

        private static MatchConfig SampleConfig()
        {
            return new MatchConfig(8, "beach_map_01", new List<string> { "p1", "p2" });
        }

        [Test]
        public void CreateMatch_ReservesCapacity_AndIsRetrievableById()
        {
            var orch = Build(4, out var capacity);

            var match = orch.CreateMatch(SampleConfig());

            Assert.IsNotNull(match);
            Assert.AreEqual(1, capacity.ActiveMatches);
            Assert.AreEqual(1, orch.ActiveMatchCount);
            Assert.AreSame(match, orch.GetMatch(match.MatchId));
            Assert.AreEqual(MatchState.Running, match.State);
            CollectionAssert.AreEqual(new[] { "p1", "p2" }, match.ExpectedPlayers);
        }

        [Test]
        public void CreateMatch_ReturnsNull_WhenFull()
        {
            var orch = Build(1, out _);

            Assert.IsNotNull(orch.CreateMatch(SampleConfig()));
            Assert.IsNull(orch.CreateMatch(SampleConfig())); // sin capacidad
        }

        [Test]
        public void CreateMatch_GeneratesUniqueIds()
        {
            var orch = Build(4, out _);

            var a = orch.CreateMatch(SampleConfig());
            var b = orch.CreateMatch(SampleConfig());

            Assert.AreNotEqual(a.MatchId, b.MatchId);
        }

        [Test]
        public void StopMatch_ReleasesCapacity_AndRemovesInstance()
        {
            var orch = Build(2, out var capacity);
            var match = orch.CreateMatch(SampleConfig());

            var stopped = orch.StopMatch(match.MatchId);

            Assert.IsTrue(stopped);
            Assert.AreEqual(0, capacity.ActiveMatches);
            Assert.IsNull(orch.GetMatch(match.MatchId));
            Assert.AreEqual(MatchState.Stopped, match.State);
        }

        [Test]
        public void StopMatch_UnknownId_ReturnsFalse()
        {
            var orch = Build(2, out _);
            Assert.IsFalse(orch.StopMatch("match_no_existe"));
        }
    }
}
