using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class AttackKindTests
    {
        [TestCase(AttackKind.StoredSsrf, "stored_ssrf")]
        [TestCase(AttackKind.OutboundConnectionBlocked, "outbound_connection_blocked")]
        public void ToJsonName_ReturnsExpectedName(AttackKind kind, string expected)
        {
            Assert.That(kind.ToJsonName(), Is.EqualTo(expected));
        }

        [Test]
        public void ToJsonName_WhenKindIsUnknown_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((AttackKind)999).ToJsonName());
        }

        [TestCase(AttackKind.SqlInjection, "an SQL injection")]
        [TestCase(AttackKind.ShellInjection, "a shell injection")]
        [TestCase(AttackKind.PathTraversal, "a path traversal attack")]
        [TestCase(AttackKind.Ssrf, "a server-side request forgery")]
        [TestCase(AttackKind.StoredSsrf, "a stored server-side request forgery")]
        [TestCase(AttackKind.OutboundConnectionBlocked, "an outbound connection block")]
        public void ToHumanName_ReturnsExpectedName(AttackKind kind, string expected)
        {
            Assert.That(kind.ToHumanName(), Is.EqualTo(expected));
        }

        [Test]
        public void ToHumanName_WhenKindIsUnknown_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((AttackKind)999).ToHumanName());
        }
    }
}
