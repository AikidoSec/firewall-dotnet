using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Routing;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Context = Aikido.Zen.Core.Context;

[assembly: InternalsVisibleTo("Aikido.Zen.Tests.DotNetFramework")]
namespace Aikido.Zen.DotNetFramework.HttpModules
{
    /// <summary>
    /// This Http module is used to capture the context of incoming requests.
    /// </summary>
    /// <summary>
    /// This Http module is used to capture the context of incoming requests.
    /// </summary>
    internal class ContextModule : IHttpModule
    {
        public void Dispose()
        {
            // Nothing to dispose
        }

        public void Init(HttpApplication context)
        {
            context.PostAuthenticateRequest += Context_PostAuthenticateRequest;
            // we add the .Wait(), because we want our module to handle exceptions properly
            context.BeginRequest += (sender, e) => Task.Run(() => Context_BeginRequest(sender, e)).Wait();
            context.EndRequest += Context_EndRequest; // Subscribe to EndRequest event
        }

        private void Context_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            var user = Zen.SetUserAction(httpContext);
            httpContext.Items["Aikido.Zen.CurrentUser"] = user;
            var aikidoContext = (Context)httpContext.Items["Aikido.Zen.Context"];
            if (aikidoContext == null)
            {
                return;
            }
            var clientIp = GetClientIp(httpContext);
            Agent.Instance.CaptureUser(user, clientIp);
        }

        private async Task Context_BeginRequest(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            // if the ip is bypassed, skip the handling of the request
            if (Agent.Instance.Context.BlockList.IsIPBypassed(GetClientIp(httpContext)) || EnvironmentHelper.IsDisabled)
            {
                return;
            }

            var context = new Context
            {
                Url = httpContext.Request.Path,
                Method = httpContext.Request.HttpMethod,
                Query = httpContext.Request.QueryString.AllKeys.ToDictionary(k => k, k => httpContext.Request.QueryString.GetValues(k)),
                Headers = httpContext.Request.Headers.AllKeys.ToDictionary(k => k, k => httpContext.Request.Headers.GetValues(k)),
                RemoteAddress = httpContext.Request.UserHostAddress ?? string.Empty,
                Cookies = httpContext.Request.Cookies.AllKeys.ToDictionary(k => k, k => httpContext.Request.Cookies[k].Value),
                User = (User)httpContext.Items["Aikido.Zen.CurrentUser"],
                UserAgent = httpContext.Request.UserAgent,
                Source = "DotNetFramework",
                Route = GetParametrizedRoute(httpContext),
            };

            Agent.Instance.SetContextMiddlewareInstalled(true);

            string clientIp = GetClientIp(httpContext);

            try
            {
                var request = httpContext.Request;
                var httpData = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    queryParams: request.QueryString.AllKeys.ToDictionary(k => k, k => request.QueryString.Get(k)),
                    headers: request.Headers.AllKeys.ToDictionary(k => k, k => request.Headers.Get(k)),
                    cookies: request.Cookies.AllKeys.ToDictionary(k => k, k => request.Cookies[k].Value),
                    body: request.InputStream,
                    contentType: request.ContentType,
                    contentLength: request.ContentLength
                );
                context.ParsedUserInput = httpData.FlattenedData;
                context.Body = request.InputStream;
                context.ParsedBody = httpData.ParsedBody;
                Agent.Instance.CaptureRequestUser(context);
            }
            catch (Exception ex)
            {
                // pass through
                LogHelper.DebugLog(Agent.Logger, $"AIKIDO: Error capturing request {ex.Message}");
            }
            finally
            {
                httpContext.Request.InputStream.Position = 0;
            }

            httpContext.Items["Aikido.Zen.Context"] = context;
        }

        private void Context_EndRequest(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            var aikidoContext = (Context)httpContext.Items["Aikido.Zen.Context"];
            if (aikidoContext == null)
            {
                return;
            }

            int statusCode = httpContext.Response.StatusCode;
            if (RouteHelper.ShouldAddRoute(aikidoContext, statusCode))
            {
                Agent.Instance.AddRoute(aikidoContext);
                Agent.Instance.IncrementTotalRequestCount();
            }
        }

        private static string GetClientIp(HttpContext httpContext)
        {
            return !string.IsNullOrEmpty(httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"])
                ? httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]
                : httpContext.Request.ServerVariables["REMOTE_ADDR"];
        }

        /// <summary>
        /// Gets a parameterized route from the HTTP context, matching against the route collection
        /// and applying route parameter detection when needed.
        /// </summary>
        /// <param name="context">The HTTP context containing the request information</param>
        /// <returns>A parameterized route string with a leading slash</returns>
        internal string GetParametrizedRoute(HttpContext context)
        {
            var routePattern = context.Request.Path;
            if (routePattern == null)
            {
                return "/";
            }

            if (RouteHelper.PathIsSingleSegment(context.Request.Path))
            {
                return "/" + context.Request.Path.TrimStart('/');
            }

            // Use the .NET framework route collection to match against the request path
            var exactMatchRoute = RouteTable.Routes
                .Cast<RouteBase>()
                .Select(route => GetRoutePattern(route))
                .FirstOrDefault(rp => rp == context.Request.Path);

            if (exactMatchRoute != null)
            {
                return "/" + exactMatchRoute.TrimStart('/');
            }

            var matchedRoutes = RouteTable.Routes
                .Cast<RouteBase>()
                .Select(route => GetRoutePattern(route))
                .Where(rp => RouteHelper.MatchRoute(rp, context.Request.Path))
                .OrderByDescending(rp => rp.Count(c => c == '/')) // prioritize more specific routes
                .ThenBy(rp => rp.Count(c => c == '{')) // prioritize routes with fewer parameters
                .ToList();

            routePattern = matchedRoutes.FirstOrDefault();

            // If no route was found, use RouteParameterHelper as fallback
            if (string.IsNullOrEmpty(routePattern))
            {
                var parameterizedRoute = RouteParameterHelper.BuildRouteFromUrl(context.Request.Url.ToString());
                if (!string.IsNullOrEmpty(parameterizedRoute))
                {
                    routePattern = parameterizedRoute;
                }
                else
                {
                    routePattern = context.Request.Path;
                }
            }

            // Add a leading slash to the route pattern if not present
            // Ensure route pattern starts with a slash and handle null case
            if (string.IsNullOrEmpty(routePattern))
            {
                return "/";
            }

            return "/" + routePattern.TrimStart('/');
        }

        private string GetRoutePattern(RouteBase route)
        {
            string routePattern = null;
            if (route is System.Web.Routing.Route)
            {
                routePattern = (route as System.Web.Routing.Route).Url;
            }
            // remove the leading slash from the route pattern, to ensure we don't distinguish for example between api/users and /api/users
            return routePattern?.TrimStart('/');
        }
    }
}
