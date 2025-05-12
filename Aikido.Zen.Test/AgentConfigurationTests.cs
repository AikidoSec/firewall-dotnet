using System;
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
        public void AddHostname_WithValidHostname_AddsToHostnames()
        {
            // Arrange
            var hostname = "example.com:8080";

            // Act
            _config.AddHostname(hostname);

            // Assert
            var hostnames = _config.Hostnames;
            Assert.That(hostnames.Count(), Is.EqualTo(1));
            var host = hostnames.First();
            Assert.That(host.Hostname, Is.EqualTo("example.com"));
            Assert.That(host.Port, Is.EqualTo(8080));
        }

        [Test]
        public void AddHostname_WithInvalidHostname_DoesNotAddToHostnames()
        {
            // Arrange
            var hostname = "";

            // Act
            _config.AddHostname(hostname);

            // Assert
            Assert.That(_config.Hostnames, Is.Empty);
        }

        [Test]
        public void AddUser_WithValidUser_AddsToUsers()
        {
            // Arrange
            var user = new User("123", "Test User");
            var ipAddress = "192.168.1.1";

            // Act
            _config.AddUser(user, ipAddress);

            // Assert
            var users = _config.Users;
            Assert.That(users?.Count(), Is.EqualTo(1));
            var addedUser = users.First();
            Assert.That(addedUser.Id, Is.EqualTo("123"));
            Assert.That(addedUser.Name, Is.EqualTo("Test User"));
            Assert.That(addedUser.LastIpAddress, Is.EqualTo(ipAddress));
            Assert.That(addedUser.LastSeenAt, Is.GreaterThan(0));
        }

        [Test]
        public void AddUser_WithInvalidUser_DoesNotAddToUsers()
        {
            // Arrange
            User user = null;
            var ipAddress = "192.168.1.1";

            // Act
            _config.AddUser(user, ipAddress);

            // Assert
            Assert.That(_config.Users, Is.Empty);
        }

        [Test]
        public void AddRoute_WithValidContext_AddsToRoutes()
        {
            // Arrange
            var context = new Aikido.Zen.Core.Context
            {
                Route = "/api/test",
                Method = "GET"
            };

            // Act
            _config.AddRoute(context);

            // Assert
            var routes = _config.Routes;
            Assert.That(routes.Count(), Is.EqualTo(1));
            var route = routes.First();
            Assert.That(route.Path, Is.EqualTo("/api/test"));
            Assert.That(route.Method, Is.EqualTo("GET"));
        }

        [Test]
        public void AddRoute_WithInvalidContext_DoesNotAddToRoutes()
        {
            // Arrange
            Aikido.Zen.Core.Context context = null;

            // Act
            _config.AddRoute(context);

            // Assert
            Assert.That(_config.Routes, Is.Empty);
        }

        [Test]
        public void Clear_ClearsAllCollections()
        {
            // Arrange
            _config.AddHostname("example.com");
            _config.AddUser(new User("123", "Test User"), "192.168.1.1");
            _config.AddRoute(new Aikido.Zen.Core.Context { Route = "/api/test", Method = "GET" });
            _config.UpdateBlockedUsers(new List<string> { "123" });

            // Act
            _config.Clear();

            // Assert
            Assert.That(_config.Hostnames, Is.Empty);
            Assert.That(_config.Users, Is.Empty);
            Assert.That(_config.Routes, Is.Empty);
            Assert.That(_config.IsUserBlocked("123"), Is.False);
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
