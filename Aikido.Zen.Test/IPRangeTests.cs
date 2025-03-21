using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Ip;
using NUnit.Framework;
using System.Collections.Generic;

namespace Aikido.Zen.Test
{
    public class IPRangeTests
    {
        private IPRange _ipRange;

        [SetUp]
        public void Setup()
        {
            _ipRange = new IPRange();
        }

        [Test]
        public void InsertRange_ShouldInsertSingleIP()
        {
            // Arrange
            var ip = "192.168.1.1";
            var ip6 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

            // Act
            _ipRange.InsertRange(ip);
            _ipRange.InsertRange(ip6);

            // Assert
            Assert.That(_ipRange.IsIpInRange(ip));
            Assert.That(_ipRange.IsIpInRange(ip6));
        }

        [Test]
        public void InsertRange_ShouldInsertCIDRRange()
        {
            // Arrange
            var cidr = "192.168.1.0/24";
            var cidr6 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334/64";

            // Act
            _ipRange.InsertRange(cidr);
            _ipRange.InsertRange(cidr6);

            // Assert
            Assert.That(_ipRange.IsIpInRange("192.168.1.1"));
            Assert.That(_ipRange.IsIpInRange("192.168.1.255"));
            Assert.That(_ipRange.IsIpInRange("192.168.2.1"), Is.False);
            Assert.That(_ipRange.IsIpInRange("2001:0db8:85a3:0000:0000:8a2e:0370:7334"));
            Assert.That(_ipRange.IsIpInRange("2001:0db8:85a3:0000:0000:8a2e:0370:7335"));
            Assert.That(_ipRange.IsIpInRange("2001:0db8:85a3:0000:0000:8a2e:0370:7334:ffff"), Is.False);
        }

        [Test]
        public void IsIpInRange_WithEmptyRange_ShouldReturnFalse()
        {
            // Arrange
            var ip = "192.168.1.1";

            // Act & Assert
            Assert.That(_ipRange.IsIpInRange(ip), Is.False);
        }

        [Test]
        public void IsIpInRange_WithInvalidIP_ShouldReturnFalse()
        {
            // Arrange
            var invalidIp = "invalid.ip.address";

            // Act & Assert
            Assert.That(_ipRange.IsIpInRange(invalidIp), Is.False);
        }

        [Test]
        public void InsertRange_ShouldHandleMultipleRanges()
        {
            // Arrange
            var ranges = new List<string> { "192.168.1.0/24", "10.0.0.0/8" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.That(_ipRange.IsIpInRange("192.168.1.1"));
            Assert.That(_ipRange.IsIpInRange("10.0.0.1"));
            Assert.That(_ipRange.IsIpInRange("172.16.0.1"), Is.False);
        }

        [Test]
        public void InsertRange_ShouldHandleOverlappingRanges()
        {
            // Arrange
            var range1 = "192.168.1.0/24";
            var range2 = "192.168.1.128/25";

            // Act
            _ipRange.InsertRange(range1);
            _ipRange.InsertRange(range2);

            // Assert
            Assert.That(_ipRange.IsIpInRange("192.168.1.1"));
            Assert.That(_ipRange.IsIpInRange("192.168.1.200"));
        }

        [Test]
        public void HasItems_ShouldReturnTrueWhenItemsExist()
        {
            // Arrange
            var range = "192.168.1.0/24";

            // Act
            _ipRange.InsertRange(range);

            // Assert
            Assert.That(_ipRange.HasItems);
        }

        [Test]
        public void HasItems_ShouldReturnFalseWhenNoItemsExist()
        {
            // Act & Assert
            Assert.That(_ipRange.HasItems, Is.False);
        }

        [Test]
        public void ShouldWorkWithZeroZeroZeroZero()
        {
            // Arrange
            var ip = "0.0.0.0/8";

            // Act
            _ipRange.InsertRange(ip);

            // Assert
            Assert.That(_ipRange.IsIpInRange("0.0.0.0"));
        }

        [Test]
        public void ShouldWorkWithZeroZeroZeroZeroAndMultipleRanges()
        {
            // Arrange
            var ip = "0.0.0.0/8";
            var ip2 = "10.0.0.0/8";
            var ip3 = "::/128";

            // Act
            _ipRange.InsertRange(ip);
            _ipRange.InsertRange(ip2);
            _ipRange.InsertRange(ip3);
            // Assert
            Assert.That(_ipRange.IsIpInRange("0.0.0.0"));
            Assert.That(_ipRange.IsIpInRange("10.0.0.0"));
            Assert.That(_ipRange.IsIpInRange("::"));
            Assert.That(_ipRange.IsIpInRange("192.168.1.0"), Is.False);
            Assert.That(_ipRange.IsIpInRange("d6:3f:f8:00:00:00:00:00"), Is.False);
        }

        [Test]
        public void IsIpInRange_ShouldWorkWithZeroAddress()
        {
            // Arrange
            var ipRange = new IPRange();
            ipRange.InsertRange("0.0.0.0/8");

            // Act & Assert
            Assert.Multiple(() =>
            {
                Assert.That(ipRange.IsIpInRange("0.0.0.0"), Is.True, "Should match exact 0.0.0.0");
                Assert.That(ipRange.IsIpInRange("0.1.2.3"), Is.True, "Should match address in 0.0.0.0/8 range");
                Assert.That(ipRange.IsIpInRange("1.0.0.0"), Is.False, "Should not match address outside range");
            });
        }

        [Test]
        public void IPHelper_ShouldIdentifyZeroAddressAsPrivate()
        {
            // Act & Assert
            Assert.That(IPHelper.IsPrivateOrLocalIp("0.0.0.0"), Is.True, "0.0.0.0 should be identified as private");
            Assert.That(IPHelper.IsPrivateOrLocalIp("0.1.2.3"), Is.True, "Address in 0.0.0.0/8 should be identified as private");
        }

        [Test]
        public void ShouldHandleDifferentCIDRNotations()
        {
            // Arrange
            var ranges = new List<string> { "1.2.3.4/16", "1.2.3.4/15", "1.2.3.4/17" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_ipRange.IsIpInRange("1.2.3.4"), Is.True, "Should match exact IP");
                Assert.That(_ipRange.IsIpInRange("1.2.0.4"), Is.True, "Should match IP in /16 range");
                Assert.That(_ipRange.IsIpInRange("1.2.3.0"), Is.True, "Should match IP in /16 range");
            });
        }

