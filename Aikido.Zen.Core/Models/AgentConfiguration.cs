using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
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
        private Regex _monitoredUserAgents;
        private BlockList _blockList = new BlockList();
        private List<EndpointConfig> _endpoints = new List<EndpointConfig>();
        private List<(string Key, Regex Pattern)> _userAgentDetails = new List<(string Key, Regex Pattern)>();
        private List<(string Key, IPRange List)> _monitoredIPAddresses = new List<(string Key, IPRange List)>();
        private readonly object _endpointsLock = new object();
        private readonly object _monitoringLock = new object();

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
            _monitoredUserAgents = null;
            _userAgentDetails = new List<(string Key, Regex Pattern)>();
            _monitoredIPAddresses = new List<(string Key, IPRange List)>();
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
        /// Checks whether a user agent matches monitored user-agent patterns.
        /// </summary>
        /// <param name="userAgent">The user-agent string to check.</param>
        /// <returns>True if the user-agent is monitored, false otherwise.</returns>
        public bool IsMonitoredUserAgent(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return false;

            return _monitoredUserAgents?.IsMatch(userAgent) ?? false;
        }

        /// <summary>
        /// Gets all user-agent detail keys matching a specific user-agent value.
        /// </summary>
        /// <param name="userAgent">The user-agent string to check.</param>
        /// <returns>A list of matching keys.</returns>
        public IEnumerable<string> GetMatchingUserAgentKeys(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return Enumerable.Empty<string>();
            }

            List<(string Key, Regex Pattern)> detailsSnapshot;
            lock (_monitoringLock)
            {
                detailsSnapshot = _userAgentDetails.ToList();
            }

            return detailsSnapshot.Where(detail => detail.Pattern.IsMatch(userAgent))
                                  .Select(detail => detail.Key)
                                  .ToList();
        }

        /// <summary>
        /// Gets all monitored IP list keys matching the provided IP.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>A list of matching monitored IP keys.</returns>
        public IEnumerable<string> GetMatchingMonitoredIPListKeys(string ip)
        {
            if (!IPHelper.IsValidIp(ip))
            {
                return Enumerable.Empty<string>();
            }

            List<(string Key, IPRange List)> monitoredSnapshot;
            lock (_monitoringLock)
            {
                monitoredSnapshot = _monitoredIPAddresses.ToList();
            }

            return monitoredSnapshot.Where(list => list.List.HasItems && list.List.IsIpInRange(ip))
                                    .Select(list => list.Key)
                                    .ToList();
        }

        /// <summary>
        /// Gets all blocked IP list keys matching the provided IP.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>A list of matching blocked IP keys.</returns>
        public IEnumerable<string> GetMatchingBlockedIPListKeys(string ip)
        {
            return BlockList.GetMatchingBlockedIPListKeys(ip);
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
                ClearFirewallLists();
                return;
            }
            UpdateBlockedIps(response.BlockedIPAddresses);
            UpdateAllowedIps(response.AllowedIPAddresses);
            UpdateBlockedUserAgents(response.BlockedUserAgents);
            UpdateMonitoredUserAgents(response.MonitoredUserAgents);
            UpdateMonitoredIPAddresses(response.MonitoredIPAddresses);
            UpdateUserAgentDetails(response.UserAgentDetails);
        }

        private void ClearFirewallLists()
        {
            UpdateBlockedIps(new List<FirewallListsAPIResponse.IPList>());
            UpdateAllowedIps(new List<FirewallListsAPIResponse.IPList>());
            UpdateBlockedUserAgents(null);
            UpdateMonitoredUserAgents(null);
            UpdateMonitoredIPAddresses(new List<FirewallListsAPIResponse.IPList>());
            UpdateUserAgentDetails(new List<FirewallListsAPIResponse.UserAgentDetail>());
        }

        /// <summary>
        /// Updates the blocked user agents regex pattern from a regex string.
        /// </summary>
        /// <param name="blockedUserAgents">The regex pattern string for blocked user agents.</param>
        public void UpdateBlockedUserAgents(string blockedUserAgents)
        {
            if (string.IsNullOrWhiteSpace(blockedUserAgents))
            {
                _blockedUserAgents = null;
                return;
            }

            try
            {
                _blockedUserAgents = new Regex(blockedUserAgents, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                // Ignore invalid regex patterns from API input and treat as no pattern configured.
                _blockedUserAgents = null;
            }
        }

        /// <summary>
        /// Updates the monitored user agents regex pattern from a regex string.
        /// </summary>
        /// <param name="monitoredUserAgents">The regex pattern string for monitored user agents.</param>
        public void UpdateMonitoredUserAgents(string monitoredUserAgents)
        {
            if (string.IsNullOrWhiteSpace(monitoredUserAgents))
            {
                _monitoredUserAgents = null;
                return;
            }

            try
            {
                _monitoredUserAgents = new Regex(monitoredUserAgents, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                // Ignore invalid regex patterns from API input and treat as no pattern configured.
                _monitoredUserAgents = null;
            }
        }

        /// <summary>
        /// Updates keyed user-agent patterns used for stats attribution.
        /// Invalid regex patterns are ignored.
        /// </summary>
        /// <param name="userAgentDetails">The keyed user-agent pattern list.</param>
        public void UpdateUserAgentDetails(IEnumerable<FirewallListsAPIResponse.UserAgentDetail> userAgentDetails)
        {
            var details = new List<(string Key, Regex Pattern)>();

            if (userAgentDetails != null)
            {
                foreach (var detail in userAgentDetails)
                {
                    if (detail == null || string.IsNullOrWhiteSpace(detail.Key) || string.IsNullOrWhiteSpace(detail.Pattern))
                    {
                        continue;
                    }

                    try
                    {
                        details.Add((detail.Key, new Regex(detail.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase)));
                    }
                    catch (ArgumentException)
                    {
                        // Invalid regex from API should not crash config updates.
                    }
                }
            }

            lock (_monitoringLock)
            {
                _userAgentDetails = details;
            }
        }

        /// <summary>
        /// Updates monitored IP lists used for keyed monitoring stats.
        /// </summary>
        /// <param name="monitoredIPAddresses">The monitored IP lists from the API.</param>
        public void UpdateMonitoredIPAddresses(IEnumerable<FirewallListsAPIResponse.IPList> monitoredIPAddresses)
        {
            var monitored = new List<(string Key, IPRange List)>();

            if (monitoredIPAddresses != null)
            {
                foreach (var list in monitoredIPAddresses)
                {
                    if (list == null || string.IsNullOrWhiteSpace(list.Key))
                    {
                        continue;
                    }

                    var range = new IPRange();
                    var ips = list.Ips ?? Enumerable.Empty<string>();
                    foreach (var ip in ips)
                    {
                        foreach (var cidr in IPHelper.ToCidrString(ip))
                        {
                            range.InsertRange(cidr);
                        }
                    }

                    monitored.Add((list.Key, range));
                }
            }

            lock (_monitoringLock)
            {
                _monitoredIPAddresses = monitored;
            }
        }

        private void UpdateBlockedIps(IEnumerable<FirewallListsAPIResponse.IPList> blockedIPAddresses)
        {
            BlockList.UpdateBlockedIps((blockedIPAddresses ?? Enumerable.Empty<FirewallListsAPIResponse.IPList>())
                .Where(list => list != null)
                .Select(list => (list.Key, list.Ips ?? Enumerable.Empty<string>())));
        }

        private void UpdateAllowedIps(IEnumerable<FirewallListsAPIResponse.IPList> allowedIPAddresses)
        {
            BlockList.UpdateAllowedIps((allowedIPAddresses ?? Enumerable.Empty<FirewallListsAPIResponse.IPList>())
                .Where(list => list != null)
                .SelectMany(list => list.Ips ?? Enumerable.Empty<string>()));
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
