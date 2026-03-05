using System;
using System.Linq;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class OutboundRequestPatcherTests
    {
        private Mock<IReportingAPIClient> _reportingApiMock;
        private Mock<IRuntimeAPIClient> _runtimeApiMock;
        private Agent _agent;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            Environment.SetEnvironmentVariable("AIKIDO_URL", "http://localhost:3000");
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "http://localhost:3000");

            _reportingApiMock = new Mock<IReportingAPIClient>();
            _reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            _runtimeApiMock = new Mock<IRuntimeAPIClient>();
            _runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            _agent = Agent.NewInstance(ZenApiMock.CreateMock(_reportingApiMock.Object, _runtimeApiMock.Object).Object);
            _agent.ClearContext();
        }

        [TearDown]
        public void TearDown()
        {
            _agent?.Dispose();
        }

        [Test]
        public void Inspect_WhenDomainRuleBlocks_CapturesHostnameWithoutAttack()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            // Act
            var result = OutboundRequestPatcher.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                null);

            // Assert
            Assert.That(result.ShouldProceed, Is.False);
            Assert.That(result.AttackDetected, Is.False);
            Assert.That(result.Blocked, Is.True);
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(0));
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "blocked.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void Inspect_WhenForceProtectionOffRoute_DomainBlockingStillApplies()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(true, new[]
            {
                new OutboundDomainConfig { Hostname = "allowed.example", Mode = "allow" }
            });
            _agent.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "/outbound",
                    ForceProtectionOff = true
                }
            });

            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10"
            };

            // Act
            var result = OutboundRequestPatcher.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context);

            // Assert
            Assert.That(result.ShouldProceed, Is.False);
            Assert.That(result.Blocked, Is.True);
        }

        [Test]
        public void Inspect_WhenRequestTargetsConfiguredAikidoCore_DoesNotBlock()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(true, new[]
            {
                new OutboundDomainConfig { Hostname = "safe.example", Mode = "allow" }
            });

            // Act
            var result = OutboundRequestPatcher.Inspect(
                new Uri("http://localhost:3000/api/runtime/events"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                null);

            // Assert
            Assert.That(result.ShouldProceed, Is.True);
            Assert.That(result.Blocked, Is.False);
        }

        [Test]
        public void Inspect_WhenRequestIsFromBypassedIp_DoesNotCaptureHostname()
        {
            // Arrange
            _agent.Context.Config.UpdateConfig(new ReportingAPIResponse
            {
                ConfigUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Block = true,
                Endpoints = new EndpointConfig[0],
                BlockedUserIds = new string[0],
                BypassedIPAddresses = new[] { "1.2.3.4" }
            });

            var context = new Context
            {
                RemoteAddress = "1.2.3.4",
                Method = "POST",
                Route = "/api/request",
                Url = "http://app.local/api/request"
            };

            // Act
            var result = OutboundRequestPatcher.Inspect(
                new Uri("http://domain1.example.com/test"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context);

            // Assert
            Assert.That(result.ShouldProceed, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "domain1.example.com"), Is.False);
        }
    }
}
