using System;
using System.Net;
using System.Net.Http;
using NUnit.Framework;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Control;

namespace IslaTortuga.GameServer.Tests
{
    public class ControlApiTests
    {
        private ControlApi _api;
        private int _port;

        [SetUp]
        public void SetUp()
        {
            _port = TestSupport.GetFreeTcpPort();
        }

        [TearDown]
        public void TearDown()
        {
            _api?.StopAsync().GetAwaiter().GetResult();
            _api = null;
        }

        private CapacityManager StartApi(int maxMatches = 4)
        {
            var config = TestSupport.ConfigWithControlPort(_port, maxMatches);
            var metrics = new MetricsRegistry();
            var capacity = new CapacityManager(config, metrics);
            _api = new ControlApi(config, capacity, new CapturingLogger());
            _api.Start();
            return capacity;
        }

        private static (HttpStatusCode Status, string Body) Get(string url)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return (response.StatusCode, body);
            }
        }

        [Test]
        public void Health_Returns200_WithStatusOk()
        {
            StartApi();

            var (status, body) = Get($"http://localhost:{_port}/health");

            Assert.AreEqual(HttpStatusCode.OK, status);
            StringAssert.Contains("\"status\":\"ok\"", body);
            StringAssert.Contains("\"service\":\"game-server\"", body);
            StringAssert.Contains("uptimeSeconds", body);
        }

        [Test]
        public void Capacity_ReflectsCapacityManagerState()
        {
            var capacity = StartApi(maxMatches: 4);
            capacity.TryReserveMatch(); // 1 de 4 activa

            var (status, body) = Get($"http://localhost:{_port}/capacity");

            Assert.AreEqual(HttpStatusCode.OK, status);
            StringAssert.Contains("\"activeMatches\":1", body);
            StringAssert.Contains("\"maxMatches\":4", body);
            StringAssert.Contains("\"availableSlots\":3", body);
            StringAssert.Contains("\"canAcceptMatch\":true", body);
        }

        [Test]
        public void Capacity_ReportsFull_WhenNoSlotsLeft()
        {
            var capacity = StartApi(maxMatches: 1);
            capacity.TryReserveMatch(); // lleno

            var (status, body) = Get($"http://localhost:{_port}/capacity");

            Assert.AreEqual(HttpStatusCode.OK, status);
            StringAssert.Contains("\"canAcceptMatch\":false", body);
            StringAssert.Contains("\"availableSlots\":0", body);
        }

        [Test]
        public void UnknownRoute_Returns404()
        {
            StartApi();

            var (status, _) = Get($"http://localhost:{_port}/no-existe");

            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }
    }
}
