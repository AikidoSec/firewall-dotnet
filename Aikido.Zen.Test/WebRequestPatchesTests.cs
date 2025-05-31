using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Core.Patches;
using Moq;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class WebRequestPatchesTests
    {
        private Context _realContext;
        private Mock<Context> _mockContext;
        private MethodInfo _methodInfo;
        private Mock<IReportingAPIClient> _reportingMock;
        private Mock<IRuntimeAPIClient> _runtimeMock;
        private Mock<ZenApi> _zenApiMock;

        [SetUp]
        public void Setup()
        {
            _realContext = new Context();
            _mockContext = new Mock<Context>() { CallBase = true };
            _methodInfo = typeof(WebRequest).GetMethod("GetResponse");

            _reportingMock = new Mock<IReportingAPIClient>();
            _runtimeMock = new Mock<IRuntimeAPIClient>();
            _zenApiMock = new Mock<ZenApi>(_reportingMock.Object, _runtimeMock.Object);

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);

            Agent.NewInstance(_zenApiMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
        }

        // Helper to run the operation and verify AttackDetected flag
        private void RunAndVerifyAttackFlag(WebRequest request, MethodInfo methodInfo, Context context, bool expectAttack, bool expectBlocked)
        {
            context.AttackDetected = false; // Reset flag before test

            if (expectBlocked)
            {
                Assert.Throws<AikidoException>(
                    () => WebRequestPatcher.OnWebRequest(request, methodInfo, context),
                    "Expected AikidoException for blocked SSRF attack."
                );
            }
            else
            {
                var result = WebRequestPatcher.OnWebRequest(request, methodInfo, context);
                Assert.That(result, Is.True, "CaptureRequest should return true when not blocking.");
            }

            // Verify AttackDetected flag on the context object passed to the patcher
            if (expectAttack)
            {
                Assert.That(context.AttackDetected, Is.True, "Context AttackDetected flag should be true when attack is expected.");
            }
            else
            {
                Assert.That(context.AttackDetected, Is.False, "Context AttackDetected flag should be false when attack is not expected.");
            }
        }

        [Test]
        public void CaptureRequest_WithNullContext_ReturnsTrue()
        {
            // Arrange
            var request = WebRequest.Create("https://example.com");

            // Act
            var result = WebRequestPatcher.OnWebRequest(request, _methodInfo, null);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CaptureRequest_WithNullRequest_ReturnsTrue()
        {
            // Act
            var result = WebRequestPatcher.OnWebRequest(null, _methodInfo, _mockContext.Object);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CaptureRequest_WithSafeUrl_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var request = WebRequest.Create("https://example.com");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "https://example.com" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: false, expectBlocked: false);
        }

        [Test]
        public void CaptureRequest_WithLocalhostUrl_ThrowsExceptionWhenBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var request = WebRequest.Create("http://localhost:8080");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "http://localhost:8080" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void CaptureRequest_WithLocalhostUrl_ReturnsTrueWhenNotBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var request = WebRequest.Create("http://localhost:8080");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "http://localhost:8080" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void CaptureRequest_WithRedirect_DetectsSSRFInRedirectChain()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var request = WebRequest.Create("https://example.com");
            // Add redirect info to context
            _realContext.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                new Uri("https://example.com"),
                new Uri("http://localhost:8080")
            ));
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "https://example.com" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: true);
        }

        [TestCase("127.0.0.1")]
        [TestCase("::1")]
        [TestCase("localhost")]
        [TestCase("127.0.0.1:8080")]
        [TestCase("::1:8080")]
        [TestCase("localhost:8080")]
        public void CaptureRequest_WithLocalhostVariants_ThrowsExceptionWhenBlocking(string host)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var request = WebRequest.Create($"http://{host}");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", $"http://{host}" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: true);
        }

        [TestCase("127.0.0.1")]
        [TestCase("::1")]
        [TestCase("localhost")]
        [TestCase("127.0.0.1:8080")]
        [TestCase("::1:8080")]
        [TestCase("localhost:8080")]
        public void CaptureRequest_WithLocalhostVariants_ReturnsTrueWhenNotBlocking(string host)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var request = WebRequest.Create($"http://{host}");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", $"http://{host}" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void CaptureRequest_WithPrivateIP_ThrowsExceptionWhenBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var request = WebRequest.Create("http://192.168.1.1");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "http://192.168.1.1" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: true);
        }

        [Test]
        public void CaptureRequest_WithPrivateIP_ReturnsTrueWhenNotBlocking()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var request = WebRequest.Create("http://192.168.1.1");
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "http://192.168.1.1" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: false);
        }

        [Test]
        public void CaptureRequest_WithMultipleRedirects_DetectsSSRFInChain()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var request = WebRequest.Create("https://example.com");
            // Add multiple redirects to context
            _realContext.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                new Uri("https://example.com"),
                new Uri("https://redirect1.com")
            ));
            _realContext.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                new Uri("https://redirect1.com"),
                new Uri("http://localhost:8080")
            ));
            _realContext.ParsedUserInput = new Dictionary<string, string> { { "url", "https://example.com" } };

            // Act & Assert
            RunAndVerifyAttackFlag(request, _methodInfo, _mockContext.Object, expectAttack: true, expectBlocked: true);
        }
    }
}
