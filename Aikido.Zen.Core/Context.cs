using System;
using System.Collections.Generic;

namespace Aikido.Zen.Core
{
	public class Context
	{
		public string Url { get; set; } = string.Empty;
		public string Method { get; set; } = string.Empty;
		public Dictionary<string, string[]> Query { get; set; } = new Dictionary<string, string[]>();
		public Dictionary<string, string[]> Headers { get; set; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, string> RouteParams { get; set; }
		public string RemoteAddress { get; set; } = string.Empty;
		public object Body { get; set; }
		public Dictionary<string, string> Cookies { get; set; } = new Dictionary<string, string>();
		public bool AttackDetected { get; set; }
		public bool ConsumedRateLimitForIP { get; set; }
		public bool ConsumedRateLimitForUser { get; set; }
		public User? User { get; set; }
		public string Source { get; set; } = string.Empty;
		public string Route { get; set; } = string.Empty;
		public string[] Graphql { get; set; }
		public object Xml { get; set; }
		public string[] Subdomains { get; set; } = Array.Empty<string>();
		public Dictionary<string, HashSet<string>> Cache { get; set; }
		public List<RedirectInfo> OutgoingRequestRedirects { get; set; }

	}

	public struct User
	{

        public User(string id = "", string name = "")
		{
			Id = id;
			Name = name;
		}
        public string Id { get; set; }
		public string Name { get; set; }
	}

	public struct RedirectInfo
	{

        public RedirectInfo(Uri src, Uri dest)
        {
            this.Source = src;
			this.Destination = dest;
        }

        public Uri Source { get; set; }
		public Uri Destination { get; set; }
	}
}