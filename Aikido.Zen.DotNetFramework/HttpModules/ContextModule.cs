using System;
using Aikido.Zen.Core;
using System.Web;
using System.Linq;
using Aikido.Zen.DotNetFramework.Configuration;
using Aikido.Zen.Core.Api;

namespace Aikido.Zen.DotNetFramework.HttpModules
{
	internal class ContextModule : IHttpModule
	{
		private static Agent _agent;
		private static string _apiToken;

		public void Dispose()
		{
			throw new NotImplementedException();
		}

		public void Init(HttpApplication context)
		{
			// Initialize agent if not already done
			if (_agent == null)
			{
				var config = ZenConfiguration.Config;
				_apiToken = config.ZenToken;
				_agent = Firewall.Agent;
			}

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

			if (httpContext.Request.ContentLength > 0)
			{
				try
				{
					var buffer = new byte[httpContext.Request.ContentLength];
					httpContext.Request.InputStream.Read(buffer, 0, buffer.Length);
					context.Body = System.Text.Encoding.UTF8.GetString(buffer);
					httpContext.Request.InputStream.Position = 0;
				}
				catch (Exception)
				{
					httpContext.Request.InputStream.Position = 0;
				}

			}

			var id = httpContext.User.Identity.Name ?? string.Empty;
			var name = id;
			context.User = new User(id, name);

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
