using System;
using Aikido.Zen.Core;
using System.Web;
using System.Linq;
using System.IO;

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
			context.OnExecuteRequestStep(PopulateContext);
		}

		private void PopulateContext(HttpContextBase httpContext, Action action)
		{
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

			action();
		}
	}
}
