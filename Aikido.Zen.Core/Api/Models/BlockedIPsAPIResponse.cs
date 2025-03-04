using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Api
{
    public class FirewallListsAPIResponse : APIResponse
    {

        public FirewallListsAPIResponse ()
        {
            BlockedIPAddresses = new List<IPList>();
            AllowedIPAddresses = new List<IPList>();
            BlockedUserAgents = string.Empty;
        }
        public FirewallListsAPIResponse (IEnumerable<IPList> blockedIPAddresses = null, IEnumerable<IPList> allowedIPAddresses = null, string blockedUserAgents = null)
        {
            BlockedIPAddresses = blockedIPAddresses ?? new List<IPList>();
            AllowedIPAddresses = allowedIPAddresses ?? new List<IPList>();
            BlockedUserAgents = blockedUserAgents ?? string.Empty;

        }

        private IEnumerable<IPList> _blockedIpAddresses = new List<IPList>();
        private IEnumerable<IPList> _allowedIPAddresses = new List<IPList>();

        public IEnumerable<IPList> BlockedIPAddresses
        {
            get
            {
                return _blockedIpAddresses;
            }
            set
            {
                _blockedIpAddresses = value ?? new List<IPList>();
            }
        }

        public IEnumerable<IPList> AllowedIPAddresses
        {
            get
            {
                return _allowedIPAddresses;
            }
            set
            {
                _allowedIPAddresses = value ?? new List<IPList>();
            }
        }

        public string BlockedUserAgents { get; set; }

        public IEnumerable<string> BlockedIps => BlockedIPAddresses.Where(BlockedIPAddresses => BlockedIPAddresses != null)
                   .SelectMany(BlockedIPAddresses => BlockedIPAddresses.Ips ?? Enumerable.Empty<string>());

        public IEnumerable<string> AllowedIps => AllowedIPAddresses.Where(AllowedIPAddresses => AllowedIPAddresses != null)
                   .SelectMany(AllowedIPAddresses => AllowedIPAddresses.Ips ?? Enumerable.Empty<string>());

        public class IPList
        {
            public string Source { get; set; }
            public string Description { get; set; }
            public IEnumerable<string> Ips { get; set; }
        }
    }
}
