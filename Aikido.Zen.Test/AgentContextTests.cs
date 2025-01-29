using System.Text.RegularExpressions;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
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
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testurl",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                }
            };
            var blockedUserAgents = new Regex("googlebot|bingbot|yandexbot");

            _agentContext.UpdateBlockedUsers(new[] { "user1" });
            _agentContext.BlockList.AddIpAddressToBlocklist("192.168.1.101");
            _agentContext.BlockList.UpdateAllowedSubnets(endpoints);
            _agentContext.UpdateBlockedUserAgents(blockedUserAgents);

            // Act & Assert
            Assert.That(_agentContext.IsBlocked(user, "192.168.1.102", url, "useragent")); // Blocked user
            Assert.That(_agentContext.IsBlocked(null, "192.168.1.101", url, "useragent")); // Blocked IP
            Assert.That(_agentContext.IsBlocked(null, ip, url, "useragent")); // Not in allowed subnet
            Assert.That(_agentContext.IsBlocked(null, "10.0.0.1", url, "useragent"), Is.False); // In allowed subnet
            Assert.That(_agentContext.IsBlocked(null, "invalid.ip", url, "useragent"), Is.False); // Invalid IP should not be blocked
            Assert.That(_agentContext.IsBlocked(new User("user2", "allowed"), "10.0.0.1", url, "useragent"), Is.False); // Non-blocked user in allowed subnet
            Assert.That(_agentContext.IsBlocked(new User("user2", "allowed"), "192.168.1.101", url, "googlebot"), Is.True); // Blocked user agent
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
            Assert.That(host == null, Is.False);
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
            Assert.That(userExtended == null, Is.False);
            Assert.That(userExtended.Name, Is.EqualTo("User One"));
            Assert.That(userExtended.LastIpAddress, Is.EqualTo(ipAddress));
        }

        [Test]
        public void AddRoute_ShouldAddRouteToDictionary()
        {
            // Arrange
            var context = new Context
            {
                Url = "/api/test",
                Method = "GET"
            };

            // Act
            _agentContext.AddRoute(context);

            // Assert
            var route = _agentContext.Routes.FirstOrDefault(r => r.Path == context.Url);
            Assert.That(route == null, Is.False);
            Assert.That(route.Method, Is.EqualTo(context.Method));
            Assert.That(route.Hits, Is.EqualTo(1));
        }

        [Test]
        public void AddRoute_WithNullContext_HandlesGracefully()
        {
            // Act
            _agentContext.AddRoute(null);

            // Assert
            Assert.That(_agentContext.Routes, Is.Empty);
        }

        [Test]
        public void AddRoute_WithNullUrl_HandlesGracefully()
        {
            // Arrange
            var context = new Context
            {
                Method = "GET",
                Url = null
            };

            // Act
            _agentContext.AddRoute(context);

            // Assert
            Assert.That(_agentContext.Routes, Is.Empty);
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
            _agentContext.AddRoute(new Context
            {
                Url = "/api/test",
                Method = "GET"
            });

            // Act
            _agentContext.Clear();

            // Assert
            Assert.That(_agentContext.Requests, Is.EqualTo(0));
            Assert.That(_agentContext.RequestsAborted, Is.EqualTo(0));
            Assert.That(_agentContext.AttacksDetected, Is.EqualTo(0));
            Assert.That(_agentContext.AttacksBlocked, Is.EqualTo(0));
            Assert.That(_agentContext.Hostnames, Is.Empty);
            Assert.That(_agentContext.Users, Is.Empty);
            Assert.That(_agentContext.Routes, Is.Empty);
        }

        [Test]
        public void IsBlocked_ShouldReturnTrue_WhenUserIsBlocked()
        {
            // Arrange
            var user = new User("user1", "User One");
            _agentContext.UpdateBlockedUsers(new[] { "user1" });

            // Act
            var isBlocked = _agentContext.IsBlocked(user, string.Empty, string.Empty, string.Empty);

            // Assert
            Assert.That(isBlocked);
        }

        [Test]
        public void IsBlocked_ShouldReturnFalse_WhenUserIsNotBlocked()
        {
            // Arrange
            var user = new User("user1", "User One");

            // Act
            var isBlocked = _agentContext.IsBlocked(user, string.Empty, string.Empty, string.Empty);

            // Assert
            Assert.That(isBlocked, Is.False);
        }

        [Test]
        public void AddRateLimitedEndpoint_ShouldAddConfigToDictionary()
        {
            // Arrange
            var path = "GET|api/test";
            var config = new RateLimitingConfig { Enabled = true, MaxRequests = 10, WindowSizeInMS = 1000 };

            // Act
            _agentContext.AddRateLimitedEndpoint(path, config);

            // Assert
            Assert.That(_agentContext.RateLimitedRoutes.ContainsKey(path), Is.True);
            Assert.That(_agentContext.RateLimitedRoutes[path].MaxRequests, Is.EqualTo(10));
            Assert.That(_agentContext.RateLimitedRoutes[path].Enabled, Is.True);
            Assert.That(_agentContext.RateLimitedRoutes[path].WindowSizeInMS, Is.EqualTo(1000));
        }

        [Test]
        public void AddRateLimitedEndpoint_WithNullPath_ShouldNotAddToRoutes()
        {
            // Arrange
            string path = null;
            var config = new RateLimitingConfig { MaxRequests = 60 };

            // Act
            _agentContext.AddRateLimitedEndpoint(path, config);

            // Assert
            Assert.That(_agentContext.RateLimitedRoutes, Is.Empty);
        }

        [Test]
        public void AddRateLimitedEndpoint_WithNullConfig_ShouldNotAddToRoutes()
        {
            // Arrange
            var path = "GET|/api/test";
            RateLimitingConfig config = null;

            // Act
            _agentContext.AddRateLimitedEndpoint(path, config);

            // Assert
            Assert.That(_agentContext.RateLimitedRoutes, Is.Empty);
        }

        [Test]
        public void UpdateRatelimitedRoutes_ShouldUpdateRoutesFromEndpoints()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/test1",
                    RateLimiting = new RateLimitingConfig { MaxRequests = 30, WindowSizeInMS = 2000 }
                },
                new EndpointConfig {
                    Method = "POST",
                    Route = "/api/test2",
                    RateLimiting = new RateLimitingConfig { MaxRequests = 60 }
                }
            };

            // Act
            _agentContext.UpdateRatelimitedRoutes(endpoints);

            // Assert
            Assert.That(_agentContext.RateLimitedRoutes.Count, Is.EqualTo(2));
            Assert.That(_agentContext.RateLimitedRoutes["GET|api/test1"].MaxRequests, Is.EqualTo(30));
            Assert.That(_agentContext.RateLimitedRoutes["GET|api/test1"].WindowSizeInMS, Is.EqualTo(2000));
            Assert.That(_agentContext.RateLimitedRoutes["POST|api/test2"].MaxRequests, Is.EqualTo(60));
        }

        [Test]
        public void UpdateRatelimitedRoutes_ShouldClearExistingRoutes()
        {
            // Arrange
            _agentContext.AddRateLimitedEndpoint("GET|/api/old", new RateLimitingConfig { MaxRequests = 100 });
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/new",
                    RateLimiting = new RateLimitingConfig { MaxRequests = 30 }
                }
            };

            // Act
            _agentContext.UpdateRatelimitedRoutes(endpoints);

            // Assert
            Assert.That(_agentContext.RateLimitedRoutes.Count, Is.EqualTo(1));
            Assert.That(_agentContext.RateLimitedRoutes.ContainsKey("GET|api/new"), Is.True);
            Assert.That(_agentContext.RateLimitedRoutes.ContainsKey("GET|/api/old"), Is.False);
        }

        [Test]
        public void UpdateConfig_ShouldUpdateAllConfigurationAspects()
        {
            // Arrange
            var block = true;
            var blockedUsers = new[] { "user1", "user2" };
            var endpoints = new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "/test",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" },
                    RateLimiting = new RateLimitingConfig { MaxRequests = 60 }

                }
            };
            var configVersion = 123L;

            // Act
            _agentContext.UpdateConfig(block, blockedUsers, endpoints, null, configVersion);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(Environment.GetEnvironmentVariable("AIKIDO_BLOCK"), Is.EqualTo("true"));
                Assert.That(_agentContext.IsUserBlocked("user1"), Is.True);
                Assert.That(_agentContext.IsUserBlocked("user2"), Is.True);
                Assert.That(_agentContext.RateLimitedRoutes["GET|test"].MaxRequests, Is.EqualTo(60));
                Assert.That(_agentContext.ConfigLastUpdated, Is.EqualTo(configVersion));
            });
        }

        [Test]
        public void UpdateBlockedIps_ShouldUpdateBlockedSubnets()
        {
            // Arrange
            var blockedIPs = new[] { "192.168.1.0/24", "10.0.0.1" };

            // Act
            _agentContext.UpdateBlockedIps(blockedIPs);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_agentContext.BlockList.IsIPBlocked("192.168.1.100"), Is.True);
                Assert.That(_agentContext.BlockList.IsIPBlocked("10.0.0.1"), Is.True);
                Assert.That(_agentContext.BlockList.IsIPBlocked("172.16.0.1"), Is.False);
            });
        }

        [Test]
        public void UpdateBlockedIps_WithNullInput_ShouldHandleGracefully()
        {
            // Arrange
            _agentContext.UpdateBlockedIps(new[] { "192.168.1.1" });

            // Act
            _agentContext.UpdateBlockedIps(null);

            // Assert
            Assert.That(_agentContext.BlockList.IsIPBlocked("192.168.1.1"), Is.False);
        }

        [Test]
        public void UpdateBlockedIps_WithInvalidIPs_ShouldSkipInvalidOnes()
        {
            // Arrange
            var blockedIPs = new[] { "invalid-ip", "192.168.1.1", "not-an-ip" };

            // Act
            _agentContext.UpdateBlockedIps(blockedIPs);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_agentContext.BlockList.IsIPBlocked("192.168.1.1"), Is.True);
                Assert.That(_agentContext.BlockList.IsIPBlocked("invalid-ip"), Is.False);
                Assert.That(_agentContext.BlockList.IsIPBlocked("not-an-ip"), Is.False);
            });
        }

        [Test]
        public void UpdateBlockedUserAgents_ShouldUpdateBlockedUserAgentsList()
        {
            // Arrange
            var userAgents = new Regex("googlebot|bingbot|yandexbot");

            // Act
            _agentContext.UpdateBlockedUserAgents(userAgents);

            // Assert
            Assert.That(_agentContext.IsUserAgentBlocked("googlebot"), Is.True);
            Assert.That(_agentContext.IsUserAgentBlocked("bingbot"), Is.True);
            Assert.That(_agentContext.IsUserAgentBlocked("yandexbot"), Is.True);
            Assert.That(_agentContext.IsUserAgentBlocked("Opera/9.80"), Is.False);
        }

        [Test]
        public void UpdateBlockedUserAgents_WithEmptyList_ShouldClearBlockedUserAgents()
        {
            // Arrange
            _agentContext.UpdateBlockedUserAgents(new Regex("Mozilla/5.0"));

            // Act
            _agentContext.UpdateBlockedUserAgents(null);

            // Assert
            Assert.That(_agentContext.IsUserAgentBlocked("Mozilla/5.0"), Is.False);
        }

        [Test]
        public void IsUserAgentBlocked_ShouldReturnTrue_WhenUserAgentIsBlocked()
        {
            // Arrange
            var userAgent = "Mozilla/5.0";
            _agentContext.UpdateBlockedUserAgents(new Regex(userAgent));

            // Act
            var isBlocked = _agentContext.IsUserAgentBlocked(userAgent);

            // Assert
            Assert.That(isBlocked, Is.True);
        }

        [Test]
        public void IsUserAgentBlocked_ShouldReturnFalse_WhenUserAgentIsNotBlocked()
        {
            // Arrange
            var userAgent = "Mozilla/5.0";

            // Act
            var isBlocked = _agentContext.IsUserAgentBlocked(userAgent);

            // Assert
            Assert.That(isBlocked, Is.False);
        }
    }
}
