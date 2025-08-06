using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore
{
    public class Zen
    {

        private static IServiceProvider _serviceProvider;
        private static IHttpContextAccessor _httpContextAccessor;

        public static void Initialize(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
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
