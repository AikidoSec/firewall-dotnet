using Aikido.Zen.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Aikido.Zen.DotNetCore.Middleware
{
	public class ContextMiddleware : IMiddleware
	{
		private readonly Agent _agent;
		private readonly string _apiToken;

		public ContextMiddleware(Agent agent, IOptions<AikidoZenConfig> config)
		{
			_agent = agent;
			_apiToken = config.Value.ZenToken;
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

			if (httpContext.Request.ContentLength > 0)
			{
				try
				{
					var buffer = new byte[httpContext.Request.ContentLength.Value];
					await httpContext.Request.Body.ReadAsync(buffer, 0, buffer.Length);
					context.Body = System.Text.Encoding.UTF8.GetString(buffer);
					httpContext.Request.Body.Position = 0;
				}
				catch (Exception)
				{
					httpContext.Request.Body.Position = 0;
				}
			}
			var id = httpContext.User.Identities.FirstOrDefault()?.Claims.FirstOrDefault(c => c.Type == "id")?.Value
				?? string.Empty;
			var name = httpContext.User.Identity?.Name ?? string.Empty;
			context.User = new User(id, name);

			httpContext.Items["Aikido.Zen.Context"] = context;
		}
	}
}
