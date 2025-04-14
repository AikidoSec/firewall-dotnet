using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetCore.Middleware;
using Aikido.Zen.Tests.Mocks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using NUnit.Framework;

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
        public void GetParametrizedRoute_ReturnsForwardSlash_ForNullPath()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns((string)null);

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/", Is.EqualTo(route));
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

        [Test]
        public void GetParametrizedRoute_ReturnsOriginalUrl_WhenPathIsSingleSegment()
        {
            // Arrange
            // Clear specific endpoints, use only default fallback logic
            _mockEndpointDataSource.Setup(m => m.Endpoints).Returns(new List<Endpoint>());
            _mockHttpContext.Setup(c => c.Request.Scheme).Returns("http");
            _mockHttpContext.Setup(c => c.Request.Host).Returns(new HostString("test.local"));

            // Act & Assert

            // Test case where the path segment *is* a parameter type recognized by BuildRouteFromUrl (like UUID)
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/109156be-c4fb-41ea-b1b4-efe1671c5836"));
            var routeUuid = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);
            Assert.That(routeUuid, Is.EqualTo("/109156be-c4fb-41ea-b1b4-efe1671c5836")); // PathIsSingleSegment would return true for /:uuid

            // Test case where the path segment is *not* a parameter type recognized by BuildRouteFromUrl
            // but might be considered a slug
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/simple-slug"));
            var routeSlug = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);
            Assert.That(routeSlug, Is.EqualTo("/simple-slug")); // PathIsSingleSegment would return false for /simple-slug

            // Test case where PathIsSingleRouteParameter would be false (multiple segments)
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/users/123/profile"));
            var routeMultiSegment = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);
            Assert.That(routeMultiSegment, Is.EqualTo("/users/:number/profile")); // PathIsSingleSegment would return false for /users/:number/profile

            // Test case for API prefix which should be ignored by PathIsSingleSegment
            _mockHttpContext.Setup(c => c.Request.Path).Returns(new PathString("/api/v1/109156be-c4fb-41ea-b1b4-efe1671c5836"));
            var routeApiUuid = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);
            // BuildRouteFromUrl handles the full path including /api/v1/
            Assert.That(routeApiUuid, Is.EqualTo("/api/v1/:uuid")); // PathIsSingleSegment would return true for /:uuid (after stripping prefix)

            // Restore endpoints for other tests if needed (though Setup runs before each test)
            _mockEndpointDataSource.Setup(m => m.Endpoints).Returns(new List<Endpoint>
            {
                CreateEndpoint("api/test", "TestEndpoint"),
                // ... potentially add back other endpoints if needed for subsequent tests in the same run, though usually not necessary with [SetUp]
            });
        }
    }
}
