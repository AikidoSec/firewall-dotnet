using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Routing;
using Aikido.Zen.DotNetFramework.HttpModules;
using NUnit.Framework;


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

        [Test]
        public void GetParametrizedRoute_ReturnsOriginalUrl_WhenPathIsSingleSegment()
        {
            // Arrange
            // Add a generic slug route to simulate a catch-all scenario
            RouteTable.Routes.Add(new System.Web.Routing.Route("{slug}", new StopRoutingHandler()));

            // Test case where the path segment *is* a parameter type recognized by BuildRouteFromUrl (like UUID)
            _mockHttpContext = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/this-is-a-potential-slug", string.Empty),
                new HttpResponse(null));

            // Act
            var route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            // Assert
            _mockHttpContext = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/109156be-c4fb-41ea-b1b4-efe1671c5836", string.Empty),
                new HttpResponse(null));
            route = _contextModule.GetParametrizedRoute(_mockHttpContext);

            Assert.That(route, Is.EqualTo("/109156be-c4fb-41ea-b1b4-efe1671c5836")); // PathIsSingleSegment would return true for /:uuid
            _mockHttpContext = new HttpContext(
               new HttpRequest(string.Empty, "http://test.local/simple-slug", string.Empty),
               new HttpResponse(null));
            route = _contextModule.GetParametrizedRoute(_mockHttpContext);
            Assert.That(route, Is.EqualTo("/simple-slug")); // PathIsSingleSegment would return false for /simple-slug

            // Test case where PathIsSingleSegment would be false (multiple segments)
            _mockHttpContext = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/users/123/profile", string.Empty),
                new HttpResponse(null));
            route = _contextModule.GetParametrizedRoute(_mockHttpContext);
            Assert.That(route, Is.EqualTo("/users/:number/profile")); // PathIsSingleSegment would return false for /users/:number/profile
        }

        [Test]
        public void FlattenQueryParameters_FlattensSingleParameter()
        {
            // Arrange
            var queryString = new System.Collections.Specialized.NameValueCollection();
            queryString.Add("param1", "value1");

            // Act
            var method = typeof(ContextModule).GetMethod("FlattenQueryParameters", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { queryString });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ContainsKey("param1"), Is.True);
            Assert.That(result["param1"], Is.EqualTo("value1"));
        }

        [Test]
        public void FlattenQueryParameters_FlattensMultipleParameters()
        {
            // Arrange
            var queryString = new System.Collections.Specialized.NameValueCollection();
            queryString.Add("param1", "value1");
            queryString.Add("param1", "value2");
            queryString.Add("param1", "value3");

            // Act
            var method = typeof(ContextModule).GetMethod("FlattenQueryParameters", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { queryString });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ContainsKey("param1"), Is.True);
            Assert.That(result["param1"], Is.EqualTo("value1"));
            Assert.That(result.ContainsKey("param1[1]"), Is.True);
            Assert.That(result["param1[1]"], Is.EqualTo("value2"));
            Assert.That(result.ContainsKey("param1[2]"), Is.True);
            Assert.That(result["param1[2]"], Is.EqualTo("value3"));
        }

        [Test]
        public void FlattenHeaders_FlattensSingleHeader()
        {
            // Arrange
            var headers = new System.Collections.Specialized.NameValueCollection();
            headers.Add("X-Custom-Header", "custom-value");

            // Act
            var method = typeof(ContextModule).GetMethod("FlattenHeaders", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { headers });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ContainsKey("X-Custom-Header"), Is.True);
            Assert.That(result["X-Custom-Header"], Is.EqualTo("custom-value"));
        }

        [Test]
        public void FlattenHeaders_FlattensMultipleHeaders()
        {
            // Arrange
            var headers = new System.Collections.Specialized.NameValueCollection();
            headers.Add("Accept", "text/html");
            headers.Add("Accept", "application/json");
            headers.Add("Accept", "application/xml");

            // Act
            var method = typeof(ContextModule).GetMethod("FlattenHeaders", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { headers });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ContainsKey("Accept"), Is.True);
            Assert.That(result["Accept"], Is.EqualTo("text/html"));
            Assert.That(result.ContainsKey("Accept[1]"), Is.True);
            Assert.That(result["Accept[1]"], Is.EqualTo("application/json"));
            Assert.That(result.ContainsKey("Accept[2]"), Is.True);
            Assert.That(result["Accept[2]"], Is.EqualTo("application/xml"));
        }
    }
}
