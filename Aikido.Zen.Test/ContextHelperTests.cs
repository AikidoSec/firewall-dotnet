using System;
using System.Collections.Generic;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class ContextHelperTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            _context = new Context
            {
                Query = new Dictionary<string, string[]>(),
                Headers = new Dictionary<string, string[]>(),
                RouteParams = new Dictionary<string, string>(),
                ParsedUserInput = new Dictionary<string, string>()
            };
        }

        [Test]
        public void FindHostnameInContext_WithNullContext_ReturnsNull()
        {
            // Act
            var result = ContextHelper.FindHostnameInContext(null, "example.com");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindHostnameInContext_WithNullHostname_ReturnsNull()
        {
            // Act
            var result = ContextHelper.FindHostnameInContext(_context, null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindHostnameInContext_WithHostnameInQuery_ReturnsLocation()
        {
            // Arrange
            _context.Query["url"] = new[] { "http://example.com:8080/path" };

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com", 8080);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Source, Is.EqualTo("query"));
                Assert.That(result.PathToPayload, Is.EqualTo("query.url"));
                Assert.That(result.Payload, Is.EqualTo("http://example.com:8080/path"));
                Assert.That(result.Hostname, Is.EqualTo("example.com"));
                Assert.That(result.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public void FindHostnameInContext_WithHostnameInHeaders_ReturnsLocation()
        {
            // Arrange
            _context.Headers["Host"] = new[] { "example.com:8080" };

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com", 8080);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Source, Is.EqualTo("headers"));
                Assert.That(result.PathToPayload, Is.EqualTo("headers.Host"));
                Assert.That(result.Payload, Is.EqualTo("example.com:8080"));
                Assert.That(result.Hostname, Is.EqualTo("example.com"));
                Assert.That(result.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public void FindHostnameInContext_WithHostnameInRouteParams_ReturnsLocation()
        {
            // Arrange
            _context.RouteParams["domain"] = "example.com:8080";

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com", 8080);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Source, Is.EqualTo("route"));
                Assert.That(result.PathToPayload, Is.EqualTo("route.domain"));
                Assert.That(result.Payload, Is.EqualTo("example.com:8080"));
                Assert.That(result.Hostname, Is.EqualTo("example.com"));
                Assert.That(result.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public void FindHostnameInContext_WithHostnameInUserInput_ReturnsLocation()
        {
            // Arrange
            _context.ParsedUserInput["url"] = "http://example.com:8080/path";

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com", 8080);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result.Source, Is.EqualTo("user_input"));
                Assert.That(result.PathToPayload, Is.EqualTo("user_input.url"));
                Assert.That(result.Payload, Is.EqualTo("http://example.com:8080/path"));
                Assert.That(result.Hostname, Is.EqualTo("example.com"));
                Assert.That(result.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public void FindHostnameInContext_WithHostnameNotFound_ReturnsNull()
        {
            // Arrange
            _context.Query["url"] = new[] { "http://other.com/path" };

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindHostnameInContext_WithPortMismatch_ReturnsNull()
        {
            // Arrange
            _context.Query["url"] = new[] { "http://example.com:9090/path" };

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com", 8080);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindHostnameInContext_WithMultipleMatches_ReturnsFirstMatch()
        {
            // Arrange
            _context.Query["url1"] = new[] { "http://example.com:8080/path1" };
            _context.Query["url2"] = new[] { "http://example.com:8080/path2" };

            // Act
            var result = ContextHelper.FindHostnameInContext(_context, "example.com", 8080);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PathToPayload, Is.EqualTo("query.url1"));
        }
    }
}
