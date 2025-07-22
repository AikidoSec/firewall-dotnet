using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;
using Aikido.Zen.Core.Models.Ip;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// This class holds the state and configuration for the agent.
    /// </summary>
    public class AgentContext
    {
        private const int MaxHostnames = 2000;
        private const int MaxUsers = 2000;
        private const int MaxRoutes = 5000;

        private readonly AgentStats _stats = new AgentStats();
        private readonly AgentConfiguration _config = new AgentConfiguration();
        private readonly ConcurrentLFUDictionary<string, Host> _hostnames = new ConcurrentLFUDictionary<string, Host>(MaxHostnames);
        private readonly ConcurrentLFUDictionary<string, UserExtended> _users = new ConcurrentLFUDictionary<string, UserExtended>(MaxUsers);
        private readonly ConcurrentLFUDictionary<string, Route> _routes = new ConcurrentLFUDictionary<string, Route>(MaxRoutes);

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
            if (string.IsNullOrWhiteSpace(hostname))
                return;
            var hostParts = hostname.Split(':');
            var name = hostParts[0];
            var port = hostParts.Length > 1 ? hostParts[1] : "80";
            int.TryParse(port, out int portNumber);

            var key = $"{name}:{port}";
            _hostnames.TryGet(key, out var host);
            if (host == null)
            {
                host = new Host { Hostname = name, Port = portNumber };
            }

            // when updating, we keep track of hits
            _hostnames.AddOrUpdate(key, host);
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
            if (user == null || string.IsNullOrEmpty(user.Id))
                return;

            UserExtended userExtended;
            if (_users.TryGet(user.Id, out var existingUser))
            {
                // User exists, update details before calling AddOrUpdate
                existingUser.Name = user.Name; // Update name in case it changed
                existingUser.LastIpAddress = ipAddress;
                existingUser.LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds();
                userExtended = existingUser;
            }
            else
            {
                // User does not exist, create a new one
                userExtended = new UserExtended(user.Id, user.Name)
                {
                    LastIpAddress = ipAddress,
                    LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds()
                };
            }

            // AddOrUpdate handles incrementing hits and eviction
            _users.AddOrUpdate(user.Id, userExtended);
        }

        public void AddRoute(Context context)
        {
            // return if context or route are empty
            if (context == null || string.IsNullOrEmpty(context.Route)) return;

            Route route;
            if (_routes.TryGet(context.Route, out var existingRoute))
            {
                // Route exists, update API info before calling AddOrUpdate
                OpenAPIHelper.UpdateApiInfo(context, existingRoute, EnvironmentHelper.MaxApiDiscoverySamples);
                route = existingRoute;
            }
            else
            {
                // Route does not exist, create a new one
                route = new Route
                {
                    Path = context.Route,
                    Method = context.Method,
                    ApiSpec = OpenAPIHelper.GetApiInfo(context),
                };
            }

            // AddOrUpdate handles incrementing hits and eviction
            _routes.AddOrUpdate(context.Route, route);
        }

        public void UpdateRequestStats(Context context)
        {
            if (context == null) return;

            var monitoredIpKeys = Config.GetMatchingMonitoredIPListKeys(context.RemoteAddress);
            if (monitoredIpKeys.Any())
            {
                _stats.OnIPAddressMatches(monitoredIpKeys);
            }

            var blockedIpKeys = Config.GetMatchingBlockedIPListKeys(context.RemoteAddress);
            if (blockedIpKeys.Any())
            {
                _stats.OnIPAddressMatches(blockedIpKeys);
            }

            var userAgentKeys = Config.GetMatchingUserAgentKeys(context.UserAgent);
            if (userAgentKeys.Any())
            {
                _stats.OnUserAgentMatches(userAgentKeys);
            }
        }

        public void Clear()
        {
            _config.Clear();
            _stats.Reset();
            _hostnames.Clear();
            _users.Clear();
            _routes.Clear();
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

        public IEnumerable<Host> Hostnames => _hostnames.GetValues();
        public IEnumerable<UserExtended> Users => _users.GetValues();
        public IEnumerable<Route> Routes => _routes.GetValues();

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
