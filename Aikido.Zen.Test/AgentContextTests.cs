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
            var context4 = new Context { RemoteAddress = "9.9.9.1", Method = "GET", Url = url, UserAgent = "useragent", Route = "testUrl" };
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
        public void AddRoute_ShouldIncrementHits_ForRoutesAndHosts()
        {
            // Arrange
            var context = new Context
            {
                Url = "/api/repeated",
                Method = "GET",
                Route = "/api/repeated"
            };

            // Act
            _agentContext.Clear();
            _agentContext.AddRoute(context); // Hit 1 (TryAdd)
            _agentContext.AddRoute(context); // Hit 2 (TryGetValue -> Increment)
            _agentContext.AddRoute(context); // Hit 3 (TryGetValue -> Increment)

            _agentContext.AddHostname("example.com:8080"); // Hit 1 (TryAdd)
            _agentContext.AddHostname("example.com:8080"); // Hit 2 (TryGetValue -> Increment)
            _agentContext.AddHostname("example.com:8080"); // Hit 3 (TryGetValue -> Increment)
            _agentContext.AddHostname("example.com:8081"); // Hit 1 (TryAdd)

            // Assert
            Assert.That(_agentContext.Routes.Count(), Is.EqualTo(1), "Should only be one route entry.");
            var route = _agentContext.Routes.First();
            Assert.That(route.Path, Is.EqualTo(context.Route));
            Assert.That(route.Method, Is.EqualTo(context.Method));
            Assert.That(route.Hits, Is.EqualTo(3), "Hits should be incremented for the same route.");

            Assert.That(_agentContext.Hostnames.Count(), Is.EqualTo(2), "Should only be two hostnames entries.");
            var host = _agentContext.Hostnames.FirstOrDefault(x => x.Port == 8080);
            Assert.That(host.Hostname, Is.EqualTo("example.com"));
            Assert.That(host.Hits, Is.EqualTo(3));

            host = _agentContext.Hostnames.FirstOrDefault(x => x.Port == 8081);
            Assert.That(host.Hostname, Is.EqualTo("example.com"));
            Assert.That(host.Port, Is.EqualTo(8081));
            Assert.That(host.Hits, Is.EqualTo(1));
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
            _agentContext.UpdateBlockedUsers(new[] { "user1" });
            var context = new Context { User = new User("user1", "Blocked User"), RemoteAddress = string.Empty, Method = string.Empty, Url = string.Empty, UserAgent = string.Empty };

            // Act
            bool isBlocked = _agentContext.IsBlocked(context, out var reason);

            // Assert
            Assert.That(isBlocked, Is.True);
            Assert.That(reason, Is.EqualTo("User is blocked"));
        }

        [Test]
        public void IsBlocked_ShouldReturnFalse_WhenUserIsNotBlocked()
        {
            // Arrange
            _agentContext.UpdateBlockedUsers(new[] { "user1" });
            var context = new Context { User = new User("user2", "Not Blocked User"), RemoteAddress = string.Empty, Method = string.Empty, Url = string.Empty, UserAgent = string.Empty };

            // Act
            bool isBlocked = _agentContext.IsBlocked(context, out var reason);

            // Assert
            Assert.That(isBlocked, Is.False);
            Assert.That(reason, Is.Null);
        }

        [Test]
        public void UpdateRatelimitedRoutes_ShouldUpdateEndpointsList()
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
            var endpointsList = _agentContext.Endpoints.ToList();
            Assert.That(endpointsList.Count, Is.EqualTo(2));

            var endpoint1 = endpointsList.FirstOrDefault(e => e.Method == "GET" && e.Route == "/api/test1");
            var endpoint2 = endpointsList.FirstOrDefault(e => e.Method == "POST" && e.Route == "/api/test2");

            Assert.That(endpoint1, Is.Not.Null);
            Assert.That(endpoint1.RateLimiting.MaxRequests, Is.EqualTo(30));
            Assert.That(endpoint1.RateLimiting.WindowSizeInMS, Is.EqualTo(2000));

            Assert.That(endpoint2, Is.Not.Null);
            Assert.That(endpoint2.RateLimiting.MaxRequests, Is.EqualTo(60));
        }

        [Test]
        public void UpdateRatelimitedRoutes_ShouldReplaceExistingEndpoints()
        {
            // Arrange
            var initialEndpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/old",
                    RateLimiting = new RateLimitingConfig { MaxRequests = 100 }
                }
            };
            _agentContext.UpdateRatelimitedRoutes(initialEndpoints);

            var newEndpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/new",
                    RateLimiting = new RateLimitingConfig { MaxRequests = 30 }
                }
            };

            // Act
            _agentContext.UpdateRatelimitedRoutes(newEndpoints);

            // Assert
            var endpointsList = _agentContext.Endpoints.ToList();
            Assert.That(endpointsList.Count, Is.EqualTo(1));
            Assert.That(endpointsList.Any(e => e.Method == "GET" && e.Route == "/api/new"), Is.True);
            Assert.That(endpointsList.Any(e => e.Method == "GET" && e.Route == "/api/old"), Is.False);
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

                var endpointsList = _agentContext.Endpoints.ToList();
                var endpoint = endpointsList.FirstOrDefault(e => e.Method == "GET" && e.Route == "/test");
                Assert.That(endpoint, Is.Not.Null);
                Assert.That(endpoint.RateLimiting.MaxRequests, Is.EqualTo(60));

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

        [Test]
        public void AddHostname_ShouldEvictLeastFrequentlyUsed_WhenMaxReached()
        {
            // Arrange
            const int MaxHostnames = 2000;
            var firstHostname = "host0.com:80";
            _agentContext.Clear();

            // Act
            // Add the first hostname - this will have Hits = 1 and should be the LFU candidate
            _agentContext.AddHostname(firstHostname);

            // Add MaxHostnames more hostnames, each with Hits = 2
            for (int i = 1; i <= MaxHostnames; i++)
            {
                var hostname = $"host{i}.com:80";
                _agentContext.AddHostname(hostname); // Access 1: TryAdd -> Hits=1
                _agentContext.AddHostname(hostname); // Access 2: TryGetValue -> Increment -> Hits=2
            }

            // Add one more hostname to trigger eviction
            var extraHostname = $"host{MaxHostnames + 1}.com:80";
            _agentContext.AddHostname(extraHostname); // Hits = 1 (added)
            _agentContext.AddHostname(extraHostname); // Hits = 2 (incremented)

            // Assert
            Assert.That(_agentContext.Hostnames.Count(), Is.EqualTo(MaxHostnames), "Dictionary size should be at max capacity.");
            // host0 (Hits=1) should be evicted as the LFU item.
            Assert.That(_agentContext.Hostnames.Any(h => h.Hostname == "host0.com"), Is.False, "First hostname (host0) should be evicted as it was LFU (1 hit).");
            // Verify one of the later added hostnames (with Hits=2) is still present.
            // We check host1 as an example, but any host from 1 to MaxHostnames could be present.
            Assert.That(_agentContext.Hostnames.Any(h => h.Hostname == "host1.com"), Is.True, "A hostname with higher hits (host1) should remain.");
            // Verify the extra hostname added last (with Hits=2) is present.
            Assert.That(_agentContext.Hostnames.Any(h => h.Hostname == extraHostname.Split(':')[0]), Is.True, "The extra hostname added to trigger eviction should remain.");
        }

        [Test]
        public void AddUser_ShouldEvictLeastFrequentlyUsed_WhenMaxReached()
        {
            // Arrange
            const int MaxUsers = 2000;
            var firstUser = new User("user0", "User Zero");
            var ipAddress = "192.168.0.1";
            _agentContext.Clear();

            // Act
            // Add the first user - this will have Hits = 1 and should be the LFU candidate
            _agentContext.AddUser(firstUser, ipAddress);

            // Add MaxUsers more users, each with Hits = 2
            for (int i = 1; i <= MaxUsers; i++)
            {
                var user = new User($"user{i}", $"User {i}");
                _agentContext.AddUser(user, ipAddress); // Access 1: TryAdd -> Hits=1
                _agentContext.AddUser(user, ipAddress); // Access 2: TryGetValue -> Increment -> Hits=2
            }

            // Add one more user to trigger eviction
            var extraUser = new User($"user{MaxUsers + 1}", $"User {MaxUsers + 1}");
            _agentContext.AddUser(extraUser, ipAddress); // Hits = 1 (added)
            _agentContext.AddUser(extraUser, ipAddress); // Hits = 2 (incremented)

            // Assert
            Assert.That(_agentContext.Users.Count(), Is.EqualTo(MaxUsers), "Dictionary size should be at max capacity.");
            // user0 (Hits=1) should be evicted as the LFU item.
            Assert.That(_agentContext.Users.Any(u => u.Id == "user0"), Is.False, "First user (user0) should be evicted as it was LFU (1 hit).");
            // Verify one of the later added users (with Hits=2) is still present.
            // We check user1 as an example.
            Assert.That(_agentContext.Users.Any(u => u.Id == "user1"), Is.True, "A user with higher hits (user1) should remain.");
            // Verify the extra user added last (with Hits=2) is present.
            Assert.That(_agentContext.Users.Any(u => u.Id == extraUser.Id), Is.True, "The extra user added to trigger eviction should remain.");
        }

        [Test]
        public void AddRoute_ShouldEvictLeastFrequentlyUsed_WhenMaxReached()
        {
            // Arrange
            const int MaxRoutes = 5000;
            var firstRouteContext = new Context { Url = "/route0", Method = "GET", Route = "/route0" };
            _agentContext.Clear();

            // Act
            // Add the first route - this will have Hits = 1 and should be the LFU candidate
            _agentContext.AddRoute(firstRouteContext);

            // Add MaxRoutes more routes, each with Hits = 2
            for (int i = 1; i <= MaxRoutes; i++)
            {
                var routeContext = new Context { Url = $"/route{i}", Method = "GET", Route = $"/route{i}" };
                _agentContext.AddRoute(routeContext); // Access 1: TryAdd -> Hits=1
                _agentContext.AddRoute(routeContext); // Access 2: TryGetValue -> Increment -> Hits=2
            }

            // Add one more route to trigger eviction
            var extraRouteContext = new Context { Url = $"/route{MaxRoutes + 1}", Method = "GET", Route = $"/route{MaxRoutes + 1}" };
            _agentContext.AddRoute(extraRouteContext); // Hits = 1 (added)
            _agentContext.AddRoute(extraRouteContext); // Hits = 2 (incremented)

            // Assert
            Assert.That(_agentContext.Routes.Count(), Is.EqualTo(MaxRoutes), "Dictionary size should be at max capacity.");
            // route0 (Hits=1) should be evicted as the LFU item.
            Assert.That(_agentContext.Routes.Any(r => r.Path == "/route0"), Is.False, "First route (route0) should be evicted as it was LFU (1 hit).");
            // Verify one of the later added routes (with Hits=2) is still present.
            // We check route1 as an example.
            Assert.That(_agentContext.Routes.Any(r => r.Path == "/route1"), Is.True, "A route with higher hits (/route1) should remain.");
            // Verify the extra route added last (with Hits=2) is present.
            Assert.That(_agentContext.Routes.Any(r => r.Path == extraRouteContext.Route), Is.True, "The extra route added to trigger eviction should remain.");
        }
    }
}
