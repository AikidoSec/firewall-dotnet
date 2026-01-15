using System;
using System.Collections.Generic;
using System.IO;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core
{
    public class Context
    {
        public string Path { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public IDictionary<string, string> Query { get; set; } = new Dictionary<string, string>();
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> RouteParams { get; set; } = new Dictionary<string, string>();
        public string RemoteAddress { get; set; } = string.Empty;
        public Stream Body { get; set; }
        public IDictionary<string, string> Cookies { get; set; } = new Dictionary<string, string>();
        public bool AttackDetected { get; set; }
        public User User { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string[] Graphql { get; set; }
        public object Xml { get; set; }
        public string[] Subdomains { get; set; } = Array.Empty<string>();
        public Dictionary<string, HashSet<string>> Cache { get; set; } = new Dictionary<string, HashSet<string>>();
        public List<RedirectInfo> OutgoingRequestRedirects { get; set; } = new List<RedirectInfo>();
        public IDictionary<string, string> ParsedUserInput { get; set; } = new Dictionary<string, string>();
        public string UserAgent { get; set; } = string.Empty;
        public bool IsGraphQL => Graphql != null && Graphql.Length > 0;
        public object ParsedBody { get; set; }

        internal bool ContextMiddlewareInstalled { get; set; }
        internal bool BlockingMiddlewareInstalled { get; set; }

        public bool ConsumedRateLimitForIP { get; set; }
        public bool ConsumedRateLimitForUser { get; set; }

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
}
