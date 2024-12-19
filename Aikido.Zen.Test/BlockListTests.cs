using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;
using NetTools;

namespace Aikido.Zen.Test
{
    public class BlockListTests
    {
        private BlockList _blockList;

        [SetUp]
        public void Setup()
        {
            _blockList = new BlockList();
        }

        [Test]
        public void UpdateBlockedSubnets_ShouldUpdateBlockedSubnetsList()
        {
            // Arrange
            var subnet1 = IPAddressRange.Parse("192.168.1.0/24");
            var subnet2 = IPAddressRange.Parse("10.0.0.0/8");
            var subnets = new[] { subnet1, subnet2 };

            // Act
            _blockList.UpdateBlockedSubnets(subnets);

            // Assert
            Assert.IsTrue(_blockList.IsIPBlocked("192.168.1.100"));
            Assert.IsTrue(_blockList.IsIPBlocked("10.10.10.10"));
            Assert.IsFalse(_blockList.IsIPBlocked("172.16.1.1"));
        }

        [Test]
        public void UpdateBlockedSubnets_WithEmptyList_ShouldClearBlockedSubnets()
        {
            // Arrange
            var subnet = IPAddressRange.Parse("192.168.1.0/24");
            _blockList.UpdateBlockedSubnets(new[] { subnet });

            // Act
            _blockList.UpdateBlockedSubnets(Array.Empty<IPAddressRange>());

            // Assert
            Assert.IsFalse(_blockList.IsIPBlocked("192.168.1.100"));
        }

        [Test]
        public void UpdateAllowedSubnets_ShouldUpdateAllowedSubnetsPerUrl()
        {
            // Arrange
            var subnet1 = IPAddressRange.Parse("192.168.1.0/24");
            var subnet2 = IPAddressRange.Parse("10.0.0.0/8");
            var allowedSubnets = new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { "GET|url1", new[] { subnet1 } },
                { "POST|url2", new[] { subnet2 } },
                { "POST|url1", new [] { subnet2 } },
            };

            // Act
            _blockList.UpdateAllowedSubnets(allowedSubnets);

            // Assert
            Assert.IsTrue(_blockList.IsIPAllowed("192.168.1.100", "GET|url1"));
            Assert.IsFalse(_blockList.IsIPAllowed("192.168.1.100", "POST|url1"));
            Assert.IsTrue(_blockList.IsIPAllowed("10.10.10.10", "POST|url2"));
            Assert.IsFalse(_blockList.IsIPAllowed("10.10.10.10", "GET|url1"));
            Assert.IsTrue(_blockList.IsIPAllowed("172.16.1.1", "GET|url3")); // No restrictions for url3
        }

        [Test]
        public void UpdateAllowedSubnets_WithEmptyDictionary_ShouldClearAllowedSubnets()
        {
            // Arrange
            var subnet = IPAddressRange.Parse("192.168.1.0/24");
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { "GET|url1", new[] { subnet } }
            });

            // Act
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>());

            // Assert
            Assert.IsTrue(_blockList.IsIPAllowed("192.168.1.100", "GET|url1")); // Should allow when no restrictions
        }

        [Test]
        public void AddIpAddressToBlocklist_ShouldBlockSpecificIP()
        {
            // Arrange
            var ip = "192.168.1.100";

            // Act
            _blockList.AddIpAddressToBlocklist(ip);

            // Assert
            Assert.IsTrue(_blockList.IsIPBlocked(ip));
            Assert.IsFalse(_blockList.IsIPBlocked("192.168.1.101"));
        }

        [Test]
        public void AddIpAddressToBlocklist_WithInvalidIP_ShouldStillAddToBlocklist()
        {
            // Arrange
            var invalidIp = "invalid.ip.address";

            // Act
            _blockList.AddIpAddressToBlocklist(invalidIp);

            // Assert
            Assert.IsTrue(_blockList.IsIPBlocked(invalidIp));
        }

        [Test]
        public void IsIPBlocked_WithInvalidIP_ShouldReturnFalse()
        {
            // Arrange
            var invalidIp = "invalid.ip.address";

            // Act & Assert
            Assert.IsFalse(_blockList.IsIPBlocked(invalidIp));
        }

        [Test]
        public void IsIPAllowed_WithInvalidIP_ShouldReturnTrue()
        {
            // Arrange
            var subnet = IPAddressRange.Parse("192.168.1.0/24");
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { "GET|url1", new[] { subnet } }
            });

            // Act & Assert
            Assert.IsTrue(_blockList.IsIPAllowed("invalid.ip", "GET|url1"));
        }

        [Test]
        public void IsBlocked_ShouldCheckAllBlockingConditions()
        {
            // Arrange
            var ip = "192.168.1.100";
            var url = "GET|testurl";

            _blockList.AddIpAddressToBlocklist("192.168.1.101");
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { url, new[] { IPAddressRange.Parse("10.0.0.0/8") } }
            });

            // Act & Assert
            Assert.IsTrue(_blockList.IsBlocked("192.168.1.101", url)); // Blocked IP
            Assert.IsTrue(_blockList.IsBlocked(ip, url)); // Not in allowed subnet
            Assert.IsFalse(_blockList.IsBlocked("10.0.0.1", url)); // In allowed subnet
            Assert.IsFalse(_blockList.IsBlocked("invalid.ip", url)); // Invalid IP should not be blocked
            Assert.IsFalse(_blockList.IsBlocked("10.0.0.1", url)); // Non-blocked user in allowed subnet
        }
    }
}
