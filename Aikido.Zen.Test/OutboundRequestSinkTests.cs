using System.Net;
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
        private Context? _activeContext;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            Environment.SetEnvironmentVariable("AIKIDO_URL", "http://localhost:3000");
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "http://localhost:3001");

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
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            _agent?.Dispose();
        }

        [Test]
        public void OnRequest_WhenDomainRuleBlocks_CapturesHostnameWithoutAttack()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            // Act / Assert
            var exception = Assert.Throws<AikidoException>(() => OnRequest(
                new Uri("https://blocked.example/path"),
                GetHttpClientSendAsyncMethod(),
                CreateContext()));

            Assert.That(exception?.Message, Does.Contain("blocked.example"));
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(0));
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "blocked.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WhenDryMode_DomainRuleDoesNotBlock()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            // Act
            var result = OnRequest(
                new Uri("https://blocked.example/path"),
                GetHttpClientSendAsyncMethod(),
                CreateContext());

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "blocked.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WhenForceProtectionOffRoute_SkipsDomainBlocking()
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
            var result = OnRequest(
                new Uri("https://blocked.example/path"),
                GetHttpClientSendAsyncMethod(),
                context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "blocked.example" && h.Port == 443), Is.False);
        }

        [Test]
        public void OnRequest_WhenForceProtectionOffRouteHasNoDomainBlock_SkipsSink()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(false, Array.Empty<OutboundDomainConfig>());
            _agent.Context.Config.UpdateRatelimitedRoutes(new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "/outbound",
                    ForceProtectionOff = true
                }
            });

            var context = CreateContext();

            // Act
            var result = OnRequest(
                new Uri("https://safe.example/path"),
                GetHttpClientSendAsyncMethod(),
                context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "safe.example" && h.Port == 443), Is.False);
        }

        [Test]
        public void OnRequest_WhenAikidoCoreHostAllowedByRules_DoesNotBlock()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(true, new[]
            {
                new OutboundDomainConfig { Hostname = "localhost", Mode = "allow" }
            });

            // Act
            var result = OnRequest(
                new Uri("http://localhost:3000/api/runtime/events"),
                GetHttpClientSendAsyncMethod(),
                null);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "localhost" && h.Port == 3000), Is.True);
        }

        [Test]
        public void OnRequest_WhenAikidoRealtimeHostAllowedByRules_DoesNotBlock()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(true, new[]
            {
                new OutboundDomainConfig { Hostname = "localhost", Mode = "allow" }
            });

            // Act
            var result = OnRequest(
                new Uri("http://localhost:3001/config"),
                GetHttpClientSendAsyncMethod(),
                null);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "localhost" && h.Port == 3001), Is.True);
        }

        [Test]
        public void OnRequest_WhenAikidoHostIsNotAllowedByRules_Blocks()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(true, new[]
            {
                new OutboundDomainConfig { Hostname = "safe.example", Mode = "allow" }
            });

            // Act / Assert
            var exception = Assert.Throws<AikidoException>(() => OnRequest(
                new Uri("http://localhost:3002/api/runtime/events"),
                GetHttpClientSendAsyncMethod(),
                null));

            Assert.That(exception?.Message, Does.Contain("localhost"));
        }

        [Test]
        public void OnRequest_WhenAgentHttpRequestScopeIsActive_CapturesHostnameWithoutBlocking()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(true, new[]
            {
                new OutboundDomainConfig { Hostname = "safe.example", Mode = "allow" }
            });

            // Act
            bool result;
            using (AgentHttpRequestScope.Enter())
            {
                result = OnRequest(
                    new Uri("http://10.0.0.1:3000/api/runtime/events"),
                    GetHttpClientSendAsyncMethod(),
                    null);
            }

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "10.0.0.1" && h.Port == 3000), Is.True);
        }

        [Test]
        public void OnRequest_WhenContextIsBypassed_SkipsSink()
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
            var result = OnRequest(
                new Uri("http://domain1.example.com/test"),
                GetHttpClientSendAsyncMethod(),
                context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "domain1.example.com" && h.Port == 80), Is.False);
        }

        [Test]
        public void OnRequest_WhenDomainRuleBlocks_ThrowsOutboundBlockedException()
        {
            // Arrange
            _agent.Context.Config.UpdateOutboundDomains(false, new[]
            {
                new OutboundDomainConfig { Hostname = "blocked.example", Mode = "block" }
            });

            // Act / Assert
            var exception = Assert.Throws<AikidoException>(() => OnRequest(
                new Uri("https://blocked.example/path"),
                GetHttpClientSendAsyncMethod(),
                CreateContext()));

            Assert.That(exception?.Message, Does.Contain("blocked.example"));
        }

        [Test]
        public void OnRequest_WithAbsoluteHttpClientRequest_CapturesRequestUri()
        {
            // Arrange
            // Act
            var result = OnRequest(
                new Uri("https://absolute.example/path"),
                GetHttpClientSendAsyncMethod(),
                CreateContext());

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "absolute.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WithBaseAddressAndNoRequest_CapturesBaseAddress()
        {
            // Act
            using var httpClient = new HttpClient { BaseAddress = new Uri("https://base-only.example") };
            var result = OnHttpClientRequest(
                null,
                httpClient,
                GetHttpClientSendAsyncMethod(),
                CreateContext());

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "base-only.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WithBaseAddressAndRelativeRequest_CapturesResolvedHost()
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri("https://base.example") };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/relative");

            var result = OnHttpClientRequest(
                request,
                httpClient,
                GetHttpClientSendAsyncMethod(),
                CreateContext());

            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "base.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WithWebRequest_CapturesRequestUri()
        {
#pragma warning disable SYSLIB0014
            var webRequest = WebRequest.Create("https://webrequest.example/path");
#pragma warning restore SYSLIB0014

            var result = OnWebRequest(
                webRequest,
                GetMethod(typeof(WebRequest), nameof(WebRequest.GetResponse)),
                CreateContext());

            Assert.That(result, Is.True);
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "webrequest.example" && h.Port == 443), Is.True);
        }

        [Test]
        public void OnRequest_WithHttpClientAndNoRequestOrBaseAddress_ReturnsTrue()
        {
            // Act
            var result = OnRequest(
                null,
                GetHttpClientSendAsyncMethod(),
                CreateContext());

            // Assert
            Assert.That(result, Is.True);
        }

        private static MethodInfo GetHttpClientSendAsyncMethod()
        {
            return typeof(HttpClient).GetMethod(
                nameof(HttpClient.SendAsync),
                new[] { typeof(HttpRequestMessage), typeof(CancellationToken) })!;
        }

        private static Context CreateContext()
        {
            return new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10"
            };
        }

        private bool OnRequest(Uri? targetUri, MethodInfo methodInfo, Context? context)
        {
            using var httpClient = new HttpClient();
            using var request = targetUri == null
                ? null
                : new HttpRequestMessage(HttpMethod.Get, targetUri);

            return OnHttpClientRequest(request, httpClient, methodInfo, context);
        }

        private bool OnHttpClientRequest(HttpRequestMessage? request, HttpClient? httpClient, MethodInfo methodInfo, Context? context)
        {
            _activeContext = context;
            return OutboundRequestSink.OnRequestHttpClient(request!, httpClient!, methodInfo);
        }

        private bool OnWebRequest(WebRequest? request, MethodInfo methodInfo, Context? context)
        {
            _activeContext = context;
            return OutboundRequestSink.OnRequestWebRequest(request!, methodInfo);
        }

        private static MethodInfo GetMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            return method;
        }

    }
}
