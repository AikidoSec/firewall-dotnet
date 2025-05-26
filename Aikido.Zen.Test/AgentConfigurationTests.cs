using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public void UpdateBlockedUsers_WithNullList_ClearsBlockedUsers()
        {
            // Arrange
            _config.UpdateBlockedUsers(new[] { "user1", "user2" });

            // Act
            _config.UpdateBlockedUsers(null);

            // Assert
            Assert.That(_config.IsUserBlocked("user1"), Is.False);
            Assert.That(_config.IsUserBlocked("user2"), Is.False);
        }

        [Test]
        public void UpdateBlockedUsers_WithNewList_UpdatesBlockedUsers()
        {
            // Arrange
            var blockedUsers = new[] { "user1", "user2" };

            // Act
            _config.UpdateBlockedUsers(blockedUsers);

            // Assert
            Assert.That(_config.IsUserBlocked("user1"), Is.True);
            Assert.That(_config.IsUserBlocked("user2"), Is.True);
            Assert.That(_config.IsUserBlocked("user3"), Is.False);
        }

        [Test]
        public void UpdateBlockedUserAgents_WithNullRegex_ClearsBlockedUserAgents()
        {
            // Arrange
            _config.UpdateBlockedUserAgents(new Regex("Mozilla/5.0"));

            // Act
            _config.UpdateBlockedUserAgents(null);

            // Assert
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0"), Is.False);
        }

        [Test]
        public void UpdateBlockedUserAgents_WithNewRegex_UpdatesBlockedUserAgents()
        {
            // Arrange
            var userAgentRegex = new Regex("Mozilla/5.0");

            // Act
            _config.UpdateBlockedUserAgents(userAgentRegex);

            // Assert
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0"), Is.True);
            Assert.That(_config.IsUserAgentBlocked("Chrome/91.0"), Is.False);
        }

        [Test]
        public void UpdateRatelimitedRoutes_WithNullList_ClearsRatelimitedRoutes()
        {
            // Arrange
            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig { Method = "GET", Route = "/api/test" }
            };
            _config.UpdateRatelimitedRoutes(endpoints);

            // Act
            _config.UpdateRatelimitedRoutes(null);

            // Assert
            Assert.That(_config.Endpoints, Is.Empty);
        }

        [Test]
        public void UpdateRatelimitedRoutes_WithNewList_UpdatesRatelimitedRoutes()
        {
            // Arrange
            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig { Method = "GET", Route = "/api/test1" },
                new EndpointConfig { Method = "POST", Route = "/api/test2" }
            };

            // Act
            _config.UpdateRatelimitedRoutes(endpoints);

            // Assert
            var result = _config.Endpoints.ToList();
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].Method, Is.EqualTo("GET"));
            Assert.That(result[0].Route, Is.EqualTo("/api/test1"));
            Assert.That(result[1].Method, Is.EqualTo("POST"));
            Assert.That(result[1].Route, Is.EqualTo("/api/test2"));
        }

        [Test]
        public void UpdateFirewallLists_WithNullResponse_ClearsFirewallLists()
        {
            // Arrange
            var response = new FirewallListsAPIResponse(
                blockedIPAddresses: new[] { new FirewallListsAPIResponse.IPList { Ips = new[] { "192.168.1.1" } } },
                allowedIPAddresses: new[] { new FirewallListsAPIResponse.IPList { Ips = new[] { "example.com" } } },
                blockedUserAgents: "Mozilla/5.0"
            );
            _config.UpdateFirewallLists(response);

            // Act
            _config.UpdateFirewallLists(null);

            // Assert
            Assert.That(_config.BlockList.IsIPBlocked("192.168.1.1"), Is.False);
            Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0"), Is.False);
        }

        [Test]
        public void UpdateFirewallLists_WithNewResponse_UpdatesFirewallLists()
        {
            // Arrange
            var response = new FirewallListsAPIResponse(
                blockedIPAddresses: new[] { new FirewallListsAPIResponse.IPList { Ips = new[] { "192.168.1.1", "10.0.0.1" } } },
                allowedIPAddresses: new[] { new FirewallListsAPIResponse.IPList { Ips = new[] { "example.com", "test.com" } } },
                blockedUserAgents: "Mozilla/5.0"
            );

            // Act
            _config.UpdateFirewallLists(response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_config.BlockList.IsIPBlocked("192.168.1.1"), Is.True);
                Assert.That(_config.BlockList.IsIPBlocked("10.0.0.1"), Is.True);
                Assert.That(_config.BlockList.IsIPBlocked("192.168.1.2"), Is.False);

                Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0"), Is.True);
                Assert.That(_config.IsUserAgentBlocked("Chrome/91.0"), Is.False);
            });
        }

        [Test]
        public void Clear_ResetsAllConfiguration()
        {
            // Arrange
            _config.UpdateBlockedUsers(new[] { "user1" });
            _config.UpdateBlockedUserAgents(new Regex("Mozilla/5.0"));
            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig { Method = "GET", Route = "/api/test" }
            };
            _config.UpdateRatelimitedRoutes(endpoints);
            var response = new FirewallListsAPIResponse(
                blockedIPAddresses: new[] { new FirewallListsAPIResponse.IPList { Ips = new[] { "192.168.1.1" } } },
                allowedIPAddresses: new[] { new FirewallListsAPIResponse.IPList { Ips = new[] { "example.com" } } },
                blockedUserAgents: "Mozilla/5.0"
            );
            _config.UpdateFirewallLists(response);

            // Act
            _config.Clear();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_config.IsUserBlocked("user1"), Is.False);
                Assert.That(_config.IsUserAgentBlocked("Mozilla/5.0"), Is.False);
                Assert.That(_config.Endpoints, Is.Empty);
                Assert.That(_config.BlockList.IsIPBlocked("192.168.1.1"), Is.False);
            });
        }
    }
}
