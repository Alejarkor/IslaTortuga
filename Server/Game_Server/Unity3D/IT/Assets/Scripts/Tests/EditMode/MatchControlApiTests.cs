using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using IslaTortuga.GameServer.Host;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Match;

namespace IslaTortuga.GameServer.Tests
{
    public class MatchControlApiTests
    {
        private ControlApi _api;
        private CapacityManager _capacity;
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

        private MatchOrchestrator StartApi(int maxMatches = 4)
        {
            var config = TestSupport.ConfigWithControlPort(_port, maxMatches);
            var metrics = new MetricsRegistry();
            _capacity = new CapacityManager(config, metrics);
            var orchestrator = new MatchOrchestrator(_capacity, new CapturingLogger(), metrics);
            _api = new ControlApi(config, _capacity, new CapturingLogger(), null, orchestrator);
            _api.Start();
            return orchestrator;
        }

        private static (HttpStatusCode Status, string Body) Post(string url, string json)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = client.PostAsync(url, content).GetAwaiter().GetResult();
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return (response.StatusCode, body);
            }
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

        private static string ExtractMatchId(string body)
        {
            var match = Regex.Match(body, "\"matchId\":\"(match_[a-f0-9]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        [Test]
        public void CreateMatch_Returns200_WithMatchId_AndConsumesCapacity()
        {
            StartApi(maxMatches: 4);

            var (status, body) = Post(
                $"http://localhost:{_port}/control/create-match",
                "{\"maxPlayers\":4,\"mapId\":\"beach_map_01\",\"players\":[\"p1\",\"p2\"]}");

            Assert.AreEqual(HttpStatusCode.OK, status);
            StringAssert.Contains("\"ok\":true", body);
            Assert.IsNotNull(ExtractMatchId(body), "respuesta sin matchId");
            Assert.AreEqual(1, _capacity.ActiveMatches);

            var capacity = Get($"http://localhost:{_port}/capacity");
            StringAssert.Contains("\"activeMatches\":1", capacity.Body);
        }

        [Test]
        public void StopMatch_Returns200_AndFreesCapacity()
        {
            StartApi(maxMatches: 4);

            var created = Post(
                $"http://localhost:{_port}/control/create-match",
                "{\"maxPlayers\":4,\"mapId\":\"beach\",\"players\":[\"p1\"]}");
            var matchId = ExtractMatchId(created.Body);

            var (status, body) = Post(
                $"http://localhost:{_port}/control/stop-match",
                $"{{\"matchId\":\"{matchId}\"}}");

            Assert.AreEqual(HttpStatusCode.OK, status);
            StringAssert.Contains("\"ok\":true", body);
            Assert.AreEqual(0, _capacity.ActiveMatches);
        }

        [Test]
        public void StopMatch_UnknownId_Returns404()
        {
            StartApi();

            var (status, _) = Post(
                $"http://localhost:{_port}/control/stop-match",
                "{\"matchId\":\"match_no_existe\"}");

            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [Test]
        public void CreateMatch_Returns409_WhenNoCapacity()
        {
            StartApi(maxMatches: 1);

            var first = Post(
                $"http://localhost:{_port}/control/create-match",
                "{\"maxPlayers\":4,\"mapId\":\"beach\",\"players\":[\"p1\"]}");
            Assert.AreEqual(HttpStatusCode.OK, first.Status);

            var (status, body) = Post(
                $"http://localhost:{_port}/control/create-match",
                "{\"maxPlayers\":4,\"mapId\":\"beach\",\"players\":[\"p2\"]}");

            Assert.AreEqual(HttpStatusCode.Conflict, status);
            StringAssert.Contains("no capacity", body);
        }

        [Test]
        public void CreateMatch_InvalidJson_Returns400()
        {
            StartApi();

            var (status, _) = Post(
                $"http://localhost:{_port}/control/create-match",
                "esto no es json");

            Assert.AreEqual(HttpStatusCode.BadRequest, status);
        }
    }
}
