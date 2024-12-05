using Aikido.Zen.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Aikido.Zen.DotNetCore.Middleware
{
	public class ContextMiddleware : IMiddleware
	{
		private readonly Agent _agent;
		private readonly string _apiToken;

		public ContextMiddleware(Agent agent, IOptions<AikidoOptions> config)
		{
			_agent = agent;
			_apiToken = config.Value.AikidoToken;
		}

		public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
		{
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

            // add request context to agent, so it can be included in the next heartbeat
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
            _agent.AddRequestContext(httpContext.Request.Host.Value, context.User, httpContext.Request.Path, context.Method, clientIp);

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
