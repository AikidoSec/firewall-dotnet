using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
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
            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                new Context
                {
                    Method = "GET",
                    Route = "/outbound",
                    Url = "https://app.local/outbound",
                    RemoteAddress = "203.0.113.10"
                }));

            // Assert
            Assert.That(exception.Message, Is.EqualTo("Zen has blocked an outbound connection to blocked.example"));
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
            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                new Context
                {
                    Method = "GET",
                    Route = "/outbound",
                    Url = "https://app.local/outbound",
                    RemoteAddress = "203.0.113.10"
                }));

            // Assert
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "blocked.example" && h.Port == 443), Is.True);
        }

        [Test]
        public async Task Inspect_WhenDryMode_SsrfIsReportedButNotBlocked()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");

            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", "http://127.0.0.1/admin" }
                }
            };

            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://127.0.0.1/admin"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.That(context.AttackDetected, Is.True);

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == "query" &&
                        ((DetectedAttack)evt).Attack.Path == ".url"),
                    It.IsAny<int>()),
                Times.Once);
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
            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("https://blocked.example/path"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            // Assert
            Assert.That(exception.Message, Is.EqualTo("Zen has blocked an outbound connection to blocked.example"));
        }

        [Test]
        public async Task Inspect_WhenForceProtectionOffRoute_SsrfIsNotReportedOrBlocked()
        {
            _agent.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "POST",
                    Route = "/api/stored_ssrf",
                    ForceProtectionOff = true
                }
            });

            var context = new Context
            {
                Method = "POST",
                Route = "/api/stored_ssrf",
                Url = "https://app.local/api/stored_ssrf",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.url", "http://127.0.0.1/admin" }
                }
            };

            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://127.0.0.1/admin"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            await Task.Delay(150);

            Assert.That(context.AttackDetected, Is.False);
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(0));
            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt => evt is DetectedAttack),
                    It.IsAny<int>()),
                Times.Never);
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
            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://localhost:3000/api/runtime/events"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                null));

            // Assert
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(0));
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
            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://domain1.example.com/test"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            // Assert
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "domain1.example.com"), Is.False);
        }

        [Test]
        public async Task Inspect_WhenDirectPrivateIpComesFromUserInput_ReportsSsrf()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", "http://127.0.0.1/admin" }
                }
            };

            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("http://127.0.0.1/admin"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.Multiple(() =>
            {
                Assert.That(context.AttackDetected, Is.True);
                Assert.That(exception.Message, Does.Contain("server-side request forgery"));
            });

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == "query" &&
                        ((DetectedAttack)evt).Attack.Path == ".url" &&
                        ((DetectedAttack)evt).Attack.Payload == "http://127.0.0.1/admin" &&
                        ((DetectedAttack)evt).Request != null &&
                        ((DetectedAttack)evt).Request.Url == context.Url &&
                        ((DetectedAttack)evt).Attack.Metadata["hostname"].Equals("127.0.0.1") &&
                        ((DetectedAttack)evt).Attack.Metadata["port"].Equals("80")),
                    It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public async Task Inspect_WhenHostnameResolvesToPrivateIpAndMatchesUserInput_ReportsSsrf()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.image", "http://localtest.me/admin" }
                }
            };

            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("http://localtest.me/admin"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.That(exception.Message, Does.Contain("server-side request forgery"));

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == "body" &&
                        ((DetectedAttack)evt).Attack.Path == ".image" &&
                        ((DetectedAttack)evt).Attack.Metadata["hostname"].Equals("localtest.me") &&
                        ((DetectedAttack)evt).Attack.Metadata["port"].Equals("80") &&
                        (((DetectedAttack)evt).Attack.Metadata["privateIP"].Equals("127.0.0.1") ||
                         ((DetectedAttack)evt).Attack.Metadata["privateIP"].Equals("::1"))),
                    It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public async Task Inspect_WhenPrivateTargetWasReachedThroughRedirect_ReportsSsrf()
        {
            var sourceUri = new Uri("http://ssrf-redirects.testssandbox.com/ssrf-test-4");
            var destinationUri = new Uri("http://127.0.0.1:4000/");
            var context = new Context
            {
                Method = "POST",
                Route = "/api/request",
                Url = "http://localhost:4000/api/request",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.url", sourceUri.AbsoluteUri }
                },
                OutgoingRequestRedirects = new List<Context.RedirectInfo>
                {
                    new Context.RedirectInfo(sourceUri, destinationUri)
                }
            };

            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                destinationUri,
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.Multiple(() =>
            {
                Assert.That(context.AttackDetected, Is.True);
                Assert.That(exception.Message, Does.Contain("server-side request forgery"));
            });

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == "body" &&
                        ((DetectedAttack)evt).Attack.Path == ".url" &&
                        ((DetectedAttack)evt).Attack.Payload == sourceUri.AbsoluteUri &&
                        ((DetectedAttack)evt).Attack.Metadata["hostname"].Equals("127.0.0.1") &&
                        ((DetectedAttack)evt).Attack.Metadata["port"].Equals("4000")),
                    It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public async Task Inspect_WhenUnicodeHostnameNormalizesToLocalhost_ReportsSsrf()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/api/request",
                Url = "http://localhost:4000/api/request",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.url", "http://ⓛocalhost:4000/" }
                }
            };

            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("http://ⓛocalhost:4000/"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.Multiple(() =>
            {
                Assert.That(context.AttackDetected, Is.True);
                Assert.That(exception.Message, Does.Contain("server-side request forgery"));
            });

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == "body" &&
                        ((DetectedAttack)evt).Attack.Path == ".url" &&
                        ((DetectedAttack)evt).Attack.Payload == "http://ⓛocalhost:4000/" &&
                        ((DetectedAttack)evt).Attack.Metadata["hostname"].Equals("ⓛocalhost") &&
                        (((DetectedAttack)evt).Attack.Metadata["privateIP"].Equals("127.0.0.1") ||
                         ((DetectedAttack)evt).Attack.Metadata["privateIP"].Equals("::1"))),
                    It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public async Task Inspect_WhenUserInputMatchesHostButUsesDifferentPort_DoesNotReportSsrf()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.image", "http://127.0.0.1:4001/admin" }
                }
            };

            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://127.0.0.1:4000/admin"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.That(context.AttackDetected, Is.False);

            await Task.Delay(100);

            _reportingApiMock.Verify(
                r => r.ReportAsync(It.IsAny<string>(), It.Is<object>(evt => evt is DetectedAttack), It.IsAny<int>()),
                Times.Never);
        }

        [Test]
        public async Task Inspect_WhenHostnameResolvesToImdsWithoutSource_ReportsStoredSsrfWithRequestContext()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.image", "test.png" }
                }
            };

            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("http://100.100.100.200.nip.io/latest/meta-data"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.Multiple(() =>
            {
                Assert.That(
                    exception.Message,
                    Is.EqualTo("Zen has blocked a stored server-side request forgery: HttpClient.SendAsync originating from unknown source"));
            });

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "stored_ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == null &&
                        ((DetectedAttack)evt).Attack.Path == string.Empty &&
                        ((DetectedAttack)evt).Attack.Payload == null &&
                        ((DetectedAttack)evt).Request != null &&
                        ((DetectedAttack)evt).Request.Url == context.Url &&
                        ((DetectedAttack)evt).Request.Method == context.Method &&
                        ((DetectedAttack)evt).Request.Route == context.Route &&
                        ((DetectedAttack)evt).Request.IpAddress == context.RemoteAddress &&
                        ((DetectedAttack)evt).Attack.Metadata["hostname"].Equals("100.100.100.200.nip.io") &&
                        ((DetectedAttack)evt).Attack.Metadata["privateIP"].Equals("100.100.100.200")),
                    It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public async Task Inspect_WhenHostnameResolvesToImdsWithoutContext_ReportsStoredSsrfWithoutRequest()
        {
            var exception = Assert.Throws<AikidoException>(() => OutboundRequestPatcher.Inspect(
                new Uri("http://100.100.100.200.nip.io/latest/meta-data"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                null));

            Assert.That(
                exception.Message,
                Is.EqualTo("Zen has blocked a stored server-side request forgery: HttpClient.SendAsync originating from unknown source"));

            await Task.Delay(150);

            _reportingApiMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<object>(evt =>
                        evt is DetectedAttack &&
                        ((DetectedAttack)evt).Attack.Kind == "stored_ssrf" &&
                        ((DetectedAttack)evt).Attack.Source == null &&
                        ((DetectedAttack)evt).Attack.Path == string.Empty &&
                        ((DetectedAttack)evt).Attack.Payload == null &&
                        ((DetectedAttack)evt).Request == null &&
                        ((DetectedAttack)evt).Attack.Metadata["hostname"].Equals("100.100.100.200.nip.io") &&
                        ((DetectedAttack)evt).Attack.Metadata["privateIP"].Equals("100.100.100.200")),
                    It.IsAny<int>()),
                Times.Once);
        }

        [Test]
        public async Task Inspect_WhenHostnameIsRequestToItself_DoesNotReportSsrf()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "http://app.local:4000/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "headers.Host", "app.local" }
                }
            };

            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://app.local:4000/private"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.That(context.AttackDetected, Is.False);

            await Task.Delay(100);

            _reportingApiMock.Verify(
                r => r.ReportAsync(It.IsAny<string>(), It.Is<object>(evt => evt is DetectedAttack), It.IsAny<int>()),
                Times.Never);
        }

        [Test]
        public async Task Inspect_WhenHostnameLooksLikeServiceHostname_DoesNotReportSsrf()
        {
            var context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "body.url", "http://backend/private" }
                }
            };

            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://backend/private"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                context));

            Assert.That(context.AttackDetected, Is.False);

            await Task.Delay(100);

            _reportingApiMock.Verify(
                r => r.ReportAsync(It.IsAny<string>(), It.Is<object>(evt => evt is DetectedAttack), It.IsAny<int>()),
                Times.Never);
        }

        [Test]
        public void Inspect_WhenImdsHostnameIsTrusted_DoesNotReportStoredSsrf()
        {
            Assert.DoesNotThrow(() => OutboundRequestPatcher.Inspect(
                new Uri("http://metadata.google.internal/computeMetadata/v1"),
                "HttpClient.SendAsync",
                "System.Net.Http",
                null));

            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(0));
        }
    }
}
