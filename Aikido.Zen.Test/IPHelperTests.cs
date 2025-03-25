using Aikido.Zen.Core.Helpers;
using System.Net;

namespace Aikido.Zen.Test.Helpers
{
    /// <summary>
    /// Contains unit tests for IPHelper methods.
    /// </summary>
    public class IPHelperTests
    {
        [Test]
        public void IsValidIP_ShouldReturnTrue_WhenValidIP()
        {
            // Arrange
            var validIp1 = "192.168.1.1";
            var validIp2 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

            // Act
            bool result1 = IPAddress.TryParse(validIp1, out _);
            bool result2 = IPAddress.TryParse(validIp2, out _);

            // Assert
            Assert.That(result1);
            Assert.That(result2);
        }

        [Test]
        public void IsValidIP_ShouldReturnFalse_WhenInvalidIP()
        {
            // Arrange
            var invalidIp1 = "999.999.999.999";
            var invalidIp2 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334:1234";

            // Act
            bool result1 = IPAddress.TryParse(invalidIp1, out _);
            bool result2 = IPAddress.TryParse(invalidIp2, out _);

            // Assert
            Assert.That(result1, Is.False);
            Assert.That(result2, Is.False);
        }

        [Test]
        public void ToCidrString_ShouldReturnSingleCidr_WhenSingleIpProvided()
        {
            // Arrange
            var startIp = "192.168.1.1";

            // Act
            var result = IPHelper.ToCidrString(startIp);

            // Assert
            Assert.That(result, Is.EquivalentTo(new List<string> { "192.168.1.1" }));
        }

        [Test]
        public void ToCidrString_ShouldReturnCidrRange_WhenIpRangeProvided()
        {
            // Arrange
            var startIp = "192.168.1.0";
            var endIp = "192.168.1.255";

            // Act
            var result = IPHelper.ToCidrString(startIp, endIp);

            // Assert
            Assert.That(result, Is.EquivalentTo(new List<string> { "192.168.1.0/24" }));
        }

        [Test]
        public void ToCidrString_ShouldReturnMultipleCidrs_WhenNonContiguousIpRangeProvided()
        {
            // Arrange
            var startIp = "192.168.1.0";
            var endIp = "192.168.1.128";

            // Act
            var result = IPHelper.ToCidrString(startIp, endIp);

            // Assert
            Assert.That(result, Is.EquivalentTo(new List<string> { "192.168.1.0/25", "192.168.1.128/32" }));
        }

        [Test]
        public void ContainsPrivateIPAddress_ShouldReturnFalse_ForPublicIPs()
        {
            // Arrange
            var publicIPs = new List<string>
            {
                "44.37.112.180", "46.192.247.73", "71.12.102.112", "101.0.26.90", "111.211.73.40",
                "156.238.194.84", "164.101.185.82", "223.231.138.242", "::1fff:0.0.0.0", "::1fff:10.0.0.0",
                "::1fff:0:0.0.0.0", "::1fff:0:10.0.0.0", "2001:2:ffff:ffff:ffff:ffff:ffff:ffff", "64:ff9a::0.0.0.0",
                "64:ff9a::255.255.255.255", "99::", "99::ffff:ffff:ffff:ffff", "101::", "101::ffff:ffff:ffff:ffff",
                "2000::", "2000::ffff:ffff:ffff:ffff:ffff:ffff", "2001:10::", "2001:1f:ffff:ffff:ffff:ffff:ffff:ffff",
                "2001:db7::", "2001:db7:ffff:ffff:ffff:ffff:ffff:ffff", "2001:db9::", "fb00::",
                "fbff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "fec0::", "::ffff:1.2.3.4", "::ffff:172.1.2.3", "::ffff:192.145.0.0"
            };

            // Act & Assert
            foreach (var ip in publicIPs)
            {
                Assert.That(IPHelper.IsPrivateOrLocalIp(ip), Is.False, $"Expected {ip} to be public");
            }
        }

