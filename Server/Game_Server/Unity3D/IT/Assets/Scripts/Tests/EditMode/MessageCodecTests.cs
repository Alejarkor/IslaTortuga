using NUnit.Framework;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Tests
{
    public class MessageCodecTests
    {
        [Test]
        public void Encode_SinPayload()
        {
            Assert.AreEqual("{\"type\":\"PING\"}", MessageCodec.Encode("PING"));
        }

        [Test]
        public void EncodeDecode_RoundTrip()
        {
            var json = MessageCodec.Encode("MATCH_WELCOME", "{\"matchId\":\"m1\",\"n\":3}");
            var msg = MessageCodec.Decode(json);

            Assert.AreEqual("MATCH_WELCOME", msg.Type);
            Assert.IsNotNull(msg.Payload);
            Assert.AreEqual("m1", msg.Payload["matchId"]);
            Assert.AreEqual(3d, msg.Payload["n"]);
        }

        [Test]
        public void Decode_SinType_Lanza()
        {
            Assert.Throws<System.FormatException>(() => MessageCodec.Decode("{\"payload\":{}}"));
        }
    }
}
