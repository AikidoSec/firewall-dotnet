using System;
using System.Collections.Concurrent;
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
    /// Manages configuration settings for the agent, including blocklists, firewall settings, and endpoint configurations.
    /// This class is thread-safe and handles concurrent access to its collections.
    /// </summary>
    public class AgentConfiguration
    {
        private const int MaxHostnames = 2000;
        private const int MaxUsers = 2000;
        private const int MaxRoutes = 5000;

        // Use ConcurrentLFUDictionary which handles LFU eviction internally
        private readonly ConcurrentLFUDictionary<string, Host> _hostnames = new ConcurrentLFUDictionary<string, Host>(MaxHostnames);
        private readonly ConcurrentLFUDictionary<string, Route> _routes = new ConcurrentLFUDictionary<string, Route>(MaxRoutes);
        private readonly ConcurrentLFUDictionary<string, UserExtended> _users = new ConcurrentLFUDictionary<string, UserExtended>(MaxUsers);

        private readonly ConcurrentDictionary<string, string> _blockedUsers = new ConcurrentDictionary<string, string>();
        private Regex _blockedUserAgents;
        private readonly BlockList _blockList = new BlockList();
        private List<EndpointConfig> _endpoints = new List<EndpointConfig>();
        private readonly object _endpointsLock = new object();

        public long ConfigLastUpdated { get; set; } = 0;
        public bool ContextMiddlewareInstalled { get; set; } = false;
        public bool BlockingMiddlewareInstalled { get; set; } = false;

        /// <summary>
        /// Adds or updates a hostname in the configuration.
        /// </summary>
        /// <param name="hostname">The hostname to add or update.</param>
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
        /// Adds or updates a user in the configuration, tracking their IP address and last seen time.
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

        /// <summary>
        /// Adds or updates a route in the configuration.
        /// </summary>
        /// <param name="context">The context containing route information.</param>
        public void AddRoute(Context context)
        {
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

        /// <summary>
        /// Clears all configuration data.
        /// </summary>
        public void Clear()
        {
            _hostnames.Clear();
            _users.Clear();
            _routes.Clear();
            _blockedUsers.Clear();
        }

        /// <summary>
        /// Checks if a user is blocked.
        /// </summary>
        /// <param name="userId">The user ID to check.</param>
        /// <returns>True if the user is blocked, false otherwise.</returns>
        public bool IsUserBlocked(string userId)
        {
            return _blockedUsers.ContainsKey(userId);
        }

        /// <summary>
        /// Checks if a user agent is blocked.
        /// </summary>
        /// <param name="userAgent">The user agent string to check.</param>
        /// <returns>True if the user agent is blocked, false otherwise.</returns>
        public bool IsUserAgentBlocked(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return false;

            return _blockedUserAgents?.IsMatch(userAgent) ?? false;
        }

        /// <summary>
        /// Updates the list of blocked users.
        /// </summary>
        /// <param name="users">The list of user IDs to block.</param>
        public void UpdateBlockedUsers(IEnumerable<string> users)
        {
            _blockedUsers.Clear();
            if (users != null)
            {
                foreach (var user in users)
                {
                    _blockedUsers.TryAdd(user, user);
                }
            }
        }

        /// <summary>
        /// Updates the list of rate-limited routes.
        /// </summary>
        /// <param name="endpoints">The list of endpoint configurations.</param>
        public void UpdateRatelimitedRoutes(IEnumerable<EndpointConfig> endpoints)
        {
            var newEndpoints = endpoints?.ToList() ?? new List<EndpointConfig>();
            lock (_endpointsLock)
            {
                _endpoints = newEndpoints;
            }
        }

        /// <summary>
        /// Updates the configuration based on the API response.
        /// </summary>
        /// <param name="response">The API response containing configuration updates.</param>
        public void UpdateConfig(ReportingAPIResponse response)
        {
            if (response == null) return;
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", response.Block ? "true" : "false");
            UpdateBlockedUsers(response.BlockedUserIds);
            BlockList.UpdateAllowedIpsPerEndpoint(response.Endpoints);
            BlockList.UpdateBypassedIps(response.BypassedIPAddresses);
            UpdateRatelimitedRoutes(response.Endpoints);
            ConfigLastUpdated = response.ConfigUpdatedAt;
        }

        /// <summary>
        /// Updates firewall lists based on the API response.
        /// </summary>
        /// <param name="response">The API response containing firewall list updates.</param>
        public void UpdateFirewallLists(FirewallListsAPIResponse response)
        {
            if (response == null)
            {
                BlockList.UpdateBlockedIps(new List<string>());
                BlockList.UpdateAllowedIps(new List<string>());
                UpdateBlockedUserAgents(null);
                return;
            }
            BlockList.UpdateBlockedIps(response.BlockedIps);
            BlockList.UpdateAllowedIps(response.AllowedIps);
            UpdateBlockedUserAgents(response.BlockedUserAgentsRegex);
        }

        /// <summary>
        /// Updates the blocked user agents regex pattern.
        /// </summary>
        /// <param name="blockedUserAgents">The regex pattern for blocked user agents.</param>
        public void UpdateBlockedUserAgents(Regex blockedUserAgents)
        {
            _blockedUserAgents = blockedUserAgents;
        }

        // Public properties
        public IEnumerable<Host> Hostnames => _hostnames.GetValues();
        public IEnumerable<UserExtended> Users => _users.GetValues();
        public IEnumerable<Route> Routes => _routes.GetValues();
        public IEnumerable<EndpointConfig> Endpoints
        {
            get
            {
                lock (_endpointsLock)
                {
                    return _endpoints?.ToList() ?? new List<EndpointConfig>();
                }
            }
        }
        public BlockList BlockList => _blockList;
        public Regex BlockedUserAgents => _blockedUserAgents;
    }
}
