using System;
using System.Web;
using System.Web.Routing;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using Aikido.Zen.DotNetFramework.HttpModules;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Tests.Mocks;
using System.IO;
using System.Reflection;

namespace Aikido.Zen.Tests.DotNetFramework
{
    public class ContextModuleTests
    {
        private HttpContext _mockHttpContext;
        private ContextModule _contextModule;

        [SetUp]
        public void Setup()
        {
            RouteTable.Routes.Clear();
            _contextModule = new ContextModule();
        }

        [Test]
        public void GetParametrizedRoute_ReturnsCorrectRoutePattern_ForStaticFiles()
        {
            // Arrange
            RouteTable.Routes.Add(new System.Web.Routing.Route("static/file.js", new StopRoutingHandler()));
            _mockHttpContext = new HttpContext(new HttpRequest(string.Empty, "http://test.local/static/file.js", string.Empty), new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That("/static/file.js", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsCorrectRoutePattern_ForRouteParameters()
        {
            // Arrange
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/items/{id}", new StopRoutingHandler()));
            _mockHttpContext = new HttpContext(new HttpRequest(string.Empty, "http://test.local/api/items/123", string.Empty), new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That("/api/items/{id}", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsCorrectRoutePattern()
        {
            // Arrange
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/test", new StopRoutingHandler()));
            _mockHttpContext = new HttpContext(new HttpRequest(string.Empty, "http://test.local/api/test", string.Empty), new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That("/api/test", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsMostSpecificRoutePattern_WhenMultipleRoutesMatch()
        {
            // Arrange
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/items/{id}", new StopRoutingHandler()));
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/items/special/{id}", new StopRoutingHandler()));
            _mockHttpContext = new HttpContext(new HttpRequest(string.Empty, "http://test.local/api/items/special/123", string.Empty), new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That("/api/items/special/{id}", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsMostSpecificRoutePattern_WhenMultipleRoutesWithAndWithoutParametersMatch()
        {
            // Arrange
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/items/{id}", new System.Web.Routing.StopRoutingHandler()));
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/items/special", new StopRoutingHandler()));
            _mockHttpContext = new HttpContext(new HttpRequest(string.Empty, "http://test.local/api/items/special", string.Empty), new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That("/api/items/special", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsMostSpecificRoutePattern_WhenMultipleRoutesWithDifferentDepthsMatch()
        {
            // Arrange
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/{version}/items/{id}", new StopRoutingHandler()));
            RouteTable.Routes.Add(new System.Web.Routing.Route("api/v1/items/{id}", new StopRoutingHandler()));
            _mockHttpContext = new HttpContext(new HttpRequest(string.Empty, "http://test.local/api/v1/items/123", string.Empty), new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That("/api/v1/items/{id}", Is.EqualTo(route));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsParameterizedRoute_WhenNoRouteFound()
        {
            // Arrange
            _mockHttpContext = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/api/users/123/posts/abc123dEf456", string.Empty),
                new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That(route, Is.EqualTo("/api/users/:number/posts/:secret"));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsParameterizedRoute_ForEmailAddress()
        {
            // Arrange
            _mockHttpContext = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/api/users/john.doe@example.com", string.Empty),
                new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That(route, Is.EqualTo("/api/users/:email"));
        }

        [Test]
        public void GetParametrizedRoute_ReturnsParameterizedRoute_ForUUID()
        {
            // Arrange
            _mockHttpContext = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/api/users/109156be-c4fb-41ea-b1b4-efe1671c5836", string.Empty),
                new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            Assert.That(route, Is.EqualTo("/api/users/:uuid"));
        }
    }
}
