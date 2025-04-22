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
    /// This class is thread-safe and can handle concurrent access to its collections.
    /// </summary>
    public class AgentContext
    {
        private readonly ConcurrentDictionary<string, Host> _hostnames = new ConcurrentDictionary<string, Host>();
        private readonly ConcurrentDictionary<string, Route> _routes = new ConcurrentDictionary<string, Route>();
        private readonly ConcurrentDictionary<string, UserExtended> _users = new ConcurrentDictionary<string, UserExtended>();
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
            // thread safe add or update
            _hostnames.AddOrUpdate(
                // the dictionary key is the hostname
                key: key,
                // on add, we set the host as the value
                (_) => new Host { Hostname = name, Port = portNumber },
                // on update, we set the host as the value
                (_, h) =>
                {
                    h.Increment();
                    return h;
                }
            );
        }

        public void AddUser(User user, string ipAddress)
        {
            if (user == null)
                return;
            _users.AddOrUpdate(
                // the dictionary key is the user id
                key: user.Id,
                // on add, we create a new user extended object
                (id) => new UserExtended(id, user.Name)
                {
                    FirstSeenAt = DateTimeHelper.UTCNowUnixMilliseconds(),
                    LastIpAddress = ipAddress,
                    LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds()
                },
                // on update, we update the last ip address and last seen at
                (_, existing) =>
                {
                    existing.LastIpAddress = ipAddress;
                    existing.LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds();
                    return existing;
                }
            );
        }

        public void AddRoute(Context context)
        {
            if (context == null || context.Route == null) return;
            // thread safe add or update
            _routes.AddOrUpdate(
                // the dictionary key is the route url
                key: context.Route,
                // on add, we create a new route object
                (route) => new Route
                {
                    Path = route,
                    Method = context.Method,
                    ApiSpec = OpenAPIHelper.GetApiInfo(context),
                },
                // on update, we update the api info and increment the hits
                (_, existing) =>
                {
                    OpenAPIHelper.UpdateApiInfo(context, existing, EnvironmentHelper.MaxApiDiscoverySamples);
                    existing.Increment();
                    return existing;
                }
            );
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
            // if the ip is bypassed, we don't block the request
            if (BlockList.IsIPBypassed(context.RemoteAddress))
            {
                return true;
            }
            if (context.User != null && IsUserBlocked(context.User.Id))
            {
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
            foreach (var user in users)
            {
                _blockedUsers.TryAdd(user, user);
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

        public IEnumerable<Host> Hostnames => _hostnames.Values;
        public IEnumerable<UserExtended> Users => _users.Values;
        public IEnumerable<Route> Routes => _routes.Values;
        public IEnumerable<EndpointConfig> Endpoints
        {
            get
            {
                lock (_endpointsLock)
                {
                    return _endpoints.ToList(); // Return a copy to avoid thread safety issues
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
