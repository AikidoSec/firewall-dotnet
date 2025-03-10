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
    }
}
