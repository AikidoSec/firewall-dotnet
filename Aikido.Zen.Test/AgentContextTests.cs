using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System.Linq;

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
        public void AddRequest_ShouldIncrementRequests()
        {
            // Act
            _agentContext.AddRequest();

            // Assert
            Assert.AreEqual(1, _agentContext.Requests);
        }

        [Test]
        public void AddAbortedRequest_ShouldIncrementRequestsAborted()
        {
            // Act
            _agentContext.AddAbortedRequest();

            // Assert
            Assert.AreEqual(1, _agentContext.RequestsAborted);
        }

        [Test]
        public void AddAttackDetected_ShouldIncrementAttacksDetected()
        {
            // Act
            _agentContext.AddAttackDetected();

            // Assert
            Assert.AreEqual(1, _agentContext.AttacksDetected);
        }

        [Test]
        public void AddAttackBlocked_ShouldIncrementAttacksBlocked()
        {
            // Act
            _agentContext.AddAttackBlocked();

            // Assert
            Assert.AreEqual(1, _agentContext.AttacksBlocked);
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
            Assert.AreEqual(8080, host.Port);
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
            Assert.AreEqual("User One", userExtended.Name);
            Assert.AreEqual(ipAddress, userExtended.LastIpAddress);
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
            Assert.AreEqual(method, route.Method);
            Assert.AreEqual(1, route.Hits);
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
            Assert.AreEqual(0, _agentContext.Requests);
            Assert.AreEqual(0, _agentContext.RequestsAborted);
            Assert.AreEqual(0, _agentContext.AttacksDetected);
            Assert.AreEqual(0, _agentContext.AttacksBlocked);
            Assert.IsEmpty(_agentContext.Hostnames);
            Assert.IsEmpty(_agentContext.Users);
            Assert.IsEmpty(_agentContext.Routes);
        }

        [Test]
        public void IsBlocked_ShouldReturnTrue_WhenUserIsBlocked()
        {
            // Arrange
            var user = new User("user1", "User One");
            _agentContext.BlockList.UpdateBlockedUsers(new[] { "user1" });

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
