using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class ContextMiddlewareTests
    {
        private Mock<HttpContext> _mockHttpContext;
        private Mock<EndpointDataSource> _mockEndpointDataSource;
        private ContextMiddleware _contextMiddleware;

        [SetUp]
        public void Setup()
        {
            _mockEndpointDataSource = new Mock<EndpointDataSource>();
            _mockEndpointDataSource.Setup(m => m.Endpoints).Returns(new List<Endpoint>
            {
                CreateEndpoint("api/test", "TestEndpoint"),
                CreateEndpoint("api/items/{id}", "ParameterizedEndpoint"),
                CreateEndpoint("static/file.js", "StaticFileEndpoint"),
                CreateEndpoint("api/items/special/{id}", "SpecialParameterizedEndpoint"),
                CreateEndpoint("api/items/special", "SpecialEndpoint"),
                CreateEndpoint("api/{version}/items/{id}", "VersionedParameterizedEndpoint"),
                CreateEndpoint("api/v1/items/{id}", "V1ParameterizedEndpoint")
            });
            _mockHttpContext = new Mock<HttpContext>();
            _contextMiddleware = new ContextMiddleware([_mockEndpointDataSource.Object]);
        }

        private Endpoint CreateEndpoint(string pattern, string name)
        {
            return new RouteEndpoint(
                context => Task.CompletedTask,
                RoutePatternFactory.Parse(pattern),
                0,
                new EndpointMetadataCollection
                (
                    new HttpMethodMetadata(new[] { "GET" }),
                    new RouteValuesAddressMetadata(pattern)
                ),
                name);
        }

        [Test]
        public void GetParametrizedRoute_ReturnsCorrectRoutePattern()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns("/api/test");

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/api/test", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsCorrectRoutePattern_WithRouteParameters()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns("/api/items/123");
            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/api/items/{id}", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsCorrectRoutePattern_ForStaticFiles()
        {
            _mockHttpContext.Setup(c => c.Request.Path).Returns("/static/file.js");

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/static/file.js", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsEmptyString_ForNullPath()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns((string)null);

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That(string.Empty, Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsMostSpecificRoutePattern_WhenMultipleRoutesMatch()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/items/special/123"));

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/api/items/special/{id}", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsMostSpecificRoutePattern_WhenMultipleRoutesWithAndWithoutParametersMatch()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/items/special"));

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/api/items/special", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsMostSpecificRoutePattern_WhenMultipleRoutesWithDifferentDepthsMatch()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/v1/items/123"));

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That(route, Is.EqualTo("/api/v1/items/{id}"), "The route pattern should match the most specific route with the correct parameter");
        }

        [Test]
        public void GetParametrizedRoute_ReturnsParameterizedRoute_WhenNoRouteFound()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/users/123/posts/abc123dEf456"));
            _mockHttpContext.Setup(c => c.Request.Scheme).Returns("http");
            _mockHttpContext.Setup(c => c.Request.Host).Returns(new HostString("test.local"));

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That(route, Is.EqualTo("/api/users/:number/posts/:secret"));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsParameterizedRoute_ForEmailAddress()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/users/john.doe@example.com"));
            _mockHttpContext.Setup(c => c.Request.Scheme).Returns("http");
            _mockHttpContext.Setup(c => c.Request.Host).Returns(new HostString("test.local"));

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That(route, Is.EqualTo("/api/users/:email"));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsParameterizedRoute_ForUUID()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/users/109156be-c4fb-41ea-b1b4-efe1671c5836"));
            _mockHttpContext.Setup(c => c.Request.Scheme).Returns("http");
            _mockHttpContext.Setup(c => c.Request.Host).Returns(new HostString("test.local"));

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That(route, Is.EqualTo("/api/users/:uuid"));
        }
    }
}
