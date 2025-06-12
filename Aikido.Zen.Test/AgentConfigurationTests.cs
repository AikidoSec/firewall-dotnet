
using System.Text.RegularExpressions;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class AgentConfigurationTests
    {
        private AgentConfiguration _config;

        [SetUp]
        public void Setup()
        {
            _config = new AgentConfiguration();
        }

        [Test]
        public void Clear_ClearsAllCollections()
        {
            // Arrange
            _config.UpdateBlockedUserAgents(new Regex("bot", RegexOptions.IgnoreCase));
            _config.UpdateConfig(new ReportingAPIResponse
            {
                Block = true,
                BlockedUserIds = new List<string> { "123" },
                Endpoints = [ new EndpointConfig {
                    AllowedIPAddresses = ["234.234.234.234"],
                    Route = "/test",
                }],
                BypassedIPAddresses = ["123.123.123.123"],
                ConfigUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            var context = new Context
            {
                Method = "GET",
                Route = "/test",
                Url = "/test",
                RemoteAddress = "234.234.234.234"
            };

            // Act
            _config.Clear();

            // Assert
            Assert.That(_config.IsUserBlocked("123"), Is.False);
            Assert.That(_config.BlockedUserAgents, Is.Null);
            Assert.That(_config.Endpoints, Is.Empty);
            Assert.That(_config.BlockList.IsIPBypassed("123.123.123.123"), Is.False);
            Assert.That(_config.BlockList.IsEmpty(), Is.True);
        }

        [Test]
        public void IsUserBlocked_WithBlockedUser_ReturnsTrue()
        {
            // Arrange
            var userId = "123";
            _config.UpdateBlockedUsers(new List<string> { userId });

            // Act & Assert
            Assert.That(_config.IsUserBlocked(userId), Is.True);
        }

        [Test]
        public void IsUserBlocked_WithNonBlockedUser_ReturnsFalse()
        {
            // Arrange
            var userId = "123";
            _config.UpdateBlockedUsers(new List<string> { "456" });

            // Act & Assert
            Assert.That(_config.IsUserBlocked(userId), Is.False);
        }

        [Test]
        public void IsUserAgentBlocked_WithBlockedUserAgent_ReturnsTrue()
        {
            // Arrange
            var blockedPattern = new Regex("bot", RegexOptions.IgnoreCase);
            _config.UpdateBlockedUserAgents(blockedPattern);

            // Act & Assert
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0 (compatible; Bot/1.0)"), Is.True);
        }

        [Test]
        public void IsUserAgentBlocked_WithNonBlockedUserAgent_ReturnsFalse()
        {
            // Arrange
            var blockedPattern = new Regex("bot", RegexOptions.IgnoreCase);
            _config.UpdateBlockedUserAgents(blockedPattern);

            // Act & Assert
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0 (Windows NT 10.0; Win64; x64)"), Is.False);
        }

        [Test]
        public void UpdateConfig_WithValidResponse_UpdatesConfiguration()
        {
            // Arrange
            var response = new ReportingAPIResponse
            {
                Block = true,
                BlockedUserIds = new List<string> { "123" },
                Endpoints = new List<EndpointConfig>(),
                BypassedIPAddresses = new List<string>(),
                ConfigUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Act
            _config.UpdateConfig(response);

            // Assert
            Assert.That(_config.ConfigLastUpdated, Is.EqualTo(response.ConfigUpdatedAt));
            Assert.That(_config.IsUserBlocked("123"), Is.True);
        }

        [Test]
        public void UpdateFirewallLists_WithValidResponse_UpdatesFirewallLists()
        {
            // Arrange
            var blockedIpList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "Test blocked IPs",
                Ips = new List<string> { "192.168.1.1" }
            };
            var allowedIpList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "Test allowed IPs",
                Ips = new List<string> { "10.0.0.1" }
            };
            var response = new FirewallListsAPIResponse(
                blockedIPAddresses: new[] { blockedIpList },
                allowedIPAddresses: new[] { allowedIpList },
                blockedUserAgents: "bot"
            );

            // Act
            _config.UpdateFirewallLists(response);

            // Assert
            Assert.That(_config.BlockedUserAgents, Is.Not.Null);
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0 (compatible; Bot/1.0)"), Is.True);
        }

        [Test]
        public void UpdateFirewallLists_WithNullResponse_ClearsFirewallLists()
        {
            // Arrange
            var blockedIpList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "Test blocked IPs",
                Ips = new List<string> { "192.168.1.1" }
            };
            var allowedIpList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "Test allowed IPs",
                Ips = new List<string> { "10.0.0.1" }
            };
            var initialResponse = new FirewallListsAPIResponse(
                blockedIPAddresses: new[] { blockedIpList },
                allowedIPAddresses: new[] { allowedIpList },
                blockedUserAgents: "bot"
            );
            _config.UpdateFirewallLists(initialResponse);

            // Act
            _config.UpdateFirewallLists(null);

            // Assert
            Assert.That(_config.BlockedUserAgents, Is.Null);
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0 (compatible; Bot/1.0)"), Is.False);
        }
    }
}
