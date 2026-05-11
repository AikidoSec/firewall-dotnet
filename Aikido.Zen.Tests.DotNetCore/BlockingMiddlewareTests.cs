using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class BlockingMiddlewareTests
    {
        /// <summary>
        /// ContextMiddleware resolves the real client IP from configured proxy
        /// headers (e.g. AIKIDO_CLIENT_IP_HEADER) and stores it on
        /// Context.RemoteAddress. BlockingMiddleware must attribute the
        /// authenticated user to that resolved value, not to
        /// Connection.RemoteIpAddress which is just the previous TCP hop
        /// (ingress/proxy pod) and therefore the same for every request.
        /// </summary>
        [Test]
        public async Task InvokeAsync_RecordsUser_WithResolvedClientIpFromContext()
        {
            // Arrange
            const string connectionRemoteIp = "10.0.0.5";   // previous TCP hop (e.g. ingress pod)
            const string resolvedClientIp = "203.0.113.7";  // real client, already resolved by ContextMiddleware
            const string userId = "user42";

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.Method = "GET";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(connectionRemoteIp);
            httpContext.Items["Aikido.Zen.Context"] = new Context
            {
                Method = "GET",
                Route = "/api/test",
                RemoteAddress = resolvedClientIp,
            };
            httpContext.Items["Aikido.Zen.CurrentUser"] = new User(userId, "User Forty Two");

            try
            {
                Agent.Instance.Context.Config.Clear();
                Agent.Instance.ClearContext();

                var middleware = new BlockingMiddleware();

                // Act
                await middleware.InvokeAsync(httpContext, _ => Task.CompletedTask);

                // Assert
                var recorded = Agent.Instance.Context.Users.SingleOrDefault(u => u.Id == userId);
                Assert.Multiple(() =>
                {
                    Assert.That(recorded, Is.Not.Null, "User should have been recorded by BlockingMiddleware");
                    Assert.That(recorded?.LastIpAddress, Is.EqualTo(resolvedClientIp));
                });
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.Clear();
            }
        }
    }
}
