using Aikido.Zen.Core.Models;
using NetTools;

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
            Assert.That(_agentContext.IsUserBlocked("user1"), Is.True);
            Assert.That(_agentContext.IsUserBlocked("user2"), Is.True);
            Assert.That(_agentContext.IsUserBlocked("user3"), Is.True);
            Assert.That(_agentContext.IsUserBlocked("user4"), Is.False);
        }

        [Test]
        public void UpdateBlockedUsers_WithEmptyList_ShouldClearBlockedUsers()
        {
            // Arrange
            _agentContext.UpdateBlockedUsers(new[] { "user1" });

            // Act
            _agentContext.UpdateBlockedUsers(System.Array.Empty<string>());

            // Assert
            Assert.That(_agentContext.IsUserBlocked("user1"), Is.False);
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

        [Test]
        public void AddRequest_ShouldIncrementRequests()
        {
            // Act
            _agentContext.AddRequest();

            // Assert
            Assert.That(_agentContext.Requests, Is.EqualTo(1));
        }

        [Test]
        public void AddAbortedRequest_ShouldIncrementRequestsAborted()
        {
            // Act
            _agentContext.AddAbortedRequest();

            // Assert
            Assert.That(_agentContext.RequestsAborted, Is.EqualTo(1));
        }

        [Test]
        public void AddAttackDetected_ShouldIncrementAttacksDetected()
        {
            // Act
            _agentContext.AddAttackDetected();

            // Assert
            Assert.That(_agentContext.AttacksDetected, Is.EqualTo(1));
        }

        [Test]
        public void AddAttackBlocked_ShouldIncrementAttacksBlocked()
        {
            // Act
            _agentContext.AddAttackBlocked();

            // Assert
            Assert.That(_agentContext.AttacksBlocked, Is.EqualTo(1));
        }

        [Test]
        public void AddHostname_ShouldAddHostnameToDictionary()
        {
            // Arrange
            var hostname = "example.com:8080";

            // Act
            _agentContext.AddHostname(hostname);

            // Assert
            var host = _agentContext.Hostnames.FirstOrDefault(h => h.Hostname == "example.com");
            Assert.IsNotNull(host);
            Assert.That(host.Port, Is.EqualTo(8080));
        }

        [Test]
        public void AddUser_ShouldHandleNullGracefully()
        {
            // Arrange
            User user = null;
            var ipAddress = "192.168.1.1";

            // Act
            _agentContext.AddUser(user, ipAddress);

            // Assert
            Assert.That(_agentContext.Users, Is.Empty);
        }

        [Test]
        public void AddUser_ShouldAddUserToDictionary()
        {
            // Arrange
            var user = new User("user1", "User One");
            var ipAddress = "192.168.1.1";

            // Act
            _agentContext.AddUser(user, ipAddress);

            // Assert
            var userExtended = _agentContext.Users.FirstOrDefault(u => u.Id == "user1");
            Assert.IsNotNull(userExtended);
            Assert.That(userExtended.Name, Is.EqualTo("User One"));
            Assert.That(userExtended.LastIpAddress, Is.EqualTo(ipAddress));
        }

        [Test]
        public void AddRoute_ShouldAddRouteToDictionary()
        {
            // Arrange
            var path = "/api/test";
            var method = "GET";

            // Act
            _agentContext.AddRoute(path, method);

            // Assert
            var route = _agentContext.Routes.FirstOrDefault(r => r.Path == path);
            Assert.IsNotNull(route);
            Assert.That(route.Method, Is.EqualTo(method));
            Assert.That(route.Hits, Is.EqualTo(1));
        }

        [Test]
        public void Clear_ShouldResetAllProperties()
        {
            // Arrange
            _agentContext.AddRequest();
            _agentContext.AddAbortedRequest();
            _agentContext.AddAttackDetected();
            _agentContext.AddAttackBlocked();
            _agentContext.AddHostname("example.com:8080");
            _agentContext.AddUser(new User("user1", "User One"), "192.168.1.1");
            _agentContext.AddRoute("/api/test", "GET");

            // Act
            _agentContext.Clear();

            // Assert
            Assert.That(_agentContext.Requests, Is.EqualTo(0));
            Assert.That(_agentContext.RequestsAborted, Is.EqualTo(0));
            Assert.That(_agentContext.AttacksDetected, Is.EqualTo(0));
            Assert.That(_agentContext.AttacksBlocked, Is.EqualTo(0));
            Assert.IsEmpty(_agentContext.Hostnames);
            Assert.IsEmpty(_agentContext.Users);
            Assert.IsEmpty(_agentContext.Routes);
        }

        [Test]
        public void IsBlocked_ShouldReturnTrue_WhenUserIsBlocked()
        {
            // Arrange
            var user = new User("user1", "User One");
            _agentContext.UpdateBlockedUsers(new[] { "user1" });

            // Act
            var isBlocked = _agentContext.IsBlocked(user, string.Empty, string.Empty);

            // Assert
            Assert.IsTrue(isBlocked);
        }

        [Test]
        public void IsBlocked_ShouldReturnFalse_WhenUserIsNotBlocked()
        {
            // Arrange
            var user = new User("user1", "User One");

            // Act
            var isBlocked = _agentContext.IsBlocked(user, string.Empty, string.Empty);

            // Assert
            Assert.IsFalse(isBlocked);
        }
    }
}
