using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models.Ip;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// This class holds the state and configuration for the agent.
    /// </summary>
    public class AgentContext
    {
        private readonly AgentStats _stats = new AgentStats();
        private readonly AgentConfiguration _config = new AgentConfiguration();

        public long ConfigLastUpdated { get; set; } = 0;
        public bool ContextMiddlewareInstalled { get; set; } = false;
        public bool BlockingMiddlewareInstalled { get; set; } = false;

        public void AddRequest()
        {
            _stats.OnRequest();
        }

        public void AddAbortedRequest()
        {
            _stats.OnAbortedRequest();
        }

        public void AddAttackDetected(bool blocked = false)
        {
            _stats.OnDetectedAttack(blocked);
        }

        public void AddHostname(string hostname)
        {
            _stats.AddHostname(hostname);
        }

        /// <summary>
        /// Adds or updates a user in the context, tracking their IP address and last seen time.
        /// Increments the user's hit count upon access (add or update).
        /// Handles LFU eviction if the maximum number of users is exceeded when adding a new user.
        /// </summary>
        /// <param name="user">The user object containing Id and Name.</param>
        /// <param name="ipAddress">The IP address associated with this user access.</param>
        public void AddUser(User user, string ipAddress)
        {
            _stats.AddUser(user, ipAddress);
        }

        public void AddRoute(Context context)
        {
            _stats.AddRoute(context);
        }

        public void Clear()
        {
            _config.Clear();
            _stats.Reset();
            ConfigLastUpdated = 0;
        }

        /// <summary>
        /// Records the details of an inspected operation call.
        /// </summary>
        /// <param name="operation">The operation name.</param>
        /// <param name="kind">The kind of operation.</param>
        /// <param name="durationInMs">The duration of the call in milliseconds.</param>
        /// <param name="attackDetected">Indicates whether an attack was detected during this call.</param>
        public void OnInspectedCall(string operation, string kind, double durationInMs, bool attackDetected, bool blocked, bool withoutContext)
        {
            _stats.OnInspectedCall(operation, kind, durationInMs, attackDetected, blocked, withoutContext);
        }

        /// <summary>
        /// Checks if the request should be blocked based on the context and the block list.
        /// </summary>
        /// <param name="context">The context of the request.</param>
        /// <param name="reason">The reason for blocking the request.</param>
        /// <returns>True if the request should be blocked, false otherwise.</returns>
        public bool IsBlocked(Context context, out string reason)
        {
            reason = null;
            // if the ip is bypassed, we DON'T block the request (return false)
            if (BlockList.IsIPBypassed(context.RemoteAddress))
            {
                return false;
            }
            // Check if user exists and is blocked
            if (context.User != null && IsUserBlocked(context.User.Id))
            {
                reason = "User is blocked";
                return true;
            }
            if (_config.BlockList.IsBlocked(context, out reason))
            {
                return true;
            }
            if (IsUserAgentBlocked(context.UserAgent))
            {
                reason = "You are not allowed to access this resource because you have been identified as a bot.";
                return true;
            }
            return false;
        }

        public bool IsUserBlocked(string userId)
        {
            return _config.IsUserBlocked(userId);
        }

        public bool IsUserAgentBlocked(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return false;

            return Config.IsUserAgentBlocked(userAgent);
        }

        public void UpdateBlockedUsers(IEnumerable<string> users)
        {
            _config.UpdateBlockedUsers(users);
        }

        public void UpdateRatelimitedRoutes(IEnumerable<EndpointConfig> endpoints)
        {
            _config.UpdateRatelimitedRoutes(endpoints);
        }

        public void UpdateConfig(ReportingAPIResponse response)
        {
            _config.UpdateConfig(response);
            ConfigLastUpdated = response?.ConfigUpdatedAt ?? 0;
        }

        public void UpdateFirewallLists(FirewallListsAPIResponse response)
        {
            _config.UpdateFirewallLists(response);
        }

        public void UpdateBlockedUserAgents(Regex blockedUserAgents)
        {
            _config.UpdateBlockedUserAgents(blockedUserAgents);
        }

        public IEnumerable<Host> Hostnames => _stats.Hostnames;
        public IEnumerable<UserExtended> Users => _stats.Users;
        public IEnumerable<Route> Routes => _stats.Routes;

        public IEnumerable<EndpointConfig> Endpoints => _config.Endpoints;
        public int Requests => _stats.Requests.Total;
        public int RequestsAborted => _stats.Requests.Aborted;
        public int AttacksDetected => _stats.Requests.AttacksDetected.Total;
        public int AttacksBlocked => _stats.Requests.AttacksDetected.Blocked;
        public long Started => _stats.StartedAt;
        public BlockList BlockList => _config.BlockList;
        public Regex BlockedUserAgents => _config.BlockedUserAgents;
        public AgentStats Stats => _stats;
        public AgentConfiguration Config => _config;
    }
}
