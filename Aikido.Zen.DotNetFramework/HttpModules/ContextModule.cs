using System;
using Aikido.Zen.Core;
using System.Web;
using System.Linq;
using Aikido.Zen.Core.Helpers;
using System.Threading.Tasks;

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

            context.BeginRequest += (sender, e) => Task.Run(() => Context_BeginRequest(sender, e));
            context.EndRequest += Context_EndRequest;
            context.Error += Context_Error;
        }

        private async Task Context_BeginRequest(object sender, EventArgs e)
        {

            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return;
            }

            var httpContext = ((HttpApplication)sender).Context;

            var context = new Context
            {
                Url = httpContext.Request.Path,
                Method = httpContext.Request.HttpMethod,
                Query = httpContext.Request.QueryString.AllKeys.ToDictionary(k => k, k => httpContext.Request.QueryString.GetValues(k)),
                Headers = httpContext.Request.Headers.AllKeys.ToDictionary(k => k, k => httpContext.Request.Headers.GetValues(k)),
                RemoteAddress = httpContext.Request.UserHostAddress ?? string.Empty,
                Cookies = httpContext.Request.Cookies.AllKeys.ToDictionary(k => k, k => httpContext.Request.Cookies[k].Value)
            };

            var clientIp = !string.IsNullOrEmpty(httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"])
                ? httpContext.Request.ServerVariables["HTTP_X_FORWARDED_FOR"]
                : httpContext.Request.ServerVariables["REMOTE_ADDR"];
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

            }
            catch (Exception)
            {
                httpContext.Request.InputStream.Position = 0;
            }


            httpContext.Items["Aikido.Zen.Context"] = context;
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
    }
}
