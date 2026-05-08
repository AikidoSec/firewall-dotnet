using System.Net.Http;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class OutboundRequestSinkTests
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
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
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
            var result = OutboundRequestSink.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                new Context
                {
                    Method = "GET",
                    Route = "/outbound",
                    Url = "https://app.local/outbound",
                    RemoteAddress = "203.0.113.10"
                });

            // Assert
            Assert.That(result.ShouldProceed, Is.False);
            Assert.That(result.AttackDetected, Is.False);
            Assert.That(result.Blocked, Is.True);
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(0));
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "blocked.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void Inspect_WhenDryMode_DomainRuleDoesNotBlock()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            // Act
            var result = OutboundRequestSink.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                new Context
                {
                    Method = "GET",
                    Route = "/outbound",
                    Url = "https://app.local/outbound",
                    RemoteAddress = "203.0.113.10"
                });

            // Assert
            Assert.That(result.ShouldProceed, Is.True);
            Assert.That(result.Blocked, Is.False);
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
            var result = OutboundRequestSink.Inspect(
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
            var result = OutboundRequestSink.Inspect(
                new Uri("http://localhost:3000/api/runtime/events"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                null);

            // Assert
            Assert.That(result.ShouldProceed, Is.True);
            Assert.That(result.Blocked, Is.False);
        }

        [Test]
        public void Inspect_WhenContextIsBypassed_CapturesHostnameWithoutBlocking()
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
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "domain1.example.com", Mode = "block" }
            });

            var context = new Context
            {
                Bypassed = true,
                RemoteAddress = "1.2.3.4",
                Method = "POST",
                Route = "/api/request",
                Url = "http://app.local/api/request"
            };

            // Act
            var result = OutboundRequestSink.Inspect(
                new Uri("http://domain1.example.com/test"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context);

            // Assert
            Assert.That(result.ShouldProceed, Is.True);
            Assert.That(result.Blocked, Is.False);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "domain1.example.com" && h.Port == 80), Is.True);
        }

        [Test]
        public void OnRequest_WhenDomainRuleBlocks_ThrowsOutboundBlockedException()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            using var httpClient = new HttpClient { BaseAddress = new Uri("https://blocked.example") };
            var request = new HttpRequestMessage(HttpMethod.Get, "/path");

            // Act / Assert
            var exception = Assert.Throws<AikidoException>(() => OutboundRequestSink.OnRequest(
                new object[] { request, CancellationToken.None },
                GetHttpClientSendAsyncMethod(),
                httpClient));

            Assert.That(exception?.Message, Does.Contain("blocked.example"));
        }

        [Test]
        public void OnRequest_WithAbsoluteHttpClientRequest_CapturesRequestUri()
        {
            // Arrange
            using var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://absolute.example/path");

            // Act
            var result = OutboundRequestSink.OnRequest(
                new object[] { request, CancellationToken.None },
                GetHttpClientSendAsyncMethod(),
                httpClient);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "absolute.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WithBaseAddressAndNoRequest_CapturesBaseAddress()
        {
            // Arrange
            using var httpClient = new HttpClient { BaseAddress = new Uri("https://base-only.example") };

            // Act
            var result = OutboundRequestSink.OnRequest(
                Array.Empty<object>(),
                GetHttpClientSendAsyncMethod(),
                httpClient);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "base-only.example" && h.Port == 443), Is.True);
        }

        private static MethodInfo GetHttpClientSendAsyncMethod()
        {
            return typeof(HttpClient).GetMethod(
                nameof(HttpClient.SendAsync),
                new[] { typeof(HttpRequestMessage), typeof(CancellationToken) })!;
        }
    }
}
