using System;
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
                    new HttpMethodMetadata(new[] { "GET" })
                ),
                name);
        }

        [TestCase(true, 200)]
        [TestCase(true, 302)]
        [TestCase(false, 200)]
        public async Task InvokeAsync_EndpointProtectionControlsRequestReporting(bool forceProtectionOff, int statusCode)
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
                {
                    new EndpointConfig { Route = "/api/test", Method = "GET", ForceProtectionOff = forceProtectionOff }
                });
                var context = new DefaultHttpContext();
                context.Request.Scheme = "http";
                context.Request.Host = new HostString("test.local");
                context.Request.Path = "/api/test";
                context.Request.Method = "GET";
                var nextCalled = false;

                await _contextMiddleware.InvokeAsync(context, httpContext =>
                {
                    nextCalled = true;
                    httpContext.Response.StatusCode = statusCode;
                    return Task.CompletedTask;
                });

                var heartbeat = Aikido.Zen.Core.Models.Events.Heartbeat.Create(Agent.Instance.Context);
                Assert.Multiple(() =>
                {
                    Assert.That(nextCalled, Is.True);
                    Assert.That(heartbeat.Stats.Requests.Total, Is.EqualTo(forceProtectionOff ? 0 : 1));
                    Assert.That(heartbeat.Routes.Count(), Is.EqualTo(forceProtectionOff ? 0 : 1));
                });
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
        }

        [TestCase(1024, 1024)]
        [TestCase(1025, 1024)]
        [TestCase(1025, 4096)]
        public async Task InvokeAsync_FormInspectionLimit_DoesNotChangeApplicationHandling(int count, int valueCountLimit)
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                var body = string.Join("&", Enumerable.Repeat("field=value", count));
                var context = new DefaultHttpContext();
                context.Request.Scheme = "http";
                context.Request.Host = new HostString("test.local");
                context.Request.Path = "/api/test";
                context.Request.Method = "POST";
                context.Request.ContentType = "application/x-www-form-urlencoded";
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
                if (valueCountLimit != 1024)
                {
                    context.Features.Set<IFormFeature>(new FormFeature(context.Request, new FormOptions { ValueCountLimit = valueCountLimit }));
                }
                var nextCalled = false;

                await _contextMiddleware.InvokeAsync(context, async httpContext =>
                {
                    nextCalled = true;
                    using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
                    Assert.That(await reader.ReadToEndAsync(), Is.EqualTo(body));
                    httpContext.Request.Body.Position = 0;
                    if (count > valueCountLimit)
                    {
                        Assert.ThrowsAsync<InvalidDataException>(async () => await httpContext.Request.ReadFormAsync());
                    }
                    else
                    {
                        var form = await httpContext.Request.ReadFormAsync();
                        Assert.That(form["field"].Count, Is.EqualTo(count));
                    }
                    httpContext.Response.StatusCode = StatusCodes.Status201Created;
                });

                Assert.That(nextCalled, Is.True);
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
                var captured = context.Items["Aikido.Zen.Context"] as Context;
                Assert.That(captured, Is.Not.Null);
                Assert.That(captured!.ParsedUserInput.Count(input => input.Location == "body.field"), Is.EqualTo(count > valueCountLimit ? 0 : count + 1));
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
        }

        [TestCase("application/x-www-form-urlencoded _", 2)]
        [TestCase("application/x-www-form-urlencoded; charset=\"utf-8", 2)]
        [TestCase("application/x-www-form-urlencoded _", 1024)]
        [TestCase("application/x-www-form-urlencoded _", 1025)]
        public async Task InvokeAsync_MalformedFormContentType_UsesBoundedInspectionWithoutChangingRequest(string contentType, int count)
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                var body = string.Join("&", Enumerable.Range(0, count).Select(index => $"field=value{index}"));
                var context = new DefaultHttpContext();
                context.Request.Scheme = "http";
                context.Request.Host = new HostString("test.local");
                context.Request.Path = "/api/test";
                context.Request.Method = "POST";
                context.Request.ContentType = contentType;
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
                Assert.That(context.Request.HasFormContentType, Is.False);
                var nextCalled = false;

                await _contextMiddleware.InvokeAsync(context, async httpContext =>
                {
                    nextCalled = true;
                    Assert.That(httpContext.Request.ContentType, Is.EqualTo(contentType));
                    Assert.That(httpContext.Request.Body.Position, Is.Zero);
                    using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
                    Assert.That(await reader.ReadToEndAsync(), Is.EqualTo(body));
                    httpContext.Response.StatusCode = StatusCodes.Status201Created;
                });

                Assert.That(nextCalled, Is.True);
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status201Created));
                var captured = context.Items["Aikido.Zen.Context"] as Context;
                Assert.That(captured, Is.Not.Null);
                var bodyValues = captured!.ParsedUserInput.Where(input => input.Location == "body.field").Select(input => input.Value);
                var expected = count > 1024
                    ? Array.Empty<string>()
                    : Enumerable.Range(0, count).Select(index => $"value{index}").Append(string.Join(",", Enumerable.Range(0, count).Select(index => $"value{index}")));
                Assert.That(bodyValues, Is.EqualTo(expected));
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
        }

        [Test]
        public async Task InvokeAsync_BypassedIp_CallsNextWithBypassedFlagWithoutCapturingStats()
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            const string bypassedIp = "93.184.216.34";
            var context = new DefaultHttpContext();
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("test.local");
            context.Request.Path = "/api/create";
            context.Request.Method = "POST";
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"name":"test"}"""));
            context.Connection.RemoteIpAddress = IPAddress.Parse(bypassedIp);
            var queryFeature = new Mock<IQueryFeature>();
            queryFeature.SetupGet(feature => feature.Query).Throws(new InvalidOperationException("Query must not be read for bypassed requests"));
            context.Features.Set(queryFeature.Object);

            var nextCalled = false;
            RequestDelegate next = httpContext =>
            {
                nextCalled = true;
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            };

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.BlockList.UpdateBypassedIps(new[] { bypassedIp });

                await _contextMiddleware.InvokeAsync(context, next);

                Assert.Multiple(() =>
                {
                    Assert.That(nextCalled, Is.True);
                    Assert.That(context.Items.ContainsKey("Aikido.Zen.Context"), Is.True);
                    var aikidoContext = context.Items["Aikido.Zen.Context"] as Context;
                    Assert.That(aikidoContext, Is.Not.Null);
                    Assert.That(Context.IsBypassed(aikidoContext), Is.True);
                    Assert.That(aikidoContext?.Method, Is.Empty);
                    Assert.That(aikidoContext?.Route, Is.Empty);
                    Assert.That(Agent.Instance.Context.Requests, Is.EqualTo(0));
                    Assert.That(Agent.Instance.Context.Routes, Is.Empty);
                });
                queryFeature.VerifyGet(feature => feature.Query, Times.Never);
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
        }

        [Test]
        public async Task InvokeAsync_WhenNextThrows_StillRunsAttackWaveDetection()
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            const string ip = "203.0.113.211";

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();

                RequestDelegate next = httpContext =>
                {
                    httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    throw new InvalidOperationException("boom");
                };

                for (var i = 0; i < 15; i++)
                {
                    var context = new DefaultHttpContext();
                    context.Request.Scheme = "http";
                    context.Request.Host = new HostString("test.local");
                    context.Request.Path = $"/api/pets/file{i}.env";
                    context.Request.Method = "GET";
                    context.Request.Body = new MemoryStream();
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ip);

                    var ex = Assert.ThrowsAsync<InvalidOperationException>(
                        () => _contextMiddleware.InvokeAsync(context, next));
                    Assert.That(ex?.Message, Is.EqualTo("boom"));
                }

                Assert.That(Agent.Instance.Context.AttackWavesDetected, Is.EqualTo(1));
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
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
            _mockHttpContext.Setup(c => c.Request.Path).Returns((string)null!);

            // Act
            var route = _contextMiddleware.GetParametrizedRoute(_mockHttpContext.Object);

            // Assert
            Assert.That("/", Is.EqualTo(route));
        }

        [Test]
        public async Task InvokeAsync_CapturesCompleteContextAndRateLimitGroupBeforeNext()
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.Method = "POST";
            httpContext.Request.QueryString = new QueryString("?q=search");
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Headers["X-Custom"] = new Microsoft.Extensions.Primitives.StringValues(new[] { "first", "second" });
            httpContext.Request.Headers["Cookie"] = "session=session-1";
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"name":"test"}"""));
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            Aikido.Zen.DotNetCore.Zen.SetRateLimitGroup("team-1", httpContext);

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                var nextCalled = false;
                await _contextMiddleware.InvokeAsync(httpContext, current =>
                {
                    nextCalled = true;
                    var context = (Context)current.Items["Aikido.Zen.Context"]!;
                    Assert.Multiple(() =>
                    {
                        Assert.That(context.RateLimitGroup, Is.EqualTo("team-1"));
                        Assert.That(context.RemoteAddress, Is.EqualTo("1.2.3.4"));
                        Assert.That(context.Method, Is.EqualTo("POST"));
                        Assert.That(context.Query["q"], Is.EqualTo("search"));
                        Assert.That(context.Headers["X-Custom"], Is.EqualTo("first"));
                        Assert.That(context.Headers["X-Custom[1]"], Is.EqualTo("second"));
                        Assert.That(context.Cookies["session"], Is.EqualTo("session-1"));
                        Assert.That(context.ParsedUserInput.Single(input => input.Location == "body.name").Value, Is.EqualTo("test"));
                        Assert.That(context.ParsedUserInput.Single(input => input.Location == "query.q").Value, Is.EqualTo("search"));
                        Assert.That(context.ParsedUserInput.Single(input => input.Location == "headers.X-Custom").Value, Is.EqualTo("first,second"));
                        Assert.That(((Dictionary<string, object>)context.ParsedBody)["name"], Is.EqualTo("test"));
                        Assert.That(context.Body, Is.SameAs(current.Request.Body));
                        Assert.That(context.Body.Position, Is.Zero);
                    });
                    return Task.CompletedTask;
                });
                Assert.That(nextCalled, Is.True);
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
        }

        [Test]
        public void InvokeAsync_WhenCaptureFails_PropagatesNextExceptionWithoutRetrying()
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
            var queryFeature = new Mock<IQueryFeature>();
            queryFeature.SetupGet(feature => feature.Query).Throws(new InvalidOperationException("Cannot read query"));
            httpContext.Features.Set(queryFeature.Object);
            var nextCalls = 0;
            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                var exception = Assert.ThrowsAsync<ApplicationException>(() => _contextMiddleware.InvokeAsync(httpContext, _ =>
                {
                    nextCalls++;
                    throw new ApplicationException("Downstream failure");
                }));
                Assert.Multiple(() =>
                {
                    Assert.That(exception!.Message, Is.EqualTo("Downstream failure"));
                    Assert.That(nextCalls, Is.EqualTo(1));
                    Assert.That(httpContext.Items.ContainsKey("Aikido.Zen.Context"), Is.False);
                });
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task InvokeAsync_RestoresSynchronousIoBeforeNext(bool initialAllowSynchronousIO)
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Method = "POST";
            httpContext.Request.ContentType = "application/xml";
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("<root><name>test</name></root>"));
            var bodyControl = new Mock<IHttpBodyControlFeature>();
            bodyControl.SetupProperty(feature => feature.AllowSynchronousIO, initialAllowSynchronousIO);
            httpContext.Features.Set(bodyControl.Object);
            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                await _contextMiddleware.InvokeAsync(httpContext, current =>
                {
                    Assert.That(current.Items.ContainsKey("Aikido.Zen.Context"), Is.True);
                    Assert.That(bodyControl.Object.AllowSynchronousIO, Is.EqualTo(initialAllowSynchronousIO));
                    return Task.CompletedTask;
                });
                bodyControl.VerifySet(feature => feature.AllowSynchronousIO = true, Times.AtLeastOnce);
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
            }
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
            var result = (IDictionary<string, string>)method!.Invoke(null, new object[] { mockQuery.Object })!;

            // Assert
            Assert.That(result.ContainsKey("param1"), Is.True);
            Assert.That("value1", Is.EqualTo(result["param1"]));
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
            var result = (IDictionary<string, string>)method!.Invoke(null, new object[] { mockQuery.Object })!;

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
            var result = (IDictionary<string, string>)method!.Invoke(null, new object[] { mockHeaders.Object })!;

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
            var result = (IDictionary<string, string>)method!.Invoke(null, new object[] { mockHeaders.Object })!;

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
