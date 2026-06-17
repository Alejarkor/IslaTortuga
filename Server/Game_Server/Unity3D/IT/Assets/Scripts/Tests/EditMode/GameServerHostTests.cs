using System;
using System.Net;
using System.Net.Http;
using NUnit.Framework;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Tests
{
    public class GameServerHostTests
    {
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
        public void Start_MakesHealthAndCapacityReachable()
        {
            var port = TestSupport.GetFreeTcpPort();
            var host = new GameServerHost(TestSupport.ConfigWithControlPort(port), new CapturingLogger());

            try
            {
                host.StartAsync().GetAwaiter().GetResult();
                Assert.IsTrue(host.IsRunning);

                var health = Get($"http://localhost:{port}/health");
                Assert.AreEqual(HttpStatusCode.OK, health.Status);
                StringAssert.Contains("\"status\":\"ok\"", health.Body);

                var capacity = Get($"http://localhost:{port}/capacity");
                Assert.AreEqual(HttpStatusCode.OK, capacity.Status);
            }
            finally
            {
                host.ShutdownGracefullyAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ShutdownGracefully_IsIdempotent()
        {
            var port = TestSupport.GetFreeTcpPort();
            var host = new GameServerHost(TestSupport.ConfigWithControlPort(port), new CapturingLogger());
            host.StartAsync().GetAwaiter().GetResult();

            host.ShutdownGracefullyAsync().GetAwaiter().GetResult();
            Assert.IsFalse(host.IsRunning);

            // Segunda llamada: no debe lanzar ni colgarse.
            Assert.DoesNotThrow(() => host.ShutdownGracefullyAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void ShutdownGracefully_FreesPort_SoAnotherHostCanBind()
        {
            var port = TestSupport.GetFreeTcpPort();

            var first = new GameServerHost(TestSupport.ConfigWithControlPort(port), new CapturingLogger());
            first.StartAsync().GetAwaiter().GetResult();
            first.ShutdownGracefullyAsync().GetAwaiter().GetResult();

            // Si el apagado liberó el puerto, este segundo host arranca en el mismo puerto.
            var second = new GameServerHost(TestSupport.ConfigWithControlPort(port), new CapturingLogger());
            try
            {
                Assert.DoesNotThrow(() => second.StartAsync().GetAwaiter().GetResult());

                var health = Get($"http://localhost:{port}/health");
                Assert.AreEqual(HttpStatusCode.OK, health.Status);
            }
            finally
            {
                second.ShutdownGracefullyAsync().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void Start_IsIdempotent()
        {
            var port = TestSupport.GetFreeTcpPort();
            var host = new GameServerHost(TestSupport.ConfigWithControlPort(port), new CapturingLogger());

            try
            {
                host.StartAsync().GetAwaiter().GetResult();
                // Segundo Start no debe lanzar (HttpListener ya estaría escuchando).
                Assert.DoesNotThrow(() => host.StartAsync().GetAwaiter().GetResult());
                Assert.IsTrue(host.IsRunning);
            }
            finally
            {
                host.ShutdownGracefullyAsync().GetAwaiter().GetResult();
            }
        }
    }
}