        [Test]
        public void ShouldHandleIPv6CIDRNotations()
        {
            // Arrange
            var ranges = new List<string> { "1:2::/32", "1:2::/31", "1:2::/33" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_ipRange.IsIpInRange("1:2::"), Is.True, "Should match exact IPv6");
                Assert.That(_ipRange.IsIpInRange("1:2:0:0:0:0:0:0"), Is.True, "Should match IPv6 in /32 range");
                Assert.That(_ipRange.IsIpInRange("1:2:ffff:ffff:ffff:ffff:ffff:ffff"), Is.True, "Should match IPv6 in /32 range");
            });
        }

        [Test]
        public void ShouldHandleZeroCIDRNotation()
        {
            // Arrange
            var ranges = new List<string> { "0.0.0.0/0", "::/0" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_ipRange.IsIpInRange("1.2.3.4"), Is.True, "Should match any IPv4 with /0");
                Assert.That(_ipRange.IsIpInRange("255.255.255.255"), Is.True, "Should match any IPv4 with /0");
                Assert.That(_ipRange.IsIpInRange("1:2:3:4:5:6:7:8"), Is.True, "Should match any IPv6 with /0");
                Assert.That(_ipRange.IsIpInRange("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"), Is.True, "Should match any IPv6 with /0");
            });
        }

        [Test]
        public void ShouldHandleMixedIPv4AndIPv6()
        {
            // Arrange
            var ranges = new List<string> { "1.2.3.4/16", "1:2:3:4:5:6:1.2.3.4/128" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_ipRange.IsIpInRange("1.2.3.4"), Is.True, "Should match IPv4");
                Assert.That(_ipRange.IsIpInRange("1.2.0.4"), Is.True, "Should match IPv4 in range");
                Assert.That(_ipRange.IsIpInRange("1:2:3:4:5:6:1.2.3.4"), Is.True, "Should match exact IPv6");
                Assert.That(_ipRange.IsIpInRange("1:2:3:4:5:6:1.2.3.5"), Is.False, "Should not match different IPv6");
            });
        }

        [Test]
        public void ShouldHandleOverlappingRangesWithDifferentCIDR()
        {
            // Arrange
            var ranges = new List<string> { "1.2.3.4/16", "1.2.3.4/15", "1.2.3.4/17" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_ipRange.IsIpInRange("1.2.3.4"), Is.True, "Should match exact IP");
                Assert.That(_ipRange.IsIpInRange("1.2.0.4"), Is.True, "Should match IP in /16 range");
                Assert.That(_ipRange.IsIpInRange("1.2.3.0"), Is.True, "Should match IP in /16 range");
                Assert.That(_ipRange.IsIpInRange("1.2.4.0"), Is.True, "Should match IP in /15 range");
                Assert.That(_ipRange.IsIpInRange("1.2.3.128"), Is.True, "Should match IP in /17 range");
            });
        }

        [Test]
        public void ShouldHandleZeroAddresses()
        {
            // Arrange
            var ranges = new List<string> { "0.0.0.0/8", "::/128" };

            // Act
            foreach (var range in ranges)
            {
                _ipRange.InsertRange(range);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_ipRange.IsIpInRange("0.0.0.0"), Is.True, "Should match IPv4 zero address");
                Assert.That(_ipRange.IsIpInRange("0.1.2.3"), Is.True, "Should match IPv4 in zero range");
                Assert.That(_ipRange.IsIpInRange("::"), Is.True, "Should match IPv6 zero address");
                Assert.That(_ipRange.IsIpInRange("::1"), Is.False, "Should not match different IPv6");
            });
        }
    }
}
