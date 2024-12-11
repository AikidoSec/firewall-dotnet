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
        public void UpdateBlockedUsers_ShouldUpdateBlockedUsersList()
        {
            // Arrange
            var users = new[] { "user1", "user2", "user3" };

            // Act
            _blockList.UpdateBlockedUsers(users);

            // Assert
            Assert.IsTrue(_blockList.IsUserBlocked("user1"));
            Assert.IsTrue(_blockList.IsUserBlocked("user2")); 
            Assert.IsTrue(_blockList.IsUserBlocked("user3"));
            Assert.IsFalse(_blockList.IsUserBlocked("user4"));
        }

        [Test]
        public void UpdateBlockedUsers_WithEmptyList_ShouldClearBlockedUsers()
        {
            // Arrange
            _blockList.UpdateBlockedUsers(new[] { "user1" });

            // Act
            _blockList.UpdateBlockedUsers(Array.Empty<string>());

            // Assert
            Assert.IsFalse(_blockList.IsUserBlocked("user1"));
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
                { "url1", new[] { subnet1 } },
                { "url2", new[] { subnet2 } }
            };

            // Act
            _blockList.UpdateAllowedSubnets(allowedSubnets);

            // Assert
            Assert.IsTrue(_blockList.IsIPAllowed("192.168.1.100", "url1"));
            Assert.IsFalse(_blockList.IsIPAllowed("192.168.1.100", "url2"));
            Assert.IsTrue(_blockList.IsIPAllowed("10.10.10.10", "url2"));
            Assert.IsFalse(_blockList.IsIPAllowed("10.10.10.10", "url1"));
            Assert.IsTrue(_blockList.IsIPAllowed("172.16.1.1", "url3")); // No restrictions for url3
        }

        [Test]
        public void UpdateAllowedSubnets_WithEmptyDictionary_ShouldClearAllowedSubnets()
        {
            // Arrange
            var subnet = IPAddressRange.Parse("192.168.1.0/24");
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { "url1", new[] { subnet } }
            });

            // Act
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>());

            // Assert
            Assert.IsTrue(_blockList.IsIPAllowed("192.168.1.100", "url1")); // Should allow when no restrictions
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
                { "url1", new[] { subnet } }
            });

            // Act & Assert
            Assert.IsTrue(_blockList.IsIPAllowed("invalid.ip", "url1"));
        }

        [Test]
        public void IsBlocked_ShouldCheckAllBlockingConditions()
        {
            // Arrange
            var user = new User("user1", "blocked");
            var ip = "192.168.1.100";
            var url = "testurl";

            _blockList.UpdateBlockedUsers(new[] { "user1" });
            _blockList.AddIpAddressToBlocklist("192.168.1.101");
            _blockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { url, new[] { IPAddressRange.Parse("10.0.0.0/8") } }
            });

            // Act & Assert
            Assert.IsTrue(_blockList.IsBlocked(user, "192.168.1.102", url)); // Blocked user
            Assert.IsTrue(_blockList.IsBlocked(null, "192.168.1.101", url)); // Blocked IP
            Assert.IsTrue(_blockList.IsBlocked(null, ip, url)); // Not in allowed subnet
            Assert.IsFalse(_blockList.IsBlocked(null, "10.0.0.1", url)); // In allowed subnet
            Assert.IsFalse(_blockList.IsBlocked(null, "invalid.ip", url)); // Invalid IP should not be blocked
            Assert.IsFalse(_blockList.IsBlocked(new User("user2", "allowed"), "10.0.0.1", url)); // Non-blocked user in allowed subnet
        }
    }
}
