using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class ImdsHelperTests
    {
        [TestCase("169.254.169.254", true)]
        [TestCase("fd00:ec2::254", true)]
        [TestCase("100.100.100.200", true)]
        [TestCase("::ffff:169.254.169.254", true)]
        [TestCase("::ffff:100.100.100.200", true)]
        [TestCase("0::ffff:6464:64c8", true)]
        [TestCase("0000:0000:0:0000:0000:ffff:a9fe:a9fe", true)]
        [TestCase("fd00:ec2:0:0000:0:0:0000:0254", true)]
        [TestCase("1.2.3.4", false)]
        [TestCase("example.com", false)]
        [TestCase("169.254.169.253", false)]
        public void IsImdsIPAddress_ReturnsExpectedResult(string ipAddress, bool expected)
        {
            var result = ImdsHelper.IsImdsIPAddress(ipAddress);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("metadata.google.internal", true)]
        [TestCase("metadata.goog", true)]
        [TestCase("METADATA.GOOGLE.INTERNAL", true)]
        [TestCase("example.com", false)]
        [TestCase("169.254.169.254", false)]
        public void IsTrustedHostname_ReturnsExpectedResult(string hostname, bool expected)
        {
            var result = ImdsHelper.IsTrustedHostname(hostname);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
