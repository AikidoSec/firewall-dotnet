using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Tests.Mocks;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    public class HttpClientPatchTests
    {
        private HttpClient _httpClient;
        private MethodInfo _sendAsyncMethodInfo;

        [SetUp]
        public void Setup()
        {
            _httpClient = new HttpClient();
            _sendAsyncMethodInfo = typeof(HttpClient).GetMethod("SendAsync", new[] { typeof(HttpRequestMessage), typeof(System.Threading.CancellationToken) });

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var apiMock = ZenApiMock.CreateMock();
            Agent.NewInstance(apiMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
            Agent.Instance?.ClearContext();
        }

        private Context CreateContext()
        {
            var context = new Context();
            return context;
        }

        [Test]
        public void OnHttpClient_WithNullBaseAddress_UsesRequestUri()
        {
            // Arrange
            Agent.Instance.ClearContext();
            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com:8080/path");
            var context = CreateContext();

            // Act
            var result = HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count(), Is.EqualTo(1));
            var hostname = Agent.Instance.Context.Hostnames.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(hostname.Hostname, Is.EqualTo("test.com"));
                Assert.That(hostname.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public async Task OnHttpClient_WithBaseAddressAndNullRequestUri_UsesBaseAddress()
        {
            // Arrange
            Agent.Instance.ClearContext();
            _httpClient.BaseAddress = new Uri("http://example.com:9090/");
            var request = new HttpRequestMessage();
            var context = CreateContext();
            request.RequestUri = null;

            // Act
            var result = HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count(), Is.EqualTo(0));
        }

        // --- SSRF Detection Tests for OnRequestStarted ---

        [Test]
        public void OnRequestStarted_WithSafeUrl_ReturnsTrue()
        {
            // Arrange
            var safeUri = new Uri("https://example.com/path");
            var request = new HttpRequestMessage(HttpMethod.Get, safeUri);
            var context = CreateContext();
            context.ParsedUserInput = new Dictionary<string, string> { { "url", safeUri.ToString() } };

            // Act
            var result = HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(context.AttackDetected, Is.False);
        }

        [Test]
        public void OnRequestStarted_WithLocalhostUrl_ThrowsException()
        {
            // Arrange
            var localhostUri = new Uri("http://localhost:8080/path");
            var request = new HttpRequestMessage(HttpMethod.Get, localhostUri);
            var context = CreateContext();
            context.ParsedUserInput = new Dictionary<string, string> { { "url", localhostUri.ToString() } };

            // Act & Assert
            Assert.Throws<AikidoException>(() =>
                HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context)
            );
            Assert.That(context.AttackDetected, Is.True);
        }

        [Test]
        public void OnRequestStarted_WithPrivateIP_ThrowsException()
        {
            // Arrange
            var privateIpUri = new Uri("http://192.168.1.1:8080/path");
            var request = new HttpRequestMessage(HttpMethod.Get, privateIpUri);
            var context = CreateContext();
            context.ParsedUserInput = new Dictionary<string, string> { { "url", privateIpUri.ToString() } };

            // Act & Assert
            Assert.Throws<AikidoException>(() =>
                HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context)
            );
            Assert.That(context.AttackDetected, Is.True);
        }

        [Test]
        public void OnRequestStarted_WithBlockingDisabled_ReturnsTrue_AttackDetected()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var localhostUri = new Uri("http://localhost:8080/path");
            var request = new HttpRequestMessage(HttpMethod.Get, localhostUri);
            var context = CreateContext();
            context.ParsedUserInput = new Dictionary<string, string> { { "url", localhostUri.ToString() } };

            // Act
            var result = HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(context.AttackDetected, Is.True);
        }

        [Test]
        public void OnRequestStarted_WithNullRequestUriInMessage_ReturnsTrueAndNoAttack()
        {
            // Arrange
            var request = new HttpRequestMessage();
            var context = CreateContext();

            // Act
            var result = HttpClientPatcher.OnRequestStarted(request, _sendAsyncMethodInfo, context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(context.AttackDetected, Is.False);
            Assert.That(Agent.Instance.Context.Hostnames.Count(), Is.EqualTo(0));
        }

        // --- OnRequestFinished Tests ---

        [Test]
        public void OnRequestFinished_WithNullRequest_ReturnsVoid()
        {
            // Arrange
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            var context = CreateContext();

            // Act & Assert
            Assert.DoesNotThrow(() => HttpClientPatcher.OnRequestFinished(null, response, context));
            Assert.That(context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnRequestFinished_WithNullResponse_ReturnsVoid()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            var context = CreateContext();

            // Act & Assert
            Assert.DoesNotThrow(() => HttpClientPatcher.OnRequestFinished(request, null, context));
            Assert.That(context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnRequestFinished_WithNullContext_ReturnsVoid()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            // Act & Assert
            Assert.DoesNotThrow(() => HttpClientPatcher.OnRequestFinished(request, response, null));
        }

        [TestCase(System.Net.HttpStatusCode.Redirect)]
        [TestCase(System.Net.HttpStatusCode.MovedPermanently)]
        [TestCase(System.Net.HttpStatusCode.TemporaryRedirect)]
        [TestCase((System.Net.HttpStatusCode)308)]
        public void OnRequestFinished_WithHttpRedirectAndLocationHeader_AddsRedirect(System.Net.HttpStatusCode statusCode)
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            var destinationUri = new Uri("http://destination.com");
            var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            var response = new HttpResponseMessage(statusCode);
            response.Headers.Location = destinationUri;
            var context = CreateContext();

            // Act
            HttpClientPatcher.OnRequestFinished(request, response, context);

            // Assert
            Assert.That(context.OutgoingRequestRedirects, Has.Count.EqualTo(1));
            var redirectInfo = context.OutgoingRequestRedirects.First(); Assert.Multiple(() =>
            {
                Assert.That(redirectInfo.Source, Is.EqualTo(sourceUri));
                Assert.That(redirectInfo.Destination, Is.EqualTo(destinationUri));
            });
        }

        [Test]
        public void OnRequestFinished_WithHttpRedirectAndNoLocationHeader_DoesNotAddRedirect()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Redirect);
            var context = CreateContext();

            // Act
            HttpClientPatcher.OnRequestFinished(request, response, context);

            // Assert
            Assert.That(context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnRequestFinished_WithHttpRedirectAndNullLocationHeader_DoesNotAddRedirect()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Redirect);
            response.Headers.Location = null;
            var context = CreateContext();

            // Act
            HttpClientPatcher.OnRequestFinished(request, response, context);

            // Assert
            Assert.That(context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnRequestFinished_WithHttpNonRedirectStatus_DoesNotAddRedirect()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            var destinationUri = new Uri("http://destination.com");
            var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Headers.Location = destinationUri;
            var context = CreateContext();

            // Act
            HttpClientPatcher.OnRequestFinished(request, response, context);

            // Assert
            Assert.That(context.OutgoingRequestRedirects, Is.Empty);
        }
    }
}
