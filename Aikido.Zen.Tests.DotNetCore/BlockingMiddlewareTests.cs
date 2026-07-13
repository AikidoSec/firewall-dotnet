using System.Net;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class BlockingMiddlewareTests
    {
        /// <summary>
        /// ContextMiddleware stores the client IP resolved by ASP.NET proxy handling
        /// on Context.RemoteAddress. The Zen middleware pipeline must attribute the
        /// authenticated user to that resolved value.
        /// </summary>
        [Test]
        public async Task InvokeAsync_RecordsUser_WithResolvedClientIpFromContext()
        {
            // Arrange
            const string resolvedClientIp = "203.0.113.7";
            const string userId = "user42";
            var user = new User(userId, "User Forty Two");

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.Method = "GET";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(resolvedClientIp);
            httpContext.Items["Aikido.Zen.CurrentUser"] = user;

            try
            {
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();

                var contextMiddleware = new ContextMiddleware([]);
                var blockingMiddleware = new BlockingMiddleware();

                // Act
                await contextMiddleware.InvokeAsync(
                    httpContext,
                    requestContext => blockingMiddleware.InvokeAsync(requestContext, _ => Task.CompletedTask));

                // Assert
                var recorded = Agent.Instance.Context.Users.SingleOrDefault(u => u.Id == userId);
                Assert.Multiple(() =>
                {
                    Assert.That(recorded, Is.Not.Null, "User should have been recorded by the Zen middleware pipeline");
                    Assert.That(recorded?.LastIpAddress, Is.EqualTo(resolvedClientIp));
                });
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
            }
        }

        [Test]
        public async Task InvokeAsync_DoesNotCaptureUserAgain()
        {
            const string remoteAddress = "203.0.113.7";
            const string userId = "user42";
            var user = new User(userId, "User Forty Two");
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.Method = "GET";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            httpContext.Items["Aikido.Zen.CurrentUser"] = user;

            try
            {
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();

                var contextMiddleware = new ContextMiddleware([]);
                var blockingMiddleware = new BlockingMiddleware();

                await contextMiddleware.InvokeAsync(
                    httpContext,
                    requestContext => blockingMiddleware.InvokeAsync(requestContext, _ => Task.CompletedTask));

                var recorded = Agent.Instance.Context.Users.SingleOrDefault(u => u.Id == userId);
                Assert.That(recorded?.Hits, Is.EqualTo(1), "BlockingMiddleware must not count the same request twice");
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
            }
        }

        [Test]
        public async Task InvokeAsync_RateLimitedResponse_SetsRetryAfterInSeconds()
        {
            const string route = "/api/retry-after-test";
            const string remoteAddress = "198.51.100.42";

            try
            {
                RateLimitingHelper.ResetCache(10000, 120 * 60 * 1000);
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.UpdateRatelimitedRoutes(new[]
                {
                    new EndpointConfig
                    {
                        Method = "GET",
                        Route = route,
                        RateLimiting = new RateLimitingConfig
                        {
                            Enabled = true,
                            MaxRequests = 1,
                            WindowSizeInMS = 60000
                        }
                    }
                });

                var middleware = new BlockingMiddleware();
                var nextCalls = 0;

                await middleware.InvokeAsync(
                    CreateHttpContext(route, remoteAddress),
                    _ =>
                    {
                        nextCalls++;
                        return Task.CompletedTask;
                    });

                var rateLimitedContext = CreateHttpContext(route, remoteAddress);

                await middleware.InvokeAsync(
                    rateLimitedContext,
                    _ =>
                    {
                        nextCalls++;
                        return Task.CompletedTask;
                    });

                Assert.Multiple(() =>
                {
                    Assert.That(rateLimitedContext.Response.StatusCode, Is.EqualTo(429));
                    Assert.That(rateLimitedContext.Response.Headers["Retry-After"].ToString(), Is.EqualTo("60"));
                    Assert.That(nextCalls, Is.EqualTo(1));
                });
            }
            finally
            {
                RateLimitingHelper.ResetCache(10000, 120 * 60 * 1000);
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
            }
        }

        private static DefaultHttpContext CreateHttpContext(string route, string remoteAddress)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Path = route;
            httpContext.Request.Method = "GET";
            httpContext.Items["Aikido.Zen.Context"] = new Context
            {
                Method = "GET",
                Route = route,
                RemoteAddress = remoteAddress,
            };

            return httpContext;
        }
    }
}
