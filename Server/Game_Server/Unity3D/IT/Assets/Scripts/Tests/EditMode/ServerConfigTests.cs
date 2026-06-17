using NUnit.Framework;
using IslaTortuga.GameServer.Host;

namespace IslaTortuga.GameServer.Tests
{
    public class ServerConfigTests
    {
        [Test]
        public void Default_IsValid()
        {
            var config = ServerConfig.Default();
            Assert.AreEqual(8090, config.ControlPort);
            Assert.Greater(config.TickRate, 0);
            Assert.Greater(config.MaxMatches, 0);
        }

        [Test]
        public void ControlPort_OutOfRange_Throws([Values(0, -1, 65536, 99999)] int badPort)
        {
            Assert.Throws<ServerConfigException>(() =>
                new ServerConfig("localhost", badPort, 9090, 30, 10, 8));
        }

        [Test]
        public void TickRate_NotPositive_Throws([Values(0, -1, -30)] int badTick)
        {
            Assert.Throws<ServerConfigException>(() =>
                new ServerConfig("localhost", 8080, 9090, badTick, 10, 8));
        }

        [Test]
        public void MaxMatches_NotPositive_Throws([Values(0, -5)] int badMax)
        {
            Assert.Throws<ServerConfigException>(() =>
                new ServerConfig("localhost", 8080, 9090, 30, badMax, 8));
        }

        [Test]
        public void MaxPlayersPerMatch_NotPositive_Throws()
        {
            Assert.Throws<ServerConfigException>(() =>
                new ServerConfig("localhost", 8080, 9090, 30, 10, 0));
        }

        [Test]
        public void ControlAndGatewaySamePort_Throws()
        {
            Assert.Throws<ServerConfigException>(() =>
                new ServerConfig("localhost", 8080, 8080, 30, 10, 8));
        }

        [Test]
        public void EmptyControlHost_Throws()
        {
            Assert.Throws<ServerConfigException>(() =>
                new ServerConfig("  ", 8080, 9090, 30, 10, 8));
        }

        [Test]
        public void FromEnvironment_UsesValuesAndFallsBackToDefaults()
        {
            var env = new System.Collections.Generic.Dictionary<string, string>
            {
                { "GS_CONTROL_PORT", "7000" },
                { "GS_TICK_RATE", "60" }
                // el resto cae a defaults
            };

            var config = ServerConfig.FromEnvironment(key => env.TryGetValue(key, out var v) ? v : null);

            Assert.AreEqual(7000, config.ControlPort);
            Assert.AreEqual(60, config.TickRate);
            Assert.AreEqual(ServerConfig.Default().MaxMatches, config.MaxMatches);
        }

        [Test]
        public void FromEnvironment_NonNumericValue_Throws()
        {
            var env = new System.Collections.Generic.Dictionary<string, string>
            {
                { "GS_CONTROL_PORT", "no-soy-un-numero" }
            };

            Assert.Throws<ServerConfigException>(() =>
                ServerConfig.FromEnvironment(key => env.TryGetValue(key, out var v) ? v : null));
        }
    }
}