        [Test]
        public void ContainsPrivateIPAddress_ShouldReturnTrue_ForPrivateIPs()
        {
            // Arrange
            var privateIPs = new List<string>
            {
                "0.0.0.0", "0000.0000.0000.0000", "0000.0000", "0.0.0.1", "0.0.0.7", "0.0.0.255", "0.0.255.255",
                "0.1.255.255", "0.15.255.255", "0.63.255.255", "0.255.255.254", "0.255.255.255", "10.0.0.0",
                "10.0.0.1", "10.0.0.01", "10.0.0.001", "10.255.255.254", "10.255.255.255", "100.64.0.0", "100.64.0.1",
                "100.127.255.254", "100.127.255.255", "127.0.0.0", "127.0.0.1", "127.0.0.01", "127.1", "127.0.1",
                "127.000.000.1", "127.255.255.254", "127.255.255.255", "169.254.0.0", "169.254.0.1", "169.254.255.254",
                "169.254.255.255", "172.16.0.0", "172.16.0.1", "172.16.0.001", "172.31.255.254", "172.31.255.255",
                "192.0.0.0", "192.0.0.1", "192.0.0.6", "192.0.0.7", "192.0.0.8", "192.0.0.9", "192.0.0.10", "192.0.0.11",
                "192.0.0.170", "192.0.0.171", "192.0.0.254", "192.0.0.255", "192.0.2.0", "192.0.2.1", "192.0.2.254",
                "192.0.2.255", "192.31.196.0", "192.31.196.1", "192.31.196.254", "192.31.196.255", "192.52.193.0",
                "192.52.193.1", "192.52.193.254", "192.52.193.255", "192.88.99.0", "192.88.99.1", "192.88.99.254",
                "192.88.99.255", "192.168.0.0", "192.168.0.1", "192.168.255.254", "192.168.255.255", "192.175.48.0",
                "192.175.48.1", "192.175.48.254", "192.175.48.255", "198.18.0.0", "198.18.0.1", "198.19.255.254",
                "198.19.255.255", "198.51.100.0", "198.51.100.1", "198.51.100.254", "198.51.100.255", "203.0.113.0",
                "203.0.113.1", "203.0.113.254", "203.0.113.255", "240.0.0.0", "240.0.0.1", "224.0.0.0", "224.0.0.1",
                "255.0.0.0", "255.192.0.0", "255.240.0.0", "255.254.0.0", "255.255.0.0", "255.255.255.0",
                "255.255.255.248", "255.255.255.254", "255.255.255.255", "0000:0000:0000:0000:0000:0000:0000:0000",
                "::", "::1", "::ffff:0.0.0.0", "::ffff:127.0.0.1", "fe80::", "fe80::1", "fe80::abc:1",
                "febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "fc00::", "fc00::1", "fc00::abc:1",
                "fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "2130706433", "0x7f000001", "fd00:ec2::254", "169.254.169.254",
                "::", "::1", "fc00::1", "fe80::1", "100::1", "2001:db8::1", "3fff::1",
                "::ffff:127.0.0.2", "::ffff:10.0.0.1", "::ffff:172.16.1.2", "::ffff:192.168.2.2"
            };

            // Act & Assert
            foreach (var ip in privateIPs)
            {
                Assert.That(IPHelper.IsPrivateOrLocalIp(ip), Is.True, $"Expected {ip} to be private");
            }
        }

        [Test]
        public void ContainsPrivateIPAddress_ShouldReturnFalse_ForInvalidIPs()
        {
            // Arrange
            var invalidIPs = new List<string>
            {
                "100::ffff::", "::ffff:0.0.255.255.255", "::ffff:0.255.255.255.255"
            };

            // Act & Assert
            foreach (var ip in invalidIPs)
            {
                Assert.That(IPHelper.IsPrivateOrLocalIp(ip), Is.False, $"Expected {ip} to be invalid");
            }
        }

        [Test]
        public void MapIPv4ToIPv6_ShouldReturnCorrectIPv6Mappings()
        {
            // Act & Assert
            Assert.That(IPHelper.IPv4ToIPv6("127.0.0.0"), Is.EqualTo("::ffff:127.0.0.0/128"));
            Assert.That(IPHelper.IPv4ToIPv6("127.0.0.0/8"), Is.EqualTo("::ffff:127.0.0.0/104"));
            Assert.That(IPHelper.IPv4ToIPv6("10.0.0.0"), Is.EqualTo("::ffff:10.0.0.0/128"));
            Assert.That(IPHelper.IPv4ToIPv6("10.0.0.0/8"), Is.EqualTo("::ffff:10.0.0.0/104"));
            Assert.That(IPHelper.IPv4ToIPv6("10.0.0.1"), Is.EqualTo("::ffff:10.0.0.1/128"));
            Assert.That(IPHelper.IPv4ToIPv6("10.0.0.1/8"), Is.EqualTo("::ffff:10.0.0.1/104"));
            Assert.That(IPHelper.IPv4ToIPv6("192.168.0.0/16"), Is.EqualTo("::ffff:192.168.0.0/112"));
            Assert.That(IPHelper.IPv4ToIPv6("172.16.0.0/12"), Is.EqualTo("::ffff:172.16.0.0/108"));
        }
    }
}
