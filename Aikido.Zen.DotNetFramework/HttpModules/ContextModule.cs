using System;
using Aikido.Zen.Core;
using System.Web;
using System.Linq;
using Aikido.Zen.DotNetFramework.Configuration;

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

			context.BeginRequest += Context_BeginRequest;
			context.EndRequest += Context_EndRequest;
			context.Error += Context_Error;
		}

		private void Context_BeginRequest(object sender, EventArgs e)
		{
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
                : HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
            // Add request information to the agent, which will collect routes, users and stats
            // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
            Agent.Instance.CaptureInboundRequest(context.User, httpContext.Request.Url.AbsolutePath, context.Method, clientIp);

			if (httpContext.Request.ContentLength > 0)
			{
				try
				{
					// We read the stream to a buffer, then reset the stream position
					var buffer = new byte[httpContext.Request.ContentLength];
					httpContext.Request.InputStream.Read(buffer, 0, buffer.Length);
                    httpContext.Request.InputStream.Position = 0;
                    var body = System.Text.Encoding.UTF8.GetString(buffer);
                    context.Body = body;
				}
				catch (Exception)
				{
					httpContext.Request.InputStream.Position = 0;
				}

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
