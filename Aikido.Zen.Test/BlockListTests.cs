using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;
using NetTools;
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
            var subnet1 = IPAddressRange.Parse("192.168.1.0/24");
            var subnet2 = IPAddressRange.Parse("10.0.0.0/8");
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
            var subnet = IPAddressRange.Parse("192.168.1.0/24");
            _blockList.UpdateBlockedSubnets(new[] { subnet });

            // Act
            _blockList.UpdateBlockedSubnets(Array.Empty<IPAddressRange>());

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
            _blockList.UpdateAllowedSubnets(endpoints);

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
            _blockList.UpdateAllowedSubnets(endpoints);

            // Act
            _blockList.UpdateAllowedSubnets(new List<EndpointConfig>());

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
        public void AddIpAddressToBlocklist_WithInvalidIP_ShouldStillAddToBlocklist()
        {
            // Arrange
            var invalidIp = "invalid.ip.address";

            // Act
            _blockList.AddIpAddressToBlocklist(invalidIp);

            // Assert
            Assert.That(_blockList.IsIPBlocked(invalidIp));
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
            _blockList.UpdateAllowedSubnets(endpoints);

            // Act & Assert
            Assert.That(_blockList.IsIPAllowed("192.168.1.100", "GET|testUrl")); // Should allow when no IP restrictions
        }

        [Test]
        public void IsIPAllowed_WithInvalidIP_ShouldReturnTrue()
        {
            // Arrange
            var subnet = IPAddressRange.Parse("192.168.1.0/24");
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "testUrl",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                }
            };
            _blockList.UpdateAllowedSubnets(endpoints);

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
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                }
            };

            _blockList.AddIpAddressToBlocklist("192.168.1.101");
            _blockList.UpdateAllowedSubnets(endpoints);

            // Act & Assert
            Assert.That(_blockList.IsBlocked("192.168.1.101", url)); // Blocked IP
            Assert.That(_blockList.IsBlocked(ip, url)); // Not in allowed subnet
            Assert.That(_blockList.IsBlocked("10.0.0.1", url), Is.False); // In allowed subnet
            Assert.That(_blockList.IsBlocked("invalid.ip", url), Is.False); // Invalid IP should not be blocked
            Assert.That(_blockList.IsBlocked("10.0.0.1", url), Is.False); // Non-blocked user in allowed subnet
        }

        [Test]
        public void ConcurrentAccess_ShouldHandleConcurrentUpdates()
        {
            // Arrange
            var tasks = new List<Task>();
            var ip = "192.168.1.100";

            // Act
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() => _blockList.AddIpAddressToBlocklist(ip)));
                tasks.Add(Task.Run(() => _blockList.UpdateBlockedSubnets(new[] { IPAddressRange.Parse("10.0.0.0/8") })));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.That(_blockList.IsIPBlocked(ip));
        }

        [Test]
        public void LargeBlocklistHandling_ShouldHandleLargeNumberOfIPs()
        {
            // Arrange
            for (int i = 0; i < 10000; i++)
            {
                _blockList.AddIpAddressToBlocklist($"192.168.1.{i}");
            }

            // Act & Assert
            Assert.That(_blockList.IsIPBlocked("192.168.1.9999"));
            Assert.That(_blockList.IsIPBlocked("192.168.2.1"), Is.False);
        }

        [Test]
        public void PerformanceWithDifferentSizes_ShouldPerformEfficiently()
        {
            // Arrange
            var stopwatch = new System.Diagnostics.Stopwatch();
            var ip = "192.168.1.100";

            // Act
            stopwatch.Start();
            for (int i = 0; i < 10000; i++)
            {
                _blockList.AddIpAddressToBlocklist($"192.168.1.{i}");
            }
            stopwatch.Stop();

            // Assert
            Assert.That(stopwatch.ElapsedMilliseconds < 1000); // Ensure it completes within 1 second
        }

    }
}
