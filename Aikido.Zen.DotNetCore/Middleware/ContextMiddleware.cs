using Aikido.Zen.Core;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.Middleware
{
	public class ContextMiddleware : IMiddleware
	{
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
					using var reader = new StreamReader(httpContext.Request.Body);
					var body = await reader.ReadToEndAsync();
					context.Body = body;
				}
				catch (Exception)
				{
					// TODO: Log the exception here
				}
				finally
				{
					// Reset the stream position to 0, so it can be read again
					httpContext.Request.Body.Position = 0;
				}
			}
			var id = httpContext.User.Identities.FirstOrDefault()?.Claims.FirstOrDefault(c => c.Type == "id")?.Value
				?? string.Empty;
			var name = httpContext.User.Identity?.Name ?? string.Empty;
			context.User = new User(id, name);

			httpContext.Items["Aikido.Zen.Context"] = context;

			await next(httpContext);
		}
	}
}
