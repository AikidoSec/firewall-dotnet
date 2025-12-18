using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
        private Mock<ConnectionInfo> _mockConnection;
        private MethodInfo _getClientIpMethod;
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
                CreateEndpoint("api/v1/items/{id}", "V1ParameterizedEndpoint"),
                CreateEndpoint("{slug}", "SlugEndpoint")
            });

            _mockConnection = new Mock<ConnectionInfo>();
            _mockConnection.SetupAllProperties();

            _mockHttpContext = new Mock<HttpContext>();
            _mockHttpContext.Setup(c => c.Connection).Returns(_mockConnection.Object);

            _getClientIpMethod = typeof(ContextMiddleware).GetMethod("GetClientIp", BindingFlags.NonPublic | BindingFlags.Static)!;

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

        [Test]
        public void FlattenQueryParameters_FlattensSingleParameter()
        {
            // Arrange
            var mockQuery = new Mock<IQueryCollection>();
            var queryDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "param1", "value1" }
            };
            mockQuery.Setup(x => x.GetEnumerator()).Returns(queryDict.GetEnumerator());

            // Act
            var method = typeof(ContextMiddleware).GetMethod("FlattenQueryParameters", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { mockQuery.Object });

            // Assert
            Assert.That(result.ContainsKey("param1"), Is.True);
            Assert.That(result["param1"], Is.EqualTo("value1"));
        }

        [Test]
        public void FlattenQueryParameters_FlattensMultipleParameters()
        {
            // Arrange
            var mockQuery = new Mock<IQueryCollection>();
            var queryDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "param1", new Microsoft.Extensions.Primitives.StringValues(new[] { "value1", "value2", "value3" }) }
            };
            mockQuery.Setup(x => x.GetEnumerator()).Returns(queryDict.GetEnumerator());

            // Act
            var method = typeof(ContextMiddleware).GetMethod("FlattenQueryParameters", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { mockQuery.Object });

            // Assert
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
            var mockHeaders = new Mock<IHeaderDictionary>();
            var headerDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "X-Custom-Header", "custom-value" }
            };
            mockHeaders.Setup(x => x.GetEnumerator()).Returns(headerDict.GetEnumerator());

            // Act
            var method = typeof(ContextMiddleware).GetMethod("FlattenHeaders", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { mockHeaders.Object });

            // Assert
            Assert.That(result.ContainsKey("X-Custom-Header"), Is.True);
            Assert.That(result["X-Custom-Header"], Is.EqualTo("custom-value"));
        }

        [Test]
        public void FlattenHeaders_FlattensMultipleHeaders()
        {
            // Arrange
            var mockHeaders = new Mock<IHeaderDictionary>();
            var headerDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "Accept", new Microsoft.Extensions.Primitives.StringValues(new[] { "text/html", "application/json", "application/xml" }) }
            };
            mockHeaders.Setup(x => x.GetEnumerator()).Returns(headerDict.GetEnumerator());

            // Act
            var method = typeof(ContextMiddleware).GetMethod("FlattenHeaders", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (IDictionary<string, string>)method.Invoke(null, new object[] { mockHeaders.Object });

            // Assert
            Assert.That(result.ContainsKey("Accept"), Is.True);
            Assert.That(result["Accept"], Is.EqualTo("text/html"));
            Assert.That(result.ContainsKey("Accept[1]"), Is.True);
            Assert.That(result["Accept[1]"], Is.EqualTo("application/json"));
            Assert.That(result.ContainsKey("Accept[2]"), Is.True);
            Assert.That(result["Accept[2]"], Is.EqualTo("application/xml"));
        }

        [Test]
        public void GetClientIp_ReturnsEmpty_WhenNoHeadersOrRemoteAddress()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");

            // Act
            var resultWithoutTrustProxy = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(resultWithoutTrustProxy, Is.Empty);

            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");

            // Act
            var resultWithTrustProxy = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(resultWithTrustProxy, Is.Empty);
        }

        [Test]
        public void GetClientIp_ReturnsRemoteAddress_WhenNoHeadersAndRemoteAddressPresent()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");

            // Act
            var resultWithoutTrustProxy = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(resultWithoutTrustProxy, Is.EqualTo("1.2.3.4"));

            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");

            // Act
            var resultWithTrustProxy = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(resultWithTrustProxy, Is.EqualTo("1.2.3.4"));
        }

        [Test]
        public void GetClientIp_IgnoresXForwardedFor_WhenTrustProxyDisabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");

            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "9.9.9.9" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("1.2.3.4"));

            // Arrange
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");
            headers["X-FORWARDED-FOR"] = "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("df89:84af:85e0:c55f:960c:341a:2cc6:734d"));
        }

        [Test]
        public void GetClientIp_UsesRemoteAddress_WhenXForwardedForInvalidAndTrustProxyEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");

            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "invalid" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result, Is.EqualTo("1.2.3.4"));
        }

        [Test]
        public void GetClientIp_StripsPortFromXForwardedFor_WhenTrustProxyEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "9.9.9.9:8080" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            headers["X-FORWARDED-FOR"] = "[a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880]:8080";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880"));

            // Arrange
            headers["X-FORWARDED-FOR"] = "[a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880]";

            // Act
            var result3 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result3, Is.EqualTo("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880"));

            // Invalid format
            // Arrange
            headers["X-FORWARDED-FOR"] = "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880:8080";
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");

            // Act
            var result4 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result4, Is.EqualTo("df89:84af:85e0:c55f:960c:341a:2cc6:734d"));
        }

        [Test]
        public void GetClientIp_HandlesTrailingCommasInXForwardedFor()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "9.9.9.9," }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            headers["X-FORWARDED-FOR"] = ",9.9.9.9";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("9.9.9.9"));

            // Arrange
            headers["X-FORWARDED-FOR"] = ",9.9.9.9,";

            // Act
            var result3 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result3, Is.EqualTo("9.9.9.9"));

            // Arrange
            headers["X-FORWARDED-FOR"] = ",9.9.9.9,,";

            // Act
            var result4 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result4, Is.EqualTo("9.9.9.9"));
        }

        [Test]
        public void GetClientIp_IgnoresPrivateXForwardedFor_WhenTrustProxyEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "127.0.0.1" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("1.2.3.4"));

            // Arrange
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");
            headers["X-FORWARDED-FOR"] = "::1";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("df89:84af:85e0:c55f:960c:341a:2cc6:734d"));
        }

        [Test]
        public void GetClientIp_SkipsPrivateEntriesInXForwardedFor_WhenTrustProxyEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "127.0.0.1, 9.9.9.9" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");
            headers["X-FORWARDED-FOR"] = "::1, a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880"));
        }

        [Test]
        public void GetClientIp_UsesPublicXForwardedFor_WhenTrustProxyEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "9.9.9.9" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");
            headers["X-FORWARDED-FOR"] = "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880"));
        }

        [Test]
        public void GetClientIp_IgnoresTrailingPrivateIpInXForwardedFor_WhenTrustProxyEnabled()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "9.9.9.9, 127.0.0.1" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");
            headers["X-FORWARDED-FOR"] = "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880, ::1";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880"));
        }

        [Test]
        public void GetClientIp_UsesFirstPublicEntry_WhenMultipleXForwardedForValues()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "9.9.9.9, 8.8.8.8, 7.7.7.7" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("df89:84af:85e0:c55f:960c:341a:2cc6:734d");
            headers["X-FORWARDED-FOR"] = "a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880, 3b07:2fba:0270:2149:5fc1:2049:5f04:2131, 791d:967e:428a:90b9:8f6f:4fcc:5d88:015d";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("a3ad:8f95:d2a8:454b:cf19:be6e:73c6:f880"));
        }

        [Test]
        public void GetClientIp_UsesFirstPublicEntry_WhenManyXForwardedForValues()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "127.0.0.1, 192.168.0.1, 192.168.0.2, 9.9.9.9" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("9.9.9.9"));

            // Arrange
            headers["X-FORWARDED-FOR"] = "9.9.9.9, 127.0.0.1, 192.168.0.1, 192.168.0.2";

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("9.9.9.9"));
        }

        [Test]
        public void GetClientIp_UsesConfiguredClientIpHeader_WhenSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            Environment.SetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER", null);
            _mockConnection.Object.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var headers = new HeaderDictionary
            {
                { "X-FORWARDED-FOR", "127.0.0.1, 192.168.0.1" },
                { "connecting-ip", "9.9.9.9" }
            };
            _mockHttpContext.Setup(c => c.Request.Headers).Returns(headers);

            // Act
            var result1 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result1, Is.EqualTo("1.2.3.4"));

            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER", "connecting-ip");

            // Act
            var result2 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result2, Is.EqualTo("9.9.9.9"));

            // Arrange
            headers.Remove("connecting-ip");

            // Act
            var result3 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result3, Is.EqualTo("1.2.3.4"));

            // Arrange
            headers["connecting-ip"] = "9.9.9.9";
            Environment.SetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER", "connecting-IP");

            // Act
            var result4 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result4, Is.EqualTo("9.9.9.9"));

            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER", string.Empty);

            // Act
            var result5 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result5, Is.EqualTo("1.2.3.4"));

            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER", null);
            headers["X-FORWARDED-FOR"] = "127.0.0.1, 192.168.0.1, 5.6.7.8";

            // Act
            var result6 = (string)_getClientIpMethod.Invoke(null, new object[] { _mockHttpContext.Object })!;

            // Assert
            Assert.That(result6, Is.EqualTo("5.6.7.8"));
        }
    }
}
