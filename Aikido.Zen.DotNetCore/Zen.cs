using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetCore.Patches;
using Aikido.Zen.DotNetCore.RuntimeSca;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace Aikido.Zen.DotNetCore
{
    public class Zen
    {

        private static IServiceProvider _serviceProvider;
        private static IHttpContextAccessor _httpContextAccessor;

        public static void Initialize(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public static void Start()
        {
            if (!Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException(
                    $"Aikido Zen does not support 32-bit processes. Detected process architecture: {RuntimeInformation.ProcessArchitecture}");
            }

            if (_serviceProvider == null || _httpContextAccessor == null)
            {
                throw new InvalidOperationException("Aikido.Zen.DotNetCore.Zen.Initialize must be called before Zen.Start().");
            }

            AgentInfoHelper.SetVersion(typeof(Zen).Assembly.GetName().Version.ToString());
            var options = _serviceProvider.GetRequiredService<IOptions<AikidoOptions>>();

            if (!string.IsNullOrEmpty(options?.Value?.AikidoToken))
            {
                var agent = Agent.NewInstance(_serviceProvider.GetRequiredService<IZenApi>());
                var agentLogger = _serviceProvider.GetService<ILogger<Agent>>();
                if (agentLogger != null)
                {
                    Agent.ConfigureLogger(agentLogger);
                }

                agent.Start();

                var exceptionLogger = _serviceProvider.GetService<ILogger<AikidoException>>();
                if (exceptionLogger != null)
                {
                    AikidoException.ConfigureLogger(exceptionLogger);
                }

                EnvironmentHelper.ReportValues();
                RuntimeAssemblyTracker.Instance.SubscribeToAppDomain(AppDomain.CurrentDomain);
            }

            Patcher.Patch();
        }

        public static void SetUser(string id, string name, HttpContext context)
        {
            var user = new User(id, name);
            context.Items["Aikido.Zen.CurrentUser"] = user;
        }

        public static Context GetContext()
        {
            if (_serviceProvider != null)
            {
                var contextAccessor = _serviceProvider.GetService(typeof(ContextAccessor)) as ContextAccessor;
                return contextAccessor?.CurrentContext;
            }
            return _httpContextAccessor?.HttpContext?.Items["Aikido.Zen.Context"] as Context;
        }

        public static User GetUser()
        {
            if (_serviceProvider == null)
            {
                return null;
            }
            var contextAccessor = _serviceProvider.GetService(typeof(ContextAccessor)) as ContextAccessor;
            return contextAccessor?.CurrentUser;
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
