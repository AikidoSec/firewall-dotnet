using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models.Ip;
using Aikido.Zen.Core.Models.Events;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Manages configuration settings for the agent, including blocklists, firewall settings, and endpoint configurations.
    /// This class is thread-safe and handles concurrent access to its collections.
    /// </summary>
    public class AgentConfiguration
    {
        private readonly ConcurrentDictionary<string, string> _blockedUsers = new ConcurrentDictionary<string, string>();
        private Regex _blockedUserAgents;
        private BlockList _blockList = new BlockList();
        private List<EndpointConfig> _endpoints = new List<EndpointConfig>();
        private readonly object _endpointsLock = new object();

        public long ConfigLastUpdated { get; set; } = 0;
        public int HeartbeatIntervalInMS { get; private set; } = 0;

        /// <summary>
        /// Clears all configuration data.
        /// </summary>
        public void Clear()
        {
            _blockedUsers.Clear();
            _endpoints.Clear();
            _blockedUserAgents = null;
            _blockList = new BlockList();
            HeartbeatIntervalInMS = 0;
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

            HeartbeatIntervalInMS = response.HeartbeatIntervalInMS;
            Heartbeat.UpdateDefaultInterval(response.HeartbeatIntervalInMS);
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
