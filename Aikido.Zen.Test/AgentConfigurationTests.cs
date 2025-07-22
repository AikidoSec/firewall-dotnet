
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using NUnit.Framework;

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

        [Test]
        public void UpdateMonitoredIPAddresses_ShouldSetAndUpdateLists()
        {
            // Arrange
            var monitoredIps1 = new List<FirewallListsAPIResponse.IPList>
            {
                new FirewallListsAPIResponse.IPList { Key = "monitored1", Ips = new[] { "1.1.1.0/24" } }
            };
            var monitoredIps2 = new List<FirewallListsAPIResponse.IPList>
            {
                new FirewallListsAPIResponse.IPList { Key = "monitored2", Ips = new[] { "2.2.2.0/24" } }
            };

            // Act & Assert - Initial Update
            _config.UpdateMonitoredIPAddresses(monitoredIps1);
            var matches1 = _config.GetMatchingMonitoredIPListKeys("1.1.1.1");
            Assert.That(matches1.Count(), Is.EqualTo(1));
            Assert.That(matches1.First(), Is.EqualTo("monitored1"));

            // Act & Assert - Second Update (should replace)
            _config.UpdateMonitoredIPAddresses(monitoredIps2);
            var matches2 = _config.GetMatchingMonitoredIPListKeys("1.1.1.1");
            var matches3 = _config.GetMatchingMonitoredIPListKeys("2.2.2.2");
            Assert.That(matches2.Any(), Is.False);
            Assert.That(matches3.Count(), Is.EqualTo(1));
            Assert.That(matches3.First(), Is.EqualTo("monitored2"));

            // Act & Assert - Clear list
            _config.UpdateMonitoredIPAddresses(null);
            var matches4 = _config.GetMatchingMonitoredIPListKeys("2.2.2.2");
            Assert.That(matches4.Any(), Is.False);
        }

        [Test]
        public void GetMatchingIPListKeys_ShouldReturnCorrectKeysForBlockedAndMonitored()
        {
            // Arrange
            var monitoredIps = new List<FirewallListsAPIResponse.IPList>
            {
                new FirewallListsAPIResponse.IPList { Key = "monitored", Ips = new[] { "1.1.1.0/24" } }
            };
            var blockedIps = new List<FirewallListsAPIResponse.IPList>
            {
                new FirewallListsAPIResponse.IPList { Key = "blocked", Ips = new[] { "2.2.2.0/24" } }
            };
            _config.UpdateMonitoredIPAddresses(monitoredIps);
            _config.BlockList.UpdateIPLists(blockedIps);

            // Act
            var monitoredMatches = _config.GetMatchingMonitoredIPListKeys("1.1.1.1");
            var blockedMatches = _config.GetMatchingBlockedIPListKeys("2.2.2.2");
            var noMonitoredMatches = _config.GetMatchingMonitoredIPListKeys("2.2.2.2");
            var noBlockedMatches = _config.GetMatchingBlockedIPListKeys("1.1.1.1");

            // Assert
            Assert.That(monitoredMatches.First(), Is.EqualTo("monitored"));
            Assert.That(blockedMatches.First(), Is.EqualTo("blocked"));
            Assert.That(noMonitoredMatches.Any(), Is.False);
            Assert.That(noBlockedMatches.Any(), Is.False);
        }

        [Test]
        public void GetMatchingUserAgentKeys_ShouldReturnAllMatchingKeys()
        {
            // Arrange
            var userAgentDetails = new List<UserAgentDetails>
            {
                new UserAgentDetails { Key = "GoogleBot", Pattern = "Googlebot" },
                new UserAgentDetails { Key = "DesktopBrowser", Pattern = "Mozilla" }
            };
            var regexPattern = string.Join("|", userAgentDetails.Select(ud => $"(?<{ud.Key}>{ud.Pattern})"));
            _config.UpdateMonitoredUserAgents(regexPattern);
            _config.UpdateUserAgentDetails(userAgentDetails);

            // Act
            var matches = _config.GetMatchingUserAgentKeys("Mozilla/5.0 (compatible; Googlebot/2.1;)");

            // Assert
            Assert.That(matches.Count(), Is.EqualTo(2));
            Assert.That(matches, Contains.Item("GoogleBot"));
            Assert.That(matches, Contains.Item("DesktopBrowser"));
        }

        [Test]
        public void UpdateMonitoredUserAgents_WithEmptyOrInvalidPattern_ShouldBeHandledGracefully()
        {
            // Arrange
            var userAgentDetails = new List<UserAgentDetails> { new UserAgentDetails { Key = "A", Pattern = "B" } };
            _config.UpdateUserAgentDetails(userAgentDetails);
            var userAgent = "TestUserAgent";

            // Act & Assert - Invalid Pattern
            _config.UpdateMonitoredUserAgents("(?<invalid"); // Invalid regex
            var matches1 = _config.GetMatchingUserAgentKeys(userAgent);
            Assert.That(matches1.Any(), Is.False, "Should not match with invalid regex");

            // Act & Assert - Empty Pattern
            _config.UpdateMonitoredUserAgents(string.Empty);
            var matches2 = _config.GetMatchingUserAgentKeys(userAgent);
            Assert.That(matches2.Any(), Is.False, "Should not match with empty regex");

            // Act & Assert - Null Pattern
            _config.UpdateMonitoredUserAgents(null);
            var matches3 = _config.GetMatchingUserAgentKeys(userAgent);
            Assert.That(matches3.Any(), Is.False, "Should not match with null regex");
        }
    }
}
