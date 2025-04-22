using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;
using Aikido.Zen.Core.Models.Ip;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents the context for an agent, managing hostnames, routes, users, and blocking configurations.
    /// Uses ConcurrentLruDictionary for hostnames, routes, and users to enable LFU eviction.
    /// This class is thread-safe and can handle concurrent access to its collections.
    /// </summary>
    public class AgentContext
    {
        private const int MaxHostnames = 2000;
        private const int MaxUsers = 2000;
        private const int MaxRoutes = 5000;

        // Use ConcurrentLFUDictionary which handles LFU eviction internally
        private readonly ConcurrentLFUDictionary<string, Host> _hostnames = new ConcurrentLFUDictionary<string, Host>(MaxHostnames);
        private readonly ConcurrentLFUDictionary<string, Route> _routes = new ConcurrentLFUDictionary<string, Route>(MaxRoutes);
        private readonly ConcurrentLFUDictionary<string, UserExtended> _users = new ConcurrentLFUDictionary<string, UserExtended>(MaxUsers);

        // Blocked users and user agents remain as before
        private readonly ConcurrentDictionary<string, string> _blockedUsers = new ConcurrentDictionary<string, string>();
        private Regex _blockedUserAgents;
        private readonly BlockList _blockList = new BlockList();
        private List<EndpointConfig> _endpoints = new List<EndpointConfig>();
        private readonly object _endpointsLock = new object();

        private int _requests;
        private int _attacksDetected;
        private int _attacksBlocked;
        private int _requestsAborted;
        private long _started = DateTimeHelper.UTCNowUnixMilliseconds();
        public long ConfigLastUpdated { get; set; } = 0;
        public bool ContextMiddlewareInstalled { get; set; } = false;
        public bool BlockingMiddlewareInstalled { get; set; } = false;

        public void AddRequest()
        {
            // thread safe increment
            Interlocked.Increment(ref _requests);
        }

        public void AddAbortedRequest()
        {
            // thread safe increment
            Interlocked.Increment(ref _requestsAborted);
        }

        public void AddAttackDetected()
        {
            // thread safe increment
            Interlocked.Increment(ref _attacksDetected);
        }

        public void AddAttackBlocked()
        {
            // thread safe increment
            Interlocked.Increment(ref _attacksBlocked);
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

            if (_hostnames.TryGetValue(key, out var existingHost))
            {
                existingHost.Increment();
            }
            else
            {
                var newHost = new Host { Hostname = name, Port = portNumber };
                _hostnames.TryAdd(key, newHost);
            }
        }

        /// <summary>
        /// Adds or updates a user in the context, tracking their IP address and last seen time.
        /// Increments the user's hit count upon access (update).
        /// Handles LFU eviction if the maximum number of users is exceeded when adding a new user.
        /// </summary>
        /// <param name="user">The user object containing Id and Name.</param>
        /// <param name="ipAddress">The IP address associated with this user access.</param>
        public void AddUser(User user, string ipAddress)
        {
            if (user == null || string.IsNullOrEmpty(user.Id))
                return;

            if (_users.TryGetValue(user.Id, out var existingUser))
            {
                existingUser.Increment();
                existingUser.LastIpAddress = ipAddress;
                existingUser.LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds();
            }
            else
            {
                var newUser = new UserExtended(user.Id, user.Name)
                {
                    LastIpAddress = ipAddress,
                    LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds()
                };
                _users.TryAdd(user.Id, newUser);
            }
        }

        public void AddRoute(Context context)
        {
            if (context == null || context.Route == null) return;

            if (_routes.TryGetValue(context.Route, out var existingRoute))
            {
                existingRoute.Increment();
                OpenAPIHelper.UpdateApiInfo(context, existingRoute, EnvironmentHelper.MaxApiDiscoverySamples);
            }
            else
            {
                // Route doesn't exist, create and try to add it
                var newRoute = new Route
                {
                    Path = context.Route,
                    Method = context.Method,
                    ApiSpec = OpenAPIHelper.GetApiInfo(context),
                };
                _routes.TryAdd(context.Route, newRoute);
            }
        }

        public void Clear()
        {
            _hostnames.Clear();
            _users.Clear();
            _routes.Clear();
            // thread safe reset
            Interlocked.Exchange(ref _requests, 0);
            Interlocked.Exchange(ref _attacksDetected, 0);
            Interlocked.Exchange(ref _attacksBlocked, 0);
            Interlocked.Exchange(ref _requestsAborted, 0);
            _blockedUsers.Clear();
            // reset the started time
            _started = DateTimeHelper.UTCNowUnixMilliseconds();
        }

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
                // Note: We don't need _users.TryGetValue here just to check IsUserBlocked,
                // as IsUserBlocked checks the separate _blockedUsers dictionary.
                // If we needed the UserExtended object itself, we'd use TryGetValue.
                reason = "User is blocked";
                return true;
            }
            if (_blockList.IsBlocked(context, out reason))
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
            return _blockedUsers.ContainsKey(userId);
        }

        public bool IsUserAgentBlocked(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return false;

            return _blockedUserAgents?.IsMatch(userAgent) ?? false;
        }

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

        public void UpdateRatelimitedRoutes(IEnumerable<EndpointConfig> endpoints)
        {
            var newEndpoints = endpoints?.ToList() ?? new List<EndpointConfig>();
            lock (_endpointsLock)
            {
                _endpoints = newEndpoints;
            }
        }

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

        public void UpdateBlockedUserAgents(Regex blockedUserAgents)
        {
            _blockedUserAgents = blockedUserAgents;
        }

        // Use .Values to get the collection from the dictionary
        public IEnumerable<Host> Hostnames => _hostnames.Values;
        public IEnumerable<UserExtended> Users => _users.Values;
        public IEnumerable<Route> Routes => _routes.Values;

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
        public int Requests => _requests;
        public int RequestsAborted => _requestsAborted;
        public int AttacksDetected => _attacksDetected;
        public int AttacksBlocked => _attacksBlocked;
        public long Started => _started;
        public BlockList BlockList => _blockList;
        public Regex BlockedUserAgents => _blockedUserAgents;
    }
}
