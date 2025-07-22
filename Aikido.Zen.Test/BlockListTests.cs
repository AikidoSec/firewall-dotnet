using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;

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
        public void UpdateBlockedIps_ShouldUpdateBlockedIpsList()
        {
            // Arrange
            var subnet1 = "192.168.1.0/24";
            var subnet2 = "10.0.0.0/8";
            var subnets = new[] { subnet1, subnet2 };

            // Act
            _blockList.UpdateBlockedIps(subnets);

            // Assert
            Assert.That(_blockList.IsIPBlocked("192.168.1.100"));
            Assert.That(_blockList.IsIPBlocked("10.10.10.10"));
            Assert.That(_blockList.IsIPBlocked("172.16.1.1"), Is.False);
        }

        [Test]
        public void UpdateBlockedIps_WithEmptyList_ShouldClearBlockedSubnets()
        {
            // Arrange
            var subnet = "192.168.1.0/24";
            _blockList.UpdateBlockedIps(new[] { subnet });

            // Act
            _blockList.UpdateBlockedIps(Array.Empty<string>());

            // Assert
            Assert.That(_blockList.IsIPBlocked("192.168.1.100"), Is.False);
        }

        [Test]
        public void UpdateAlllowedIps_ShouldUpdateAlllowedIpsPerUrl()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "url1",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                },
                new EndpointConfig {
                    Method = "POST",
                    Route = "url2",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                },
                new EndpointConfig {
                    Method = "POST",
                    Route = "url1",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                }
            };

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Assert
            var context1 = new Context { RemoteAddress = "192.168.1.100", Method = "GET", Url = "http://localhost:80/url1", Route = "url1" };
            var context2 = new Context { RemoteAddress = "192.168.1.100", Method = "POST", Url = "http://localhost:80/url1", Route = "url1" };
            var context3 = new Context { RemoteAddress = "10.10.10.10", Method = "POST", Url = "http://localhost:80/url2", Route = "url2" };
            var context4 = new Context { RemoteAddress = "10.10.10.10", Method = "GET", Url = "http://localhost:80/url1", Route = "url1" };
            var context5 = new Context { RemoteAddress = "172.16.1.1", Method = "GET", Url = "http://localhost:80/url3", Route = "url3" };

            Assert.That(_blockList.IsIpAllowedForEndpoint(context1));
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.False);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context3));
            Assert.That(_blockList.IsIpAllowedForEndpoint(context4), Is.False);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context5)); // No restrictions for url3
        }

        [Test]
        public void UpdateAlllowedIps_WithEmptyList_ShouldClearAllowedSubnets()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "url1",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                }
            };
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(new List<EndpointConfig>());

            // Assert
            var context = new Context { RemoteAddress = "192.168.1.100", Method = "GET", Url = "http://localhost:80/url1", Route = "url1" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context)); // Should allow when no restrictions
        }

        [Test]
        public void AddIpAddressToBlocklist_ShouldBlockSpecificIP()
        {
            // Arrange
            var ip = "192.168.1.100";

            // Act
            _blockList.AddIpAddressToBlocklist(ip);

            // Assert
            Assert.That(_blockList.IsIPBlocked(ip));
            Assert.That(_blockList.IsIPBlocked("192.168.1.101"), Is.False);
        }

        [Test]
        public void IsIPBlocked_WithInvalidIP_ShouldReturnFalse()
        {
            // Arrange
            var invalidIp = "invalid.ip.address";

            // Act & Assert
            Assert.That(_blockList.IsIPBlocked(invalidIp), Is.False);
        }

        [Test]
        public void IsIPAllowed_WithEmptyAllowedIpAddresses_ShouldAllowIP()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new string[] { } // Empty allowed IPs
                }
            };
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Act & Assert
            var context = new Context { RemoteAddress = "123.123.123.123", Method = "GET", Url = "http://localhost:80/testUrl", Route = "testUrl" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context)); // Should allow when no IP restrictions
        }

        [Test]
        public void IsIPAllowed_WithInvalidIP_ShouldReturnTrue()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                }
            };
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Act & Assert
            var context = new Context { RemoteAddress = "invalid.ip", Method = "GET", Url = "http://localhost:80/testUrl", Route = "testUrl" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context));
        }

        [Test]
        public void IsBlocked_ShouldCheckAllBlockingConditions()
        {
            // Arrange
            var ip = "123.123.123.100";
            var url = "http://localhost:80/testUrl";
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = [ "8.8.8.0/8" ]
                }
            };

            _blockList.AddIpAddressToBlocklist("192.168.1.101");
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Act & Assert
            var context1 = new Context { RemoteAddress = "123.123.123.101", Method = "GET", Url = url, Route = "testUrl" };
            var context2 = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            var context3 = new Context { RemoteAddress = "8.8.8.1", Method = "GET", Url = url, Route = "testUrl" };
            var context4 = new Context { RemoteAddress = "invalid.ip", Method = "GET", Url = url, Route = "testUrl" };

            Assert.That(_blockList.IsBlocked(context1, out var reason1)); // Blocked IP
            Assert.That(_blockList.IsBlocked(context2, out var reason2)); // Not in allowed subnet
            Assert.That(_blockList.IsBlocked(context3, out var reason3), Is.False); // In allowed subnet
            Assert.That(_blockList.IsBlocked(context4, out var reason4), Is.False); // Invalid IP should not be blocked
        }

        [Test]
        public void LargeBlocklistHandling_ShouldHandleLargeNumberOfIPs()
        {
            // Arrange
            for (int i = 0; i < 256; i++)
            {
                _blockList.AddIpAddressToBlocklist($"192.168.1.{i}");
                _blockList.AddIpAddressToBlocklist($"192.168.{i}.0/24");
            }

            // Act & Assert
            Assert.That(_blockList.IsIPBlocked("192.168.1.255"));
            Assert.That(_blockList.IsIPBlocked("192.168.2.1"));
            Assert.That(_blockList.IsIPBlocked("191.167.2.1"), Is.False);
        }

        [Test]
        public void PerformanceWithDifferentSizes_ShouldPerformEfficiently()
        {
            // Arrange
            var ip = "192.168.1.100";

            // Act
            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < 256; j++)
                {
                    _blockList.AddIpAddressToBlocklist($"192.{i}.{j}.0");
                }
            }
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < 256; i++)
            {
                _blockList.IsIPBlocked($"192.168.1.{i}");
            }
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds, Is.AtMost(5)); // Ensure it completes within 5ms
        }

        [Test]
        public void BypassedIPs_ShouldBypassAllBlockingRules()
        {
            // Arrange
            var ip = "123.123.123.123";
            var url = "http://localhost:80/testUrl";
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "8.8.8.0/8" }
                }
            };

            _blockList.AddIpAddressToBlocklist(ip);
            _blockList.UpdateAllowedIps(new[] { "8.8.8.0/8" });
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);
            _blockList.UpdateBypassedIps(new[] { ip });

            // Act & Assert
            var context = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("IP is bypassed"));
        }

        [Test]
        public void BypassedIPs_ShouldOverrideBlockedIPs()
        {
            // Arrange
            var ip = "123.123.123.123";
            var url = "http://localhost:80/testUrl";

            _blockList.UpdateAllowedIps(new[] { ip });
            _blockList.UpdateBypassedIps(new[] { ip });

            // Act & Assert
            var context = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("IP is bypassed"));
        }

        [Test]
        public void PrivateIPs_ShouldAlwaysBeAllowed()
        {
            // Arrange
            var privateIps = new[] {
                "10.0.0.1",
                "172.16.0.1",
                "192.168.1.1",
                "127.0.0.1",
                "169.254.0.1"
            };
            var url = "http://localhost:80/testUrl";

            _blockList.AddIpAddressToBlocklist("10.0.0.1");
            _blockList.UpdateAllowedIps(new[] { "8.8.8.0/8" });
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "8.8.8.0/8" }
                }
            };
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Act & Assert
            foreach (var ip in privateIps)
            {
                var context = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
                Assert.That(_blockList.IsBlocked(context, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("IP is private or local"));
            }
        }

        [Test]
        public void InvalidIPs_ShouldBeHandledAppropriately()
        {
            // Arrange
            var invalidIps = new[] {
                "invalid.ip",
                "256.256.256.256",
                "1.2.3.4.5",
                "192.168.1",
                "192.168.1.1/33"
            };
            var url = "http://localhost:80/testUrl";

            // Act & Assert
            // Invalid IPs should not be blocked by default
            Assert.That(_blockList.IsIPBlocked("invalid.ip"), Is.False);

            // Invalid IPs should not be allowed when there are allowed IPs
            _blockList.UpdateAllowedIps(new[] { "8.8.8.0/8" });
            Assert.That(_blockList.IsIpAllowed("invalid.ip"), Is.False);

            // Invalid IPs should not be bypassed
            Assert.That(_blockList.IsIPBypassed("invalid.ip"), Is.False);

            // Invalid IPs should be allowed for endpoints with no restrictions
            var context1 = new Context { RemoteAddress = "invalid.ip", Method = "GET", Url = "http://localhost:80/unrestricted", Route = "unrestricted" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context1));

            // Invalid IPs should be blocked for endpoints with restrictions
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "restricted",
                    AllowedIPAddresses = new[] { "8.8.8.0/8" }
                }
            };
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);
            var context2 = new Context { RemoteAddress = "invalid.ip", Method = "GET", Url = "http://localhost:80/restricted", Route = "restricted" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.True);
        }

        [Test]
        public void IPRules_ShouldBeAppliedInCorrectOrder()
        {
            // Arrange
            var ip = "123.123.123.123";
            var url = "http://localhost:80/testUrl";
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "8.8.8.0/8" }
                }
            };

            // Act & Assert
            // 1. Private IPs should be allowed first
            var context1 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context1, out var reason1), Is.False);
            Assert.That(reason1, Is.EqualTo("IP is private or local"));

            // 2. Bypassed IPs should be allowed second
            _blockList.UpdateBypassedIps(new[] { ip });
            var context2 = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context2, out var reason2), Is.False);
            Assert.That(reason2, Is.EqualTo("IP is bypassed"));

            // 3. Blocked IPs should be checked third
            _blockList.UpdateBypassedIps(Array.Empty<string>());
            _blockList.UpdateBlockedIps([ip]);
            var context3 = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context3, out var reason3), Is.True);
            Assert.That(reason3, Is.EqualTo("IP is blocked"));

            // 4. Bypassed IPs should override blocked IPs
            _blockList.UpdateBypassedIps(new[] { ip });
            var context4 = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context4, out var reason4), Is.False);
            Assert.That(reason4, Is.EqualTo("IP is bypassed"));

            // 5. Endpoint-specific rules should be checked last
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);
            _blockList.UpdateBlockedIps(Array.Empty<string>());
            _blockList.UpdateBypassedIps(Array.Empty<string>());
            var context5 = new Context { RemoteAddress = ip, Method = "GET", Url = url, Route = "testUrl" };
            Assert.That(_blockList.IsBlocked(context5, out var reason5), Is.True);
            Assert.That(reason5, Is.EqualTo("IP is not allowed for this endpoint"));
        }

        [Test]
        public void EndpointMatching_WithWildcards_ShouldMatchCorrectly()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/users/*",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                },
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/*/123",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                },
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/users/123",
                    AllowedIPAddresses = new[] { "172.16.0.0/12" }
                }
            };

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Assert
            // Exact match should take precedence
            var context1 = new Context { RemoteAddress = "172.16.1.1", Method = "GET", Url = "http://localhost:80/api/users/123", Route = "api/users/123" };
            var context2 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/api/users/123", Route = "api/users/123" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context1), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.False);

            // Wildcard matches should work
            var context3 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/api/users/456", Route = "api/users/{id}" };
            var context4 = new Context { RemoteAddress = "10.0.0.1", Method = "GET", Url = "http://localhost:80/api/products/123", Route = "api/products/{id}" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context3), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context4), Is.True);

            // No match should allow by default
            var context5 = new Context { RemoteAddress = "8.8.8.8", Method = "GET", Url = "http://localhost:80/api/unknown", Route = "api/unknown" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context5), Is.True);
        }

        [Test]
        public void EndpointMatching_WithWildcardMethod_ShouldMatchAnyMethod()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "*",
                    Route = "api/users",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                },
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/users",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                }
            };

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Assert
            // Exact method match should take precedence
            var context1 = new Context { RemoteAddress = "10.0.0.1", Method = "GET", Url = "http://localhost:80/api/users", Route = "api/users" };
            var context2 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/api/users", Route = "api/users" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context1), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.False);

            // Wildcard method should match other methods
            var context3 = new Context { RemoteAddress = "192.168.1.1", Method = "POST", Url = "http://localhost:80/api/users", Route = "api/users" };
            var context4 = new Context { RemoteAddress = "192.168.1.1", Method = "PUT", Url = "http://localhost:80/api/users", Route = "api/users" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context3), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context4), Is.True);
        }

        [Test]
        public void EndpointMatching_WithMultipleMatches_ShouldUseMostSpecificMatch()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/*/*",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                },
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/*/123",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                },
                new EndpointConfig {
                    Method = "GET",
                    Route = "api/users/*",
                    AllowedIPAddresses = new[] { "172.16.0.0/12" }
                }
            };

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Assert
            // Most specific match should be used
            var context1 = new Context { RemoteAddress = "172.16.1.1", Method = "GET", Url = "http://localhost:80/api/users/123", Route = "api/users/123" };
            var context2 = new Context { RemoteAddress = "10.0.0.1", Method = "GET", Url = "http://localhost:80/api/products/123", Route = "api/products/123" };
            var context3 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/api/other/456", Route = "api/other/456" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context1), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context3), Is.True);
        }

        [Test]
        public void EndpointMatching_WithInvalidEndpointFormat_ShouldAllowAccess()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/users",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                }
            };

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Assert
            var context1 = new Context { RemoteAddress = "192.168.1.1", Method = "invalid-format", Url = "http://localhost:80/invalid-format", Route = "invalid-format" };
            var context2 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/", Route = "" };
            var context3 = new Context { RemoteAddress = "192.168.1.1", Method = "", Url = "http://localhost:80/api/users", Route = "api/users" };
            var context4 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/api/users|extra", Route = "api/users|extra" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context1), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context3), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context4), Is.True);
        }

        [Test]
        public void EndpointMatching_WithEmptyAllowedIPs_ShouldAllowAccess()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/users",
                    AllowedIPAddresses = new string[] { }
                },
                new EndpointConfig {
                    Method = "GET",
                    Route = "/api/products",
                    AllowedIPAddresses = null
                }
            };

            // Act
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);

            // Assert
            var context1 = new Context { RemoteAddress = "192.168.1.1", Method = "GET", Url = "http://localhost:80/api/users", Route = "api/users" };
            var context2 = new Context { RemoteAddress = "10.0.0.1", Method = "GET", Url = "http://localhost:80/api/products", Route = "api/products" };
            Assert.That(_blockList.IsIpAllowedForEndpoint(context1), Is.True);
            Assert.That(_blockList.IsIpAllowedForEndpoint(context2), Is.True);
        }
    }
}
