using NUnit.Framework;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Control;

namespace IslaTortuga.GameServer.Tests
{
    public class CapacityManagerTests
    {
        private static CapacityManager Build(int maxMatches, out MetricsRegistry metrics)
        {
            var config = new ServerConfig("localhost", 8080, 9090, 30, maxMatches, 8);
            metrics = new MetricsRegistry();
            return new CapacityManager(config, metrics);
        }

        [Test]
        public void CanAcceptMatch_ReflectsConfiguredLimit()
        {
            var capacity = Build(2, out _);

            Assert.IsTrue(capacity.CanAcceptMatch());

            Assert.IsTrue(capacity.TryReserveMatch());
            Assert.IsTrue(capacity.CanAcceptMatch()); // 1 de 2

            Assert.IsTrue(capacity.TryReserveMatch());
            Assert.IsFalse(capacity.CanAcceptMatch()); // 2 de 2, lleno
        }

        [Test]
        public void TryReserveMatch_FailsWhenFull()
        {
            var capacity = Build(1, out _);

            Assert.IsTrue(capacity.TryReserveMatch());
            Assert.IsFalse(capacity.TryReserveMatch());
            Assert.AreEqual(1, capacity.ActiveMatches);
        }

        [Test]
        public void ReleaseMatch_FreesSlot_AndNeverGoesNegative()
        {
            var capacity = Build(1, out _);

            capacity.ReleaseMatch(); // sin reservas: se queda en 0
            Assert.AreEqual(0, capacity.ActiveMatches);

            Assert.IsTrue(capacity.TryReserveMatch());
            capacity.ReleaseMatch();
            Assert.AreEqual(0, capacity.ActiveMatches);
            Assert.IsTrue(capacity.CanAcceptMatch());
        }

        [Test]
        public void Snapshot_ReportsConsistentNumbers()
        {
            var capacity = Build(3, out _);
            capacity.TryReserveMatch();

            var snap = capacity.Snapshot();

            Assert.AreEqual(1, snap.ActiveMatches);
            Assert.AreEqual(3, snap.MaxMatches);
            Assert.AreEqual(2, snap.AvailableSlots);
            Assert.IsTrue(snap.CanAcceptMatch);
        }

        [Test]
        public void PublishesActiveMatchesGauge()
        {
            var capacity = Build(3, out var metrics);
            capacity.TryReserveMatch();
            capacity.TryReserveMatch();

            Assert.AreEqual(2d, metrics.GetGauge(CapacityManager.ActiveMatchesGauge));
        }
    }
}
