using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;
using NetTools;
using NUnit.Framework;
using System.Collections.Generic;

namespace Aikido.Zen.Test
{
    public class AgentContextTests
    {
        private AgentContext _agentContext;

        [SetUp]
        public void Setup()
        {
            _agentContext = new AgentContext();
        }

        [Test]
        public void UpdateBlockedUsers_ShouldUpdateBlockedUsersList()
        {
            // Arrange
            var users = new[] { "user1", "user2", "user3" };

            // Act
            _agentContext.UpdateBlockedUsers(users);

            // Assert
            Assert.IsTrue(_agentContext.IsUserBlocked("user1"));
            Assert.IsTrue(_agentContext.IsUserBlocked("user2"));
            Assert.IsTrue(_agentContext.IsUserBlocked("user3"));
            Assert.IsFalse(_agentContext.IsUserBlocked("user4"));
        }

        [Test]
        public void UpdateBlockedUsers_WithEmptyList_ShouldClearBlockedUsers()
        {
            // Arrange
            _agentContext.UpdateBlockedUsers(new[] { "user1" });

            // Act
            _agentContext.UpdateBlockedUsers(System.Array.Empty<string>());

            // Assert
            Assert.IsFalse(_agentContext.IsUserBlocked("user1"));
        }

        [Test]
        public void IsBlocked_ShouldCheckAllBlockingConditions()
        {
            // Arrange
            var user = new User("user1", "blocked");
            var ip = "192.168.1.100";
            var url = "GET|testurl";

            _agentContext.UpdateBlockedUsers(new[] { "user1" });
            _agentContext.BlockList.AddIpAddressToBlocklist("192.168.1.101");
            _agentContext.BlockList.UpdateAllowedSubnets(new Dictionary<string, IEnumerable<IPAddressRange>>
            {
                { url, new[] { IPAddressRange.Parse("10.0.0.0/8") } }
            });

            // Act & Assert
            Assert.IsTrue(_agentContext.IsBlocked(user, "192.168.1.102", url)); // Blocked user
            Assert.IsTrue(_agentContext.IsBlocked(null, "192.168.1.101", url)); // Blocked IP
            Assert.IsTrue(_agentContext.IsBlocked(null, ip, url)); // Not in allowed subnet
            Assert.IsFalse(_agentContext.IsBlocked(null, "10.0.0.1", url)); // In allowed subnet
            Assert.IsFalse(_agentContext.IsBlocked(null, "invalid.ip", url)); // Invalid IP should not be blocked
            Assert.IsFalse(_agentContext.IsBlocked(new User("user2", "allowed"), "10.0.0.1", url)); // Non-blocked user in allowed subnet
        }
    }
}
