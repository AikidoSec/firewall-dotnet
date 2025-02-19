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
        public void BlockedSubnets_BasicOperations()
        {
            // Test adding and clearing blocked subnets
            var subnets = new[] { "192.168.1.0/24", "10.0.0.0/8" };
            _blockList.UpdateBlockedIps(subnets);

            Assert.That(_blockList.IsIPBlocked("192.168.1.100"));
            Assert.That(_blockList.IsIPBlocked("10.10.10.10"));
            Assert.That(_blockList.IsIPBlocked("172.16.1.1"), Is.False);

            // Test clearing blocked subnets
            _blockList.UpdateBlockedIps(Array.Empty<string>());
            Assert.That(_blockList.IsIPBlocked("192.168.1.100"), Is.False);

            // Test adding single IP
            _blockList.AddIpAddressToBlocklist("192.168.1.100");
            Assert.That(_blockList.IsIPBlocked("192.168.1.100"));
            Assert.That(_blockList.IsIPBlocked("192.168.1.101"), Is.False);
        }

        [Test]
        public void AllowedSubnets_EndpointSpecific()
        {
            // Test endpoint-specific allowed subnets
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
                }
            };

            _blockList.UpdateAllowedIpsForEndpoint(endpoints);

            // Test endpoint-specific rules
            Assert.That(_blockList.IsIPAllowedForEndpoint("192.168.1.100", "GET|url1"));
            Assert.That(_blockList.IsIPAllowedForEndpoint("192.168.1.100", "POST|url2"), Is.False);
            Assert.That(_blockList.IsIPAllowedForEndpoint("10.10.10.10", "POST|url2"));
            Assert.That(_blockList.IsIPAllowedForEndpoint("172.16.1.1", "GET|url3")); // No restrictions

            // Test clearing endpoint restrictions
            _blockList.UpdateAllowedIpsForEndpoint(new List<EndpointConfig>());
            Assert.That(_blockList.IsIPAllowedForEndpoint("192.168.1.100", "GET|url1")); // Should allow when no restrictions
        }

        [Test]
        public void GlobalAllowedSubnets_IPv4AndIPv6()
        {
            // Test mixed IPv4 and IPv6 allowed subnets
            var allowedSubnets = new[] {
                "192.168.1.0/24",           // IPv4
                "10.0.0.0/8",               // IPv4
                "2001:db8::/32",            // IPv6
                "fe80::/10",                // IPv6 link-local
                "2001:db8:3333:4444:5555:6666:7777:8888" // Specific IPv6
            };

            _blockList.UpdateAllowedIps(allowedSubnets);

            // Test IPv4 addresses
            Assert.That(_blockList.IsIPAllowed("192.168.1.100"), Is.True);
            Assert.That(_blockList.IsIPAllowed("10.10.10.10"), Is.True);
            Assert.That(_blockList.IsIPAllowed("172.16.1.1"), Is.False);

            // Test IPv6 addresses
            Assert.That(_blockList.IsIPAllowed("2001:db8:3333:4444:5555:6666:7777:8888"), Is.True);
            Assert.That(_blockList.IsIPAllowed("fe80::1234:5678:9abc:def0"), Is.True);
            Assert.That(_blockList.IsIPAllowed("2002:db8:1111:2222:3333:4444:5555:6666"), Is.False);

            // Test empty allowed list
            _blockList.UpdateAllowedIps(Array.Empty<string>());
            Assert.That(_blockList.IsIPAllowed("192.168.1.100"), Is.True);
            Assert.That(_blockList.IsIPAllowed("2001:db8:3333:4444:5555:6666:7777:8888"), Is.True);
        }

        [Test]
        public void InvalidIPHandling()
        {
            // Setup some rules
            _blockList.UpdateBlockedIps(new[] { "192.168.1.0/24" });
            _blockList.UpdateAllowedIps(new[] { "10.0.0.0/8" });

            // Test invalid IPs in different contexts
            var invalidIPs = new[] {
                "invalid.ip.address",
                "256.256.256.256",
                "2001:invalid:ipv6",
                "not_an_ip"
            };

            foreach (var invalidIp in invalidIPs)
            {
                Assert.That(_blockList.IsIPBlocked(invalidIp), Is.False, "Invalid IP should not be blocked");
                Assert.That(_blockList.IsIPAllowedForEndpoint(invalidIp, "GET|testUrl"), Is.True, "Invalid IP should be allowed for endpoints");
                Assert.That(_blockList.IsIPAllowed(invalidIp), Is.False, "Invalid IP should not be allowed globally");
            }
        }

        [Test]
        public void PrivateAndLocalIPHandling()
        {
            // Setup some blocking rules (these should be bypassed for private/local IPs)
            _blockList.UpdateBlockedIps(new[] { "0.0.0.0/0" }); // Try to block everything

            // Test IPv4 private and local addresses
            var ipv4Tests = new[] {
                ("127.0.0.1", "IPv4 Localhost"),
                ("127.0.0.53", "IPv4 Localhost range"),
                ("192.168.1.100", "IPv4 Private Class C"),
                ("10.10.10.10", "IPv4 Private Class A"),
                ("172.16.1.1", "IPv4 Private Class B")
            };

            foreach (var (ip, description) in ipv4Tests)
            {
                Assert.That(_blockList.IsBlocked(ip, "ANY", out string reason), Is.False,
                    $"{description} ({ip}) should never be blocked");
            }

            // Test IPv6 private and local addresses
            var ipv6Tests = new[] {
                ("::1", "IPv6 Localhost"),
                ("fc00::1", "IPv6 Unique Local"),
                ("fd00::1", "IPv6 Unique Local")
            };

            foreach (var (ip, description) in ipv6Tests)
            {
                Assert.That(_blockList.IsBlocked(ip, "ANY", out string reason), Is.False,
                    $"{description} ({ip}) should never be blocked");
            }
        }

        [Test]
        public void Performance_LargeScaleOperations()
        {
            const int SUBNET_COUNT = 100;
            const int TEST_IPS_COUNT = 1000;
            const int MAX_ACCEPTABLE_MS = 100; // More realistic timeout

            // Add a reasonable number of subnets
            var random = new Random(42); // Fixed seed for reproducibility
            for (int i = 0; i < SUBNET_COUNT; i++)
            {
                var firstOctet = random.Next(1, 224); // Valid non-reserved first octet
                var secondOctet = random.Next(0, 256);
                _blockList.AddIpAddressToBlocklist($"{firstOctet}.{secondOctet}.0.0/16");
            }

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            // Test performance with realistic dataset
            int matchCount = 0;
            for (int i = 0; i < TEST_IPS_COUNT; i++)
            {
                var testIp = $"{random.Next(1, 224)}.{random.Next(0, 256)}.{random.Next(0, 256)}.{random.Next(0, 256)}";
                if (_blockList.IsIPBlocked(testIp))
                {
                    matchCount++;
                }
            }

            stopwatch.Stop();

            // Log performance metrics
            Console.WriteLine($"Processed {TEST_IPS_COUNT} IPs against {SUBNET_COUNT} subnets");
            Console.WriteLine($"Found {matchCount} matches");
            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per IP: {(double)stopwatch.ElapsedMilliseconds / TEST_IPS_COUNT:F3}ms");

            Assert.That(stopwatch.ElapsedMilliseconds, Is.AtMost(MAX_ACCEPTABLE_MS),
                $"Large scale operations should complete within {MAX_ACCEPTABLE_MS}ms");

            // Verify accuracy with known IPs
            var knownBlockedIP = $"192.168.1.1";
            _blockList.AddIpAddressToBlocklist(knownBlockedIP);
            Assert.That(_blockList.IsIPBlocked(knownBlockedIP), Is.True,
                "Should correctly identify known blocked IP");

            var knownAllowedIP = "8.8.8.8";
            Assert.That(_blockList.IsIPBlocked(knownAllowedIP), Is.False,
                "Should correctly identify known allowed IP");
        }

        [Test]
        public void IsBlocked_ShouldProvideCorrectReason()
        {
            // Test private/local IP - these should always be allowed regardless of other rules
            var privateAndLocalIps = new[] {
                "127.0.0.1",      // IPv4 localhost
                "::1",            // IPv6 localhost
                "192.168.1.100",  // Class C private
                "10.10.10.10",    // Class A private
                "172.16.1.1"      // Class B private
            };

            // Set up restrictive rules that should not affect private/local IPs
            _blockList.UpdateBlockedIps(new[] { "0.0.0.0/0" }); // Try to block everything
            _blockList.UpdateAllowedIps(new[] { "8.8.8.8/32" }); // Only allow Google DNS

            foreach (var ip in privateAndLocalIps)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(_blockList.IsBlocked(ip, "ANY", out string reason), Is.False);
                    Assert.That(reason, Is.EqualTo("Private or local IP"));
                });
            }

            // Now test public IPs with various rules
            _blockList.UpdateBlockedIps(new[] { "123.0.113.0/24" });
            _blockList.UpdateAllowedIps(new[] { "123.51.100.0/24", "203.0.113.1", "8.8.4.4", "123.0.113.1" });

            // Test blocked IP
            Assert.Multiple(() =>
            {
                Assert.That(_blockList.IsBlocked("123.0.113.1", "ANY", out string reason), Is.True);
                Assert.That(reason, Is.EqualTo("IP is blocked"));
            });

            // Test IP not allowed
            Assert.Multiple(() =>
            {
                Assert.That(_blockList.IsBlocked("8.8.8.8", "ANY", out string reason), Is.True);
                Assert.That(reason, Is.EqualTo("IP is not allowed"));
            });

            // Test IP not allowed for endpoint
            var endpoints = new List<EndpointConfig> {
                new EndpointConfig {
                    Method = "GET",
                    Route = "url1",
                    AllowedIPAddresses = new[] { "198.51.100.0/24" }
                }
            };
            _blockList.UpdateAllowedIpsForEndpoint(endpoints);
            Assert.Multiple(() =>
            {
                Assert.That(_blockList.IsBlocked("8.8.4.4", "GET|url1", out string reason), Is.True);
                Assert.That(reason, Is.EqualTo("IP is not allowed for endpoint"));
            });

            // Test allowed IP
            Assert.Multiple(() =>
            {
                Assert.That(_blockList.IsBlocked("123.51.100.1", "ANY", out string reason), Is.False);
                Assert.That(reason, Is.EqualTo("IP is allowed"));
            });
        }
    }
}
