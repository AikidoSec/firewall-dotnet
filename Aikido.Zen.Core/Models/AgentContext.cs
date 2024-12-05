using System;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Models
{
	public class AgentContext
	{
        private IDictionary<string, Host> _hostnames { get; set; } = new Dictionary<string, Host>();
        private IDictionary<string, Route> _routes { get; set; } = new Dictionary<string, Route>();
        private IDictionary<string, UserExtended> _users { get; set; } = new Dictionary<string, UserExtended>();

        private int _requests = 0;
        private int _attacksDetected = 0;
        private int _attacksBlocked = 0;
        private int _requestsAborted = 0;
        private long _started = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();


        public void AddRequest() {
            _requests++;
        }

        public void AddAbortedRequest() {
            _requestsAborted++;
        }

        public void AddAttackDetected() {
            _attacksDetected++;
        }

        public void AddAttackBlocked() {
            _attacksBlocked++;
        }

        public void AddHostname(string hostname) {
            var hostParts = hostname.Split(':');
            var name = hostParts[0];
            var port = hostParts.Length > 1 ? hostParts[1] : "80";
            var host = new Host { Hostname = name };
            if (int.TryParse(port, out int portNumber))
                host.Port = portNumber;
            
            var key = $"{name}:{port}";
            _hostnames[key] = host;
        }

        public void AddUser(User user, string ipAddress) {
            _users.TryGetValue(user.Id, out UserExtended userExtended);
            if (userExtended == null) {
                userExtended = new UserExtended
                {
                    FirstSeenAt = System.DateTime.UtcNow.Ticks,
                    Name = user.Name,
                    Id = user.Id
                };
                _users.Add(user.Id, userExtended);
            }
            userExtended.LastIpAddress = ipAddress;
            userExtended.LastSeenAt = System.DateTime.UtcNow.Ticks;
        }

        public void AddRoute(string path, string method) {
            _routes.TryGetValue(path, out Route route);
            if (route == null) {
                route = new Route
                {
                    Path = path,
                    Method = method,
                };
                _routes.Add(route.Path, route);
            }
            route.Hits++;
        }

        public void Clear() {
            _hostnames.Clear();
            _users.Clear();
            _routes.Clear();
            _requests = 0;
            _attacksDetected = 0;
            _attacksBlocked = 0;
            _requestsAborted = 0;
            _started = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public IEnumerable<Host> Hostnames => _hostnames.Select(x => x.Value);
        public IEnumerable<UserExtended> Users => _users.Select(x => x.Value);
        public IEnumerable<Route> Routes => _routes.Select(x => x.Value);
        public int Requests => _requests;
        public int RequestsAborted => _requestsAborted;
        public int AttacksDetected => _attacksDetected;
        public int AttacksBlocked => _attacksBlocked;
        public long Started => _started;
	}
}
