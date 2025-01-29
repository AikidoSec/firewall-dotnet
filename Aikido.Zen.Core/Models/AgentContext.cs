using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;
using Aikido.Zen.Core.Models.Ip;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Aikido.Zen.Core.Models
{
    public class AgentContext
    {
        private IDictionary<string, Host> _hostnames = new Dictionary<string, Host>();
        private IDictionary<string, Route> _routes = new Dictionary<string, Route>();
        private IDictionary<string, UserExtended> _users = new Dictionary<string, UserExtended>();
        private IDictionary<string, RateLimitingConfig> _rateLimitedRoutes = new Dictionary<string, RateLimitingConfig>();
        private Regex _blockedUserAgents;

        private BlockList _blockList = new BlockList();
        private HashSet<string> _blockedUsers = new HashSet<string>();

        private int _requests = 0;
        private int _attacksDetected = 0;
        private int _attacksBlocked = 0;
        private int _requestsAborted = 0;
        private long _started = DateTimeHelper.UTCNowUnixMilliseconds();
        public long ConfigLastUpdated { get; set; } = 0;
        public bool ContextMiddlewareInstalled { get; set; } = false;
        public bool BlockingMiddlewareInstalled { get; set; } = false;


        public void AddRequest()
        {
            _requests++;
        }

        public void AddAbortedRequest()
        {
            _requestsAborted++;
        }

        public void AddAttackDetected()
        {
            _attacksDetected++;
        }

        public void AddAttackBlocked()
        {
            _attacksBlocked++;
        }

        public void AddRateLimitedEndpoint(string path, RateLimitingConfig config)
        {
            if (string.IsNullOrWhiteSpace(path) || config == null)
                return;
            _rateLimitedRoutes[path] = config;
        }

        public void AddHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return;
            var hostParts = hostname.Split(':');
            var name = hostParts[0];
            var port = hostParts.Length > 1 ? hostParts[1] : "80";
            var host = new Host { Hostname = name };
            if (int.TryParse(port, out int portNumber))
                host.Port = portNumber;

            var key = $"{name}:{port}";
            _hostnames[key] = host;
        }

        public void AddUser(User user, string ipAddress)
        {
            if (user == null)
                return;
            if (!_users.TryGetValue(user.Id, out UserExtended userExtended))
            {
                userExtended = new UserExtended(user.Id, user.Name)
                {
                    FirstSeenAt = DateTimeHelper.UTCNowUnixMilliseconds(),
                };
                _users.Add(user.Id, userExtended);
            }
            userExtended.LastIpAddress = ipAddress;
            userExtended.LastSeenAt = DateTimeHelper.UTCNowUnixMilliseconds();
        }

        public void AddRoute(Context context)
        {
            if (context == null || context.Url == null) return;
            _routes.TryGetValue(context.Url, out Route route);
            if (route == null)
            {
                route = new Route
                {
                    Path = context.Url,
                    Method = context.Method,
                    ApiSpec = OpenAPIHelper.GetApiInfo(context)
                };
                _routes.Add(route.Path, route);
            }
            else
            {
                OpenAPIHelper.UpdateApiInfo(context, route, EnvironmentHelper.MaxApiDiscoverySamples);
            }
            route.Hits++;
        }

        public void Clear()
        {
            _hostnames.Clear();
            _users.Clear();
            _routes.Clear();
            _requests = 0;
            _attacksDetected = 0;
            _attacksBlocked = 0;
            _requestsAborted = 0;
            _started = DateTimeHelper.UTCNowUnixMilliseconds();
            _blockedUsers.Clear();
        }

        public bool IsBlocked(User user, string ip, string endpoint, string userAgent)
        {
            return (user != null && IsUserBlocked(user.Id)) || _blockList.IsBlocked(ip, endpoint) || IsUserAgentBlocked(userAgent);
        }

        public bool IsUserBlocked(string userId)
        {
            return _blockedUsers.Contains(userId);
        }

        public bool IsUserAgentBlocked(string userAgent)
        {
            return _blockedUserAgents?.IsMatch(userAgent) ?? false;
        }

        public void UpdateBlockedUsers(IEnumerable<string> users)
        {
            _blockedUsers.Clear();
            _blockedUsers.UnionWith(users);
        }

        public void UpdateRatelimitedRoutes(IEnumerable<EndpointConfig> endpoints)
        {
            _rateLimitedRoutes.Clear();
            foreach (var endpoint in endpoints)
            {
                if (endpoint.GraphQL)
                {
                    continue;
                }
                // remove the leading slash from the route pattern, to ensure we don't distinguish for example between api/users and /api/users
                _rateLimitedRoutes[$"{endpoint.Method}|{endpoint.Route.TrimStart('/')}"] = endpoint.RateLimiting;
            }
        }

        public void UpdateConfig(bool block, IEnumerable<string> blockedUsers, IEnumerable<EndpointConfig> endpoints, Regex blockedUserAgents, long configVersion)
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", block ? "true" : "false");
            UpdateBlockedUsers(blockedUsers);
            BlockList.UpdateAllowedSubnets(endpoints);
            UpdateRatelimitedRoutes(endpoints);
            UpdateBlockedUserAgents(blockedUserAgents);
            ConfigLastUpdated = configVersion;
        }

        public void UpdateBlockedIps(IEnumerable<string> blockedIPs)
        {
            if (blockedIPs == null)
            {
                BlockList.UpdateBlockedSubnets(new List<string>());
                return;
            }
            BlockList.UpdateBlockedSubnets(blockedIPs);
        }

        public void UpdateBlockedUserAgents(Regex blockedUserAgents)
        {
            _blockedUserAgents = blockedUserAgents;
        }

        public IEnumerable<Host> Hostnames => _hostnames.Select(x => x.Value);
        public IEnumerable<UserExtended> Users => _users.Select(x => x.Value);
        public IEnumerable<Route> Routes => _routes.Select(x => x.Value);
        public IDictionary<string, RateLimitingConfig> RateLimitedRoutes => _rateLimitedRoutes;
        public int Requests => _requests;
        public int RequestsAborted => _requestsAborted;
        public int AttacksDetected => _attacksDetected;
        public int AttacksBlocked => _attacksBlocked;
        public long Started => _started;
        public BlockList BlockList => _blockList;
        public Regex BlockedUserAgents => _blockedUserAgents;
    }
}
