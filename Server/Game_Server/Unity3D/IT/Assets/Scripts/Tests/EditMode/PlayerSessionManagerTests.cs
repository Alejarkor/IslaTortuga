using NUnit.Framework;
using IslaTortuga.GameServer.Gateway;

namespace IslaTortuga.GameServer.Tests
{
    public class PlayerSessionManagerTests
    {
        [Test]
        public void Add_Get_Remove_ResuelvePorSocketId()
        {
            var mgr = new PlayerSessionManager();
            var s = new PlayerSession("sock_1", "p1", "match_1", null);

            mgr.Add(s);
            Assert.AreSame(s, mgr.Get("sock_1"));
            Assert.AreEqual(1, mgr.Count);

            Assert.AreSame(s, mgr.Remove("sock_1"));
            Assert.IsNull(mgr.Get("sock_1"));
            Assert.AreEqual(0, mgr.Count);
        }

        [Test]
        public void ForMatch_FiltraPorMatchId()
        {
            var mgr = new PlayerSessionManager();
            mgr.Add(new PlayerSession("a", "p1", "m1", null));
            mgr.Add(new PlayerSession("b", "p2", "m1", null));
            mgr.Add(new PlayerSession("c", "p3", "m2", null));

            Assert.AreEqual(2, mgr.ForMatch("m1").Count);
            Assert.AreEqual(1, mgr.ForMatch("m2").Count);
        }
    }
}
