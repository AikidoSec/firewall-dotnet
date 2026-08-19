using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetCore.RuntimeSca;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using CorePatcher = Aikido.Zen.Core.Sinks.Patcher;

namespace Aikido.Zen.DotNetCore
{
    public static class Zen
    {
        private static volatile IHttpContextAccessor _httpContextAccessor;
        private static int _lateSetUserWarningLogged;

        internal static void Start(
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            // libzen_internals only available on 64
            if (!Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException(
                    $"Aikido Zen does not support 32-bit processes. Detected process architecture: {RuntimeInformation.ProcessArchitecture}");
            }

            AgentInfoHelper.SetAgentAssembly(typeof(Zen).Assembly);
            var options = serviceProvider.GetRequiredService<IOptions<AikidoOptions>>();
            Agent agent = null;

            if (!string.IsNullOrEmpty(options?.Value?.AikidoToken))
            {
                agent = Agent.NewInstance(serviceProvider.GetRequiredService<IZenApi>());
                var agentLogger = serviceProvider.GetService<ILogger<Agent>>();
                if (agentLogger != null)
                {
                    Agent.ConfigureLogger(agentLogger);
                }

                agent.Start();

                var exceptionLogger = serviceProvider.GetService<ILogger<AikidoException>>();
                if (exceptionLogger != null)
                {
                    AikidoException.ConfigureLogger(exceptionLogger);
                }

                EnvironmentHelper.ReportValues();
            }

            _httpContextAccessor = httpContextAccessor;
            CorePatcher.PatchSinks(GetContext);

            if (agent != null)
            {
                RuntimeAssemblyTracker.Instance.SubscribeToAppDomain(AppDomain.CurrentDomain);
            }

            try
            {
                serviceProvider
                    .GetService<IHostApplicationLifetime>()?
                    .ApplicationStopped.Register(() => Stop(agent));
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, "Failed to register the application shutdown callback");
            }
        }

        private static void Stop(Agent agent)
        {
            _httpContextAccessor = null;
            try
            {
                CorePatcher.Unpatch();
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, "Failed to unpatch sinks during application shutdown");
            }

            try
            {
                agent?.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, "Failed to dispose the agent during application shutdown");
            }
        }

        public static void SetUser(string id, string name, HttpContext context)
        {
            var user = new User(id, name);

            var aikidoContext = context.Items["Aikido.Zen.Context"] as Context;
            if (aikidoContext == null)
            {
                // Correct order: ContextMiddleware captures the stored user.
                context.Items["Aikido.Zen.CurrentUser"] = user;
                return;
            }

            if (Interlocked.Exchange(ref _lateSetUserWarningLogged, 1) == 0)
            {
                LogHelper.WarningLog(
                    Agent.Logger,
                    "Zen.SetUser(...) was called after the Zen middleware. Register the SetUser middleware before UseZenFirewall() so user blocking, and rate limiting work correctly.");
            }

            // Preserve late users for end-of-request reporting.
            // Blocking and rate limiting have already run.
            aikidoContext.User = user;
        }

        public static void SetRateLimitGroup(string id, HttpContext context)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            context.Items["Aikido.Zen.RateLimitGroup"] = id;
        }

        internal static Context GetContext()
        {
            return _httpContextAccessor?.HttpContext?.Items["Aikido.Zen.Context"] as Context;
        }

        /// <summary>
        /// Gets the current status of the Aikido Zen agent, including heartbeat reporting status.
        /// This method provides a snapshot of the agent's communication status with the Zen API.
        /// </summary>
        /// <returns>
        /// An <see cref="AgentStatus"/> object containing the current status information, including:
        /// - Heartbeat reporting status indicating whether the agent is successfully communicating with the Zen API
        /// - Success/failure state of recent API communications
        /// - Whether heartbeat reports have expired or are current
        /// </returns>
        public static AgentStatus Status()
        {
            return Agent.Instance?.GetCurrentStatus();
        }
    }
}
