using System;
using System.Collections.Generic;
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

        private bool responseHandled = false;

        public void Init(HttpApplication context)
        {
            responseHandled = false;
            LogHelper.DebugLog(Agent.Logger, "ContextModule initialized");
            context.PostAuthenticateRequest += Context_PostAuthenticateRequest;
            // we add the .Wait(), because we want our module to handle exceptions properly
            context.BeginRequest += (sender, e) => Task.Run(() => Context_BeginRequest(sender, e)).Wait();
            // we try to discover the route as early as possible (we just need a statuscode), but we fallback to other listeners in case some are skipped.
            context.PreSendRequestHeaders += Context_EndRequest;
            context.EndRequest += Context_EndRequest;
            context.PostRequestHandlerExecute += Context_EndRequest;
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
            LogHelper.DebugLog(Agent.Logger, "Capturing request context");
            var httpContext = ((HttpApplication)sender).Context;
            try
            {
                responseHandled = false;
                // if the ip is bypassed, skip the handling of the request
                if (Agent.Instance.Context.BlockList.IsIPBypassed(GetClientIp(httpContext)) || EnvironmentHelper.IsDisabled)
                {
                    return;
                }

                var context = new Context
                {
                    Url = httpContext.Request.Path,
                    Method = httpContext.Request.HttpMethod,
                    Query = FlattenQueryParameters(httpContext.Request.QueryString),
                    Headers = FlattenHeaders(httpContext.Request.Headers),
                    RemoteAddress = httpContext.Request.UserHostAddress ?? string.Empty,
                    Cookies = httpContext.Request.Cookies.AllKeys.ToDictionary(k => k, k => httpContext.Request.Cookies[k].Value),
                    User = (User)httpContext.Items["Aikido.Zen.CurrentUser"],
                    UserAgent = httpContext.Request.UserAgent,
                    Source = "DotNetFramework",
                    Route = GetParametrizedRoute(httpContext),
                };

                Agent.Instance.SetContextMiddlewareInstalled(true);

                string clientIp = GetClientIp(httpContext);

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
                httpContext.Items["Aikido.Zen.Context"] = context;
            }
            catch (Exception ex)
            {
                // pass through
                LogHelper.ErrorLog(Agent.Logger, $"Error capturing request {ex.Message}");
            }
            finally
            {
                httpContext.Request.InputStream.Position = 0;
            }

        }

        private void Context_EndRequest(object sender, EventArgs e)
        {
            try
            {
                if (responseHandled)
                {
                    return;
                }

                var httpContext = ((HttpApplication)sender).Context;
                var aikidoContext = (Context)httpContext.Items["Aikido.Zen.Context"];
                if (aikidoContext == null)
                {
                    LogHelper.DebugLog(Agent.Logger, "Aikido context is null, skipping route");
                    return;
                }
                responseHandled = true;

                int statusCode = httpContext.Response.StatusCode;
                if (RouteHelper.ShouldAddRoute(aikidoContext, statusCode))
                {
                    Agent.Instance.AddRoute(aikidoContext);
                    Agent.Instance.IncrementTotalRequestCount();
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error adding route: {ex.Message}");
            }
        }

        private static string GetClientIp(HttpContext httpContext)
        {
            return !string.IsNullOrEmpty(httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"])
                ? httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]
                : httpContext.Request.ServerVariables["REMOTE_ADDR"];
        }

        /// <summary>
        /// Flattens query parameters into individual dictionary entries with indexing for multiple values.
        /// </summary>
        /// <param name="queryString">The query string collection</param>
        /// <returns>A dictionary with flattened query parameters</returns>
        private static IDictionary<string, string> FlattenQueryParameters(System.Collections.Specialized.NameValueCollection queryString)
        {
            var result = new Dictionary<string, string>();

            foreach (string key in queryString.AllKeys)
            {
                var values = queryString.GetValues(key);
                if (values != null)
                {
                    if (values.Length == 1)
                    {
                        result[key] = values[0];
                    }
                    else
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            string dictKey = i == 0 ? key : $"{key}[{i}]";
                            result[dictKey] = values[i];
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Flattens headers into individual dictionary entries with indexing for multiple values.
        /// </summary>
        /// <param name="headers">The headers collection</param>
        /// <returns>A dictionary with flattened headers</returns>
        private static IDictionary<string, string> FlattenHeaders(System.Collections.Specialized.NameValueCollection headers)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string key in headers.AllKeys)
            {
                var values = headers.GetValues(key);
                if (values != null)
                {
                    if (values.Length == 1)
                    {
                        result[key] = values[0];
                    }
                    else
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            string dictKey = i == 0 ? key : $"{key}[{i}]";
                            result[dictKey] = values[i];
                        }
                    }
                }
            }

            return result;
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
            if (string.IsNullOrEmpty(routePattern))
            {
                return "/";
            }

            // Ensure the request path starts with a slash for consistency
            routePattern = "/" + routePattern.TrimStart('/');

            // Check for an exact match endpoint
            var frameworkRoutes = RouteTable.Routes.Cast<RouteBase>()
                .Select(route => GetRoutePattern(route))
                .ToList();

            var exactEndpoint = frameworkRoutes.FirstOrDefault(rp => rp == routePattern);

            if (exactEndpoint != null)
            {
                // for too generic routes, we prefer to use the exact match route
                // since some applications create a catch-all route for all requests e.g. /{slug}
                if (RouteHelper.PathIsSingleSegment(exactEndpoint))
                {
                    return routePattern;
                }
                return exactEndpoint;
            }

            var bestMatchedRoute = frameworkRoutes
                .Where(rp => RouteHelper.MatchRoute(rp, context.Request.Path))
                .OrderByDescending(rp => rp.Count(c => c == '/')) // prioritize more specific routes
                .ThenBy(rp => rp.Count(c => c == '{')) // prioritize routes with fewer parameters
                .FirstOrDefault();

            if (bestMatchedRoute != null)
            {
                // for too generic routes, we prefer to use the exact match route
                // since some applications create a catch-all route for all requests e.g. /{slug}
                if (RouteHelper.PathIsSingleSegment(bestMatchedRoute))
                {
                    return routePattern;
                }
                return bestMatchedRoute;
            }

            // If no route was found, we use a custom algorithm to find parameters in the url
            var parameterizedRoute = RouteParameterHelper.BuildRouteFromUrl(context.Request.Url.ToString());
            if (!string.IsNullOrEmpty(parameterizedRoute))
            {
                routePattern = parameterizedRoute;
            }

            return routePattern;
        }

        private string GetRoutePattern(RouteBase route)
        {
            string routePattern = null;
            if (route is System.Web.Routing.Route)
            {
                routePattern = (route as System.Web.Routing.Route).Url;
            }
            // ensure the leading slash from the route pattern, to ensure we don't distinguish for example between api/users and /api/users
            return "/" + routePattern?.TrimStart('/');
        }
    }
}
