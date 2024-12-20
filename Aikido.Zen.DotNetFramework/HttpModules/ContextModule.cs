using System;
using Aikido.Zen.Core;
using System.Web;
using System.Linq;
using Aikido.Zen.Core.Helpers;
using System.Threading.Tasks;
using Context = Aikido.Zen.Core.Context;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using System.Web.Routing;
using System.Net;

namespace Aikido.Zen.DotNetFramework.HttpModules
{
    internal class ContextModule : IHttpModule
    {

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Init(HttpApplication context)
        {
            context.PostAuthenticateRequest += Context_PostAuthenticateRequest;
            context.BeginRequest += (sender, e) => Task.Run(() => Context_BeginRequest(sender, e));
            context.EndRequest += Context_EndRequest;
            context.Error += Context_Error;
        }

        private void Context_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            var user = Zen.SetUserAction(httpContext);
            httpContext.Items["Aikido.Zen.CurrentUser"] = user;
            var clientIp = !string.IsNullOrEmpty(httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"])
                ? httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]
                : httpContext.Request.ServerVariables["REMOTE_ADDR"];
            Agent.Instance.CaptureUser(user, clientIp);
            // block the request if the user is blocked
            if (Agent.Instance.Context.IsBlocked(user, clientIp, $"{httpContext.Request.HttpMethod}|{httpContext.Request.Path}"))
            {
                Agent.Instance.Context.AddAbortedRequest();
                // don't actually block the request if we are in dry mode
                if (EnvironmentHelper.DryMode)
                {
                    return;
                }
                httpContext.Response.StatusCode = 403;
                // stop the request from being processed
                httpContext.Response.End();
                throw AikidoException.RequestBlocked($"{httpContext.Request.HttpMethod}|{httpContext.Request.Path}", clientIp);
            }
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
                Source = httpContext.Request.Path.ToString(),
                Route = GetRoute(httpContext),
            };

            string clientIp = GetClientIp(httpContext);
            // Add request information to the agent, which will collect routes, users and stats
            // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
            Agent.Instance.CaptureInboundRequest(context.User, httpContext.Request.Url.AbsolutePath, context.Method, clientIp);

            try
            {
                var request = httpContext.Request;
                // take all the user input and flatten it into a dictionary for easier processing
                var parsedUserInput = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    queryParams: request.QueryString.AllKeys.ToDictionary(k => k, k => request.QueryString.Get(k)),
                    headers: request.Headers.AllKeys.ToDictionary(k => k, k => request.Headers.Get(k)),
                    cookies: request.Cookies.AllKeys.ToDictionary(k => k, k => request.Cookies[k].Value),
                    body: request.InputStream,
                    contentType: request.ContentType,
                    contentLength: request.ContentLength
                );
                context.ParsedUserInput = parsedUserInput;
                context.Body = request.InputStream;

            }
            catch
            {
                // pass through
            }
            finally {
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

        private void Context_EndRequest(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            var context = (Context)httpContext.Items["Aikido.Zen.Context"];
        }

        private void Context_Error(object sender, EventArgs e)
        {
            var httpContext = ((HttpApplication)sender).Context;
            var context = (Context)httpContext.Items["Aikido.Zen.Context"];
            var exception = httpContext.Server.GetLastError();
        }

        private string GetRoute(HttpContext context) {
            string routePattern = null;
            foreach (var route in RouteTable.Routes) {
                routePattern = GetRoutePattern(route);
                if (RouteHelper.MatchRoute(routePattern, context.Request.Path))
                    break;
            }
            return routePattern;
        }

        private string GetRoutePattern(RouteBase route) {
            string routePattern = null;
            if (route is System.Web.Routing.Route)
            {
                routePattern = (route as System.Web.Routing.Route).Url;
            }
            return routePattern;
        }
    }
}
