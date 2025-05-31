using System;
using System.Collections.Generic;
using System.Net;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class SSRFDetectorTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
        }

        [Test]
        public void IsSSRFVulnerable_WithSafeUrl_ReturnsFalse()
        {
            // Arrange
            var uri = new Uri("https://example.com");

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "HttpClient", "outgoing_request");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSSRFVulnerable_WithLocalhost_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("http://localhost:8080");

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithPrivateIP_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("http://192.168.1.1:8080");

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithRedirectToLocalhost_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("https://example.com");
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                uri,
                new Uri("http://localhost:8080")
            ));

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithMultipleRedirects_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("https://example.com");
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                uri,
                new Uri("https://redirect1.com")
            ));
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                new Uri("https://redirect1.com"),
                new Uri("http://localhost:8080")
            ));

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithExcessiveRedirects_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("https://example.com");
            for (int i = 0; i < 11; i++)
            {
                _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                    new Uri($"https://redirect{i}.com"),
                    new Uri($"https://redirect{i + 1}.com")
                ));
            }

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [TestCase("127.0.0.1")]
        [TestCase("localhost")]
        [TestCase("127.0.0.1:8080")]
        [TestCase("localhost:8080")]
        public void IsSSRFVulnerable_WithLocalhostVariants_ReturnsTrue(string host)
        {
            // Arrange
            var uri = new Uri($"http://{host}");

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithIPv6Localhost_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("http://[::1]:8080");

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithUserInputMatch_ReturnsTrue()
        {
            // Arrange
            var uri = new Uri("http://example.com:8080");
            _context.ParsedUserInput = new Dictionary<string, string>
            {
                { "url", "http://example.com:8080" }
            };

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsSSRFVulnerable_WithNullUri_ReturnsFalse()
        {
            // Act
            var result = SSRFDetector.IsSSRFVulnerable(null, _context, "module", "operation");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsSSRFVulnerable_WithNullContext_ReturnsFalse()
        {
            // Arrange
            var uri = new Uri("http://example.com");

            // Act
            var result = SSRFDetector.IsSSRFVulnerable(uri, null, "module", "operation");

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
