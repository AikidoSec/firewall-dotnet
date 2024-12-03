using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Aikido.Zen.DotNetCore.StartupFilters;
public class ZenStartupFilter : IStartupFilter
{
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		return builder =>
		{
			// Insert our middleware at the beginning of the pipeline
			builder.UseMiddleware<ContextMiddleware>();

			// Call the next registered startup filter
			next(builder);
		};
	}
}
