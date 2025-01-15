using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.Server.Mock.Controllers
{
    /// <summary>
    /// Controller handling health check endpoints
    /// </summary>
    public class HealthController
    {
        public void ConfigureEndpoints(WebApplication app)
        {
            app.MapGet("/health", () => Results.Ok());
        }
    }
}