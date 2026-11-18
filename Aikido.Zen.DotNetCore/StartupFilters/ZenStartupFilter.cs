using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Aikido.Zen.DotNetCore.StartupFilters;
public class ZenStartupFilter : IStartupFilter
{
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
	{
		app.Use(nextRequest => context =>
		{
			ContextMiddleware.CaptureClientIpHeader(context);
			return nextRequest(context);
		});
		next(app);
	};
}
