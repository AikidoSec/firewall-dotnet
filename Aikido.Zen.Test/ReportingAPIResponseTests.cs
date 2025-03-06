using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Test
{
    public class ReportingAPIResponseTests
    {
        [Test]
        public void Constructor_CreatesValidInstance()
        {
            // Act
            var response = new ReportingAPIResponse();

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public void Properties_SetAndGetCorrectly()
        {
            // Arrange
            var response = new ReportingAPIResponse
            {
                ConfigUpdatedAt = 1234567890,
                HeartbeatIntervalInMS = 5000,
                Endpoints = new List<EndpointConfig>
                {
                    new EndpointConfig {
                        Route = "/test",
                        Method = "GET"
                    }
                },
                BlockedUserIds = new[] { "user1", "user2" },
                BypassedIPAddresses = new[] { "1.1.1.1", "2.2.2.2" },
                ReceivedAnyStats = true,
                Success = true
            };

            // Assert
            Assert.That(response.ConfigUpdatedAt, Is.EqualTo(1234567890));
            Assert.That(response.HeartbeatIntervalInMS, Is.EqualTo(5000));
            Assert.That(response.Endpoints.Count(), Is.EqualTo(1));
            Assert.That(response.Endpoints.First().Route, Is.EqualTo("/test"));
            Assert.That(response.BlockedUserIds, Has.Exactly(2).Items);
            Assert.That(response.BlockedUserIds, Contains.Item("user1"));
            Assert.That(response.BlockedUserIds, Contains.Item("user2"));
            Assert.That(response.BypassedIPAddresses, Has.Exactly(2).Items);
            Assert.That(response.BypassedIPAddresses, Contains.Item("1.1.1.1"));
            Assert.That(response.BypassedIPAddresses, Contains.Item("2.2.2.2"));
            Assert.That(response.ReceivedAnyStats, Is.True);
            Assert.That(response.Success, Is.True);
        }

        [Test]
        public void Properties_WithNullCollections_HandledGracefully()
        {
            // Arrange
            var response = new ReportingAPIResponse
            {
                Endpoints = null,
                BlockedUserIds = null,
                BypassedIPAddresses = null
            };

            // Assert
            Assert.That(response.Endpoints, Is.Null);
            Assert.That(response.BlockedUserIds, Is.Null);
            Assert.That(response.BypassedIPAddresses, Is.Null);
        }
    }
}
