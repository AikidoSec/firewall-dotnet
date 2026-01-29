using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class ContextTests
    {
        private Context _context;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
        }

        [Test]
        public void DefaultValues_ShouldBeInitializedCorrectly()
        {
            // Assert
            Assert.That(_context.Url, Is.EqualTo(string.Empty));
            Assert.That(_context.Method, Is.EqualTo(string.Empty));
            Assert.That(_context.Query, Is.Empty);
            Assert.That(_context.Headers, Is.Empty);
            Assert.That(_context.RouteParams, Is.Empty);
            Assert.That(_context.RemoteAddress, Is.EqualTo(string.Empty));
            Assert.That(_context.Body, Is.Null);
            Assert.That(_context.Cookies, Is.Empty);
            Assert.That(_context.AttackDetected, Is.False);
            Assert.That(_context.User, Is.Null);
            Assert.That(_context.Source, Is.EqualTo(string.Empty));
            Assert.That(_context.Route, Is.EqualTo(string.Empty));
            Assert.That(_context.Graphql, Is.Null);
            Assert.That(_context.Xml, Is.Null);
            Assert.That(_context.Subdomains, Is.Empty);
            Assert.That(_context.Cache, Is.Empty);
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
            Assert.That(_context.ParsedUserInput, Is.Empty);
            Assert.That(_context.UserAgent, Is.EqualTo(string.Empty));
            Assert.That(_context.IsGraphQL, Is.False);
            Assert.That(_context.ParsedBody, Is.Null);
            Assert.That(_context.ContextMiddlewareInstalled, Is.False);
            Assert.That(_context.BlockingMiddlewareInstalled, Is.False);
            Assert.That(_context.ConsumedRateLimitForIP, Is.False);
            Assert.That(_context.ConsumedRateLimitForUser, Is.False);
        }

        [Test]
        public void IsGraphQL_ShouldReturnTrue_WhenGraphqlIsNotEmpty()
        {
            // Arrange
            _context.Graphql = new[] { "query" };

            // Act
            var isGraphQL = _context.IsGraphQL;

            // Assert
            Assert.That(isGraphQL, Is.True);
        }

        [Test]
        public void IsGraphQL_ShouldReturnFalse_WhenGraphqlIsEmpty()
        {
            // Arrange
            _context.Graphql = Array.Empty<string>();

            // Act
            var isGraphQL = _context.IsGraphQL;

            // Assert
            Assert.That(isGraphQL, Is.False);
        }

        [Test]
        public void RedirectInfo_ShouldInitializeCorrectly()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            var destinationUri = new Uri("http://destination.com");

            // Act
            var redirectInfo = new Context.RedirectInfo(sourceUri, destinationUri);

            // Assert
            Assert.That(redirectInfo.Source, Is.EqualTo(sourceUri));
            Assert.That(redirectInfo.Destination, Is.EqualTo(destinationUri));
        }

        [Test]
        public void SetAndGetProperties_ShouldWorkCorrectly()
        {
            // Arrange
            var url = "http://example.com/api/test";
            var method = "POST";
            var remoteAddress = "192.168.1.1";
            var userAgent = "Mozilla/5.0";
            var user = new User("user1", "User One");

            // Act
            _context.Url = url;
            _context.Method = method;
            _context.RemoteAddress = remoteAddress;
            _context.UserAgent = userAgent;
            _context.User = user;

            // Assert
            Assert.That(_context.Url, Is.EqualTo(url));
            Assert.That(_context.Method, Is.EqualTo(method));
            Assert.That(_context.RemoteAddress, Is.EqualTo(remoteAddress));
            Assert.That(_context.UserAgent, Is.EqualTo(userAgent));
            Assert.That(_context.User, Is.EqualTo(user));
        }
    }
}
