using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore.Middleware
{
	public class ContextMiddleware : IMiddleware
	{
		public ContextMiddleware()
		{
		}

		public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
		{
            // this will be used to check for attacks
			var context = new Context
			{
				Url = httpContext.Request.Path.ToString(),
				Method = httpContext.Request.Method,			
				Query = httpContext.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToArray()),
				Headers = httpContext.Request.Headers
					.ToDictionary(h => h.Key, h => h.Value.ToArray()),
				RemoteAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
				Cookies = httpContext.Request.Cookies.ToDictionary(c => c.Key, c => c.Value)
			};

            // no need to use X-FORWARDED-FOR, .NET Core already handles this
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
            // Add request information to the agent, which will collect routes, users and stats
            // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
            Agent.Instance.CaptureInboundRequest(context.User, httpContext.Request.Path, context.Method, clientIp);

			if (httpContext.Request.ContentLength > 0)
			{
				try
				{
                    // we need to leave the body stream unread, so we copy it to a buffer
                    // and then replace the body with a new memory stream that we can read multiple times
                    var buffer = new byte[httpContext.Request.ContentLength.Value];
                    await httpContext.Request.Body.ReadAsync(buffer, 0, buffer.Length);
                    context.Body = System.Text.Encoding.UTF8.GetString(buffer);
                    httpContext.Request.Body = new MemoryStream(buffer);
				}
				catch (Exception)
				{
					httpContext.Request.Body.Position = 0;
				}
			}

			httpContext.Items["Aikido.Zen.Context"] = context;
            await next(httpContext);
		}
	}
}
