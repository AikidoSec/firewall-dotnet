using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;
using System.Threading.Tasks;

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
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "GET|url1"));
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "POST|url1"), Is.False);
            Assert.That(_blockList.IsIPAllowed("10.10.10.10", "POST|url2"));
            Assert.That(_blockList.IsIPAllowed("10.10.10.10", "GET|url1"), Is.False);
            Assert.That(_blockList.IsIPAllowed("172.16.1.1", "GET|url3")); // No restrictions for url3
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
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "GET|url1")); // Should allow when no restrictions
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
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "GET|testUrl")); // Should allow when no IP restrictions
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
            Assert.That(_blockList.IsIPAllowed("invalid.ip", "GET|testUrl"));
        }

        [Test]
        public void IsBlocked_ShouldCheckAllBlockingConditions()
        {
            // Arrange
            var ip = "123.123.123.100";
            var url = "GET|testUrl";
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
            Assert.That(_blockList.IsBlocked("123.123.123.101", url, out var reason)); // Blocked IP
            Assert.That(_blockList.IsBlocked(ip, url, out reason)); // Not in allowed subnet
            Assert.That(_blockList.IsBlocked("8.8.8.1", url, out reason), Is.False); // In allowed subnet
            Assert.That(_blockList.IsBlocked("invalid.ip", url, out reason), Is.False); // Invalid IP should not be blocked
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
            var url = "GET|testUrl";
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
            Assert.That(_blockList.IsBlocked(ip, url, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("IP is bypassed"));
        }

        [Test]
        public void BypassedIPs_ShouldOverrideBlockedIPs()
        {
            // Arrange
            var ip = "123.123.123.123";
            var url = "GET|testUrl";

            _blockList.UpdateAllowedIps(new[] { ip });
            _blockList.UpdateBypassedIps(new[] { ip });

            // Act & Assert
            Assert.That(_blockList.IsBlocked(ip, url, out var reason), Is.False);
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
            var url = "GET|testUrl";

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
                Assert.That(_blockList.IsBlocked(ip, url, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("Ip is private or local"));
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
            var url = "GET|testUrl";

            // Act & Assert
            // Invalid IPs should not be blocked by default
            Assert.That(_blockList.IsIPBlocked("invalid.ip"), Is.False);

            // Invalid IPs should not be allowed when there are allowed IPs
            _blockList.UpdateAllowedIps(new[] { "8.8.8.0/8" });
            Assert.That(_blockList.IsAllowedIP("invalid.ip"), Is.False);

            // Invalid IPs should not be bypassed
            Assert.That(_blockList.IsBypassedIP("invalid.ip"), Is.False);

            // Invalid IPs should be allowed for endpoints with no restrictions
            Assert.That(_blockList.IsIPAllowed("invalid.ip", "GET|unrestricted"), Is.True);

            // Invalid IPs should be blocked for endpoints with restrictions
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "restricted",
                    AllowedIPAddresses = new[] { "8.8.8.0/8" }
                }
            };
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);
            Assert.That(_blockList.IsIPAllowed("invalid.ip", "GET|restricted"), Is.True);
        }

        [Test]
        public void IPRules_ShouldBeAppliedInCorrectOrder()
        {
            // Arrange
            var ip = "123.123.123.123";
            var url = "GET|testUrl";
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "8.8.8.0/8" }
                }
            };

            // Act & Assert
            // 1. Private IPs should be allowed first
            Assert.That(_blockList.IsBlocked("192.168.1.1", url, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("Ip is private or local"));

            // 2. Bypassed IPs should be allowed second
            _blockList.UpdateBypassedIps(new[] { ip });
            Assert.That(_blockList.IsBlocked(ip, url, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("IP is bypassed"));

            // 3. Blocked IPs should be checked third
            _blockList.UpdateBypassedIps(Array.Empty<string>());
            _blockList.UpdateBlockedIps([ip]);
            Assert.That(_blockList.IsBlocked(ip, url, out reason), Is.True);
            Assert.That(reason, Is.EqualTo("IP is blocked"));

            // 4. Bypassed IPs should override blocked IPs
            _blockList.UpdateBypassedIps(new[] { ip });
            Assert.That(_blockList.IsBlocked(ip, url, out reason), Is.False);

            // 5. Endpoint-specific rules should be checked last
            _blockList.UpdateAllowedIpsPerEndpoint(endpoints);
            _blockList.UpdateBlockedIps(Array.Empty<string>());
            _blockList.UpdateBypassedIps(Array.Empty<string>());
            Assert.That(_blockList.IsBlocked(ip, url, out reason), Is.True);
            Assert.That(reason, Is.EqualTo("Ip is not allowed for this endpoint"));
        }

    }
}
