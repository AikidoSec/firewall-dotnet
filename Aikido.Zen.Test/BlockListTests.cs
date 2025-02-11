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
        public void UpdateBlockedSubnets_ShouldUpdateBlockedSubnetsList()
        {
            // Arrange
            var subnet1 = "192.168.1.0/24";
            var subnet2 = "10.0.0.0/8";
            var subnets = new[] { subnet1, subnet2 };

            // Act
            _blockList.UpdateBlockedSubnets(subnets);

            // Assert
            Assert.That(_blockList.IsIPBlocked("192.168.1.100"));
            Assert.That(_blockList.IsIPBlocked("10.10.10.10"));
            Assert.That(_blockList.IsIPBlocked("172.16.1.1"), Is.False);
        }

        [Test]
        public void UpdateBlockedSubnets_WithEmptyList_ShouldClearBlockedSubnets()
        {
            // Arrange
            var subnet = "192.168.1.0/24";
            _blockList.UpdateBlockedSubnets(new[] { subnet });

            // Act
            _blockList.UpdateBlockedSubnets(Array.Empty<string>());

            // Assert
            Assert.That(_blockList.IsIPBlocked("192.168.1.100"), Is.False);
        }

        [Test]
        public void UpdateAllowedSubnets_ShouldUpdateAllowedSubnetsPerUrl()
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
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Assert
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "GET|url1"));
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "POST|url1"), Is.False);
            Assert.That(_blockList.IsIPAllowed("10.10.10.10", "POST|url2"));
            Assert.That(_blockList.IsIPAllowed("10.10.10.10", "GET|url1"), Is.False);
            Assert.That(_blockList.IsIPAllowed("172.16.1.1", "GET|url3")); // No restrictions for url3
        }

        [Test]
        public void UpdateAllowedSubnets_WithEmptyList_ShouldClearAllowedSubnets()
        {
            // Arrange
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "url1",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                }
            };
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Act
            _blockList.UpdateAllowedForEndpointSubnets(new List<EndpointConfig>());

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
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

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
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Act & Assert
            Assert.That(_blockList.IsIPAllowed("invalid.ip", "GET|testUrl"));
        }

        [Test]
        public void IsBlocked_ShouldCheckAllBlockingConditions()
        {
            // Arrange
            var ip = "192.168.1.100";
            var url = "GET|testUrl";
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = [ "10.0.0.0/8" ]
                }
            };

            _blockList.UpdateBlockedSubnets(new[] { "192.168.1.0/24" });
            _blockList.UpdateAllowedSubnets(new[] { "10.10.10.10" });
            _blockList.AddIpAddressToBlocklist("192.168.1.101");
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Act & Assert
            Assert.That(_blockList.IsBlocked("10.10.10.10", url), Is.False);
            Assert.That(_blockList.IsBlocked("192.168.1.101", url), Is.True);
            Assert.That(_blockList.IsBlocked("10.0.0.1", url), Is.False);
            Assert.That(_blockList.IsBlocked("invalid.ip", url), Is.False);
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
        public void UpdateAllowedSubnets_ShouldUpdateAllowedIPs()
        {
            // Arrange
            var subnets = new[] { "192.168.1.0/24", "10.0.0.0/8" };

            // Act
            _blockList.UpdateAllowedSubnets(subnets);

            // Assert
            Assert.That(_blockList.IsBypassedIP("192.168.1.100"), Is.True);
            Assert.That(_blockList.IsBypassedIP("10.10.10.10"), Is.True);
            Assert.That(_blockList.IsBypassedIP("172.16.1.1"), Is.False);
        }

        [Test]
        public void IsAllowedIp_WithInvalidIP_ShouldReturnFalse()
        {
            // Arrange
            var subnets = new[] { "192.168.1.0/24" };
            _blockList.UpdateAllowedSubnets(subnets);

            // Act & Assert
            Assert.That(_blockList.IsBypassedIP("invalid.ip"), Is.False);
        }

        [Test]
        public void IsAllowedIp_WithNoAllowedSubnets_ShouldReturnFalse()
        {
            // Act & Assert
            Assert.That(_blockList.IsBypassedIP("192.168.1.100"), Is.False);
        }

        [Test]
        public void IsBlocked_AllowedIP_ShouldBypassAllBlocking()
        {
            // Arrange
            var ip = "192.168.1.100";
            var url = "GET|testUrl";

            // Setup blocking conditions
            _blockList.AddIpAddressToBlocklist(ip); // Add to blocklist
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" } // Not in allowed IPs for endpoint
                }
            };
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Add IP to allow
            _blockList.UpdateAllowedSubnets(new[] { "192.168.1.0/24" });

            // Act & Assert
            Assert.That(_blockList.IsBlocked(ip, url), Is.False, "Allowed IP should bypass all blocking");
        }

        [Test]
        public void IsBlocked_AllowedIP_ShouldBypassEndpointRestrictions()
        {
            // Arrange
            var ip = "192.168.1.100";
            var url = "GET|testUrl";

            // Setup endpoint restrictions
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" } // IP not in allowed range
                }
            };
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Add IP to allow
            _blockList.UpdateAllowedSubnets(new[] { "192.168.1.0/24" });

            // Act & Assert
            Assert.That(_blockList.IsBlocked(ip, url), Is.False, "Allowed IP should bypass endpoint restrictions");
        }

        [Test]
        public void UpdateAllowedSubnets_WithEmptyList_ShouldClearAllowedIps()
        {
            // Arrange
            var ip = "192.168.1.100";
            _blockList.UpdateAllowedSubnets(new[] { "192.168.1.0/24" });
            Assert.That(_blockList.IsBypassedIP(ip), Is.True, "IP should be allowed initially");

            // Act
            _blockList.UpdateAllowedSubnets(Array.Empty<string>());

            // Assert
            Assert.That(_blockList.IsBypassedIP(ip), Is.False, "AllowedIp list should be cleared");
        }

        [Test]
        public void IsBlocked_AllowedIPWithBlockedUserAgent_ShouldBypassBlocking()
        {
            // Arrange
            var ip = "192.168.1.100";
            var url = "GET|testUrl";

            // Setup blocking conditions
            _blockList.AddIpAddressToBlocklist(ip);
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                }
            };
            _blockList.UpdateAllowedForEndpointSubnets(endpoints);

            // Add IP to allowed
            _blockList.UpdateAllowedSubnets(new[] { "192.168.1.0/24" });

            // Act & Assert
            Assert.That(_blockList.IsBlocked(ip, url), Is.False, "Allowed IP should bypass all blocking regardless of other conditions");
        }
    }
}
