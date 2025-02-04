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
                CreateEndpoint("static/file.js", "StaticFileEndpoint")
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
        public void GetRoute_ReturnsCorrectRoutePattern()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns("/api/test");

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.AreEqual("/api/test", route);
        }

        [Test]
        public void GetRoute_ReturnsCorrectRoutePattern_WithRouteParameters()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns("/api/items/123");
            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.AreEqual("/api/items/{id}", route);
        }

        [Test]
        public void GetRoute_ReturnsCorrectRoutePattern_ForStaticFiles()
        {
            _mockHttpContext.Setup(c => c.Request.Path).Returns("/static/file.js");

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.AreEqual("/static/file.js", route);
        }

        [Test]
        public void GetRoute_ReturnsEmptyString_ForNullPath()
        {
            // Arrange
            _mockHttpContext.Setup(c => c.Request.Path).Returns((string)null);

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.AreEqual(string.Empty, route);
        }
    }
}
