using System.Text.RegularExpressions;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
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
            var ip = "8.8.8.100";  // Using public IP
            var url = "http://localhost:80/testUrl";
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "9.9.9.0/24" }
                }
            };
            var blockedUserAgents = new Regex("googlebot|bingbot|yandexbot");

            _agentContext.UpdateBlockedUsers(new[] { "user1" });
            _agentContext.BlockList.AddIpAddressToBlocklist("8.8.8.101");  // Using public IP
            _agentContext.BlockList.UpdateAllowedIpsPerEndpoint(endpoints);
            _agentContext.UpdateBlockedUserAgents(blockedUserAgents);

            // Act & Assert
            var context1 = new Context { User = user, RemoteAddress = "8.8.8.102", Method = "GET", Url = url, UserAgent = "useragent", Route = "testUrl" };
            var context2 = new Context { RemoteAddress = "8.8.8.101", Method = "GET", Url = url, UserAgent = "useragent", Route = "testUrl" };
            var context3 = new Context { RemoteAddress = ip, Method = "GET", Url = url, UserAgent = "useragent", Route = "testUrl" };
            var context4 = new Context { RemoteAddress = "9.9.9.1", Method = "GET", Url = url, UserAgent = "useragent" , Route = "testUrl" };
            var context5 = new Context { RemoteAddress = "invalid.ip", Method = "GET", Url = url, UserAgent = "useragent", Route = "testUrl" };
            var context6 = new Context { User = new User("user2", "allowed"), RemoteAddress = "9.9.9.1", Method = "GET", Url = url, UserAgent = "useragent", Route = "testUrl" };
            var context7 = new Context { User = new User("user2", "allowed"), RemoteAddress = "8.8.8.101", Method = "GET", Url = url, UserAgent = "googlebot", Route = "testUrl" };

            Assert.That(_agentContext.IsBlocked(context1, out var reason1)); // Blocked user
            Assert.That(_agentContext.IsBlocked(context2, out var reason2)); // Blocked IP
            Assert.That(_agentContext.IsBlocked(context3, out var reason3)); // Not in allowed subnet
            Assert.That(_agentContext.IsBlocked(context4, out var reason4), Is.False); // In allowed subnet
            Assert.That(_agentContext.IsBlocked(context5, out var reason5), Is.False); // Invalid IP should not be blocked
            Assert.That(_agentContext.IsBlocked(context6, out var reason6), Is.False); // Non-blocked user in allowed subnet
            Assert.That(_agentContext.IsBlocked(context7, out var reason7), Is.True); // Blocked user agent
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
                Method = "GET",
                Route = "/api/test"
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
            Assert.That(_agentContext.Routes.Count() == 1);
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
            var context = new Context { User = user, RemoteAddress = string.Empty, Method = string.Empty, Url = string.Empty, UserAgent = string.Empty };

            // Act
            var isBlocked = _agentContext.IsBlocked(context, out var reason);

            // Assert
            Assert.That(isBlocked);
        }

        [Test]
        public void IsBlocked_ShouldReturnFalse_WhenUserIsNotBlocked()
        {
            // Arrange
            var user = new User("user1", "User One");
            var context = new Context { User = user, RemoteAddress = string.Empty, Method = string.Empty, Url = string.Empty, UserAgent = string.Empty };

            // Act
            var isBlocked = _agentContext.IsBlocked(context, out var reason);

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
            Assert.That(_agentContext.RateLimitedRoutes["GET|/api/test1"].MaxRequests, Is.EqualTo(30));
            Assert.That(_agentContext.RateLimitedRoutes["GET|/api/test1"].WindowSizeInMS, Is.EqualTo(2000));
            Assert.That(_agentContext.RateLimitedRoutes["POST|/api/test2"].MaxRequests, Is.EqualTo(60));
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
            Assert.That(_agentContext.RateLimitedRoutes.ContainsKey("GET|/api/new"), Is.True);
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

            var response = new ReportingAPIResponse
            {
                Block = block,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints,
                ConfigUpdatedAt = configVersion
            };

            // Act
            _agentContext.UpdateConfig(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(Environment.GetEnvironmentVariable("AIKIDO_BLOCK"), Is.EqualTo("true"));
                Assert.That(_agentContext.IsUserBlocked("user1"), Is.True);
                Assert.That(_agentContext.IsUserBlocked("user2"), Is.True);
                Assert.That(_agentContext.RateLimitedRoutes["GET|/test"].MaxRequests, Is.EqualTo(60));
                Assert.That(_agentContext.ConfigLastUpdated, Is.EqualTo(configVersion));
            });
        }

        [Test]
        public void UpdateBlockedIps_ShouldUpdateBlockedSubnets()
        {
            // Arrange
            var blockedIPs = new[] { "192.168.1.0/24", "10.0.0.1" };
            var blockedIPList = new FirewallListsAPIResponse.IPList
            {
                Ips = blockedIPs,
                Description = "Test"
            };
            var firewallAPiResponse = new FirewallListsAPIResponse(blockedIPAddresses: new[] { blockedIPList });

            // Act
            _agentContext.UpdateFirewallLists(firewallAPiResponse);

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
            var ips = new[] { "192.168.1.1" };
            var ipList = new FirewallListsAPIResponse.IPList
            {
                Ips = ips,
                Description = "Test"
            };

            var firewallListsAPIResponse = new FirewallListsAPIResponse(blockedIPAddresses: new[] { ipList });
            _agentContext.UpdateFirewallLists(firewallListsAPIResponse);

            // Act
            _agentContext.UpdateFirewallLists((FirewallListsAPIResponse?)null);

            // Assert
            Assert.That(_agentContext.BlockList.IsIPBlocked("192.168.1.1"), Is.False);
        }

        [Test]
        public void UpdateBlockedIps_WithInvalidIPs_ShouldSkipInvalidOnes()
        {
            // Arrange
            var blockedIPs = new[] { "invalid-ip", "192.168.1.1", "not-an-ip" };
            var blockedIpList = new FirewallListsAPIResponse.IPList
            {
                Ips = blockedIPs,
                Description = "Test"
            };
            var firewallAPiResponse = new FirewallListsAPIResponse(blockedIPAddresses: new[] { blockedIpList });

            // Act
            _agentContext.UpdateFirewallLists(firewallAPiResponse);

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
