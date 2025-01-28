using System;
using Aikido.Zen.Core;
using System.Web;
using System.Linq;
using Aikido.Zen.Core.Helpers;
using System.Threading.Tasks;
using Context = Aikido.Zen.Core.Context;
using Aikido.Zen.Core.Models;
using System.Web.Routing;

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
                Route = GetRoute(httpContext),
            };

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
                Agent.Instance.CaptureInboundRequest(context);
            }
            catch
            {
                // pass through
            }
            finally
            {
                httpContext.Request.InputStream.Position = 0;
            }

            httpContext.Items["Aikido.Zen.Context"] = context;
        }

        private static string GetClientIp(HttpContext httpContext)
        {
            return !string.IsNullOrEmpty(httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"])
                ? httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]
                : httpContext.Request.ServerVariables["REMOTE_ADDR"];
        }

        private string GetRoute(HttpContext context)
        {
            string routePattern = null;
            // we use the .NET framework route collection to match against the request path,
            // this way, the routes found by Zen match the routes found by the .NET framework
            foreach (var route in RouteTable.Routes)
            {
                routePattern = GetRoutePattern(route);
                if (RouteHelper.MatchRoute(routePattern, context.Request.Path))
                    break;
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
            // remove the leading slash from the route pattern, to ensure we don't distinguish for example between api/users and /api/users
            return routePattern?.TrimStart('/');
            // remove the leading slash from the route pattern, to ensure we don't distinguish for example between api/users and /api/users
            return routePattern?.TrimStart('/');
        }
    }
}
