using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Api
{
    public class BlockedIpsAPIResponse : APIResponse
    {
        public BlockedIpsAPIResponse(IEnumerable<BlockedIPAddressesList> blockedIPAddresses)
        {
            BlockedIPAddresses = blockedIPAddresses ?? new List<BlockedIPAddressesList>();
        }

        public BlockedIpsAPIResponse()
        {
            BlockedIPAddresses = new List<BlockedIPAddressesList>();
        }

        private IEnumerable<BlockedIPAddressesList> _blockedIpAddresses = new List<BlockedIPAddressesList>();
        public IEnumerable<BlockedIPAddressesList> BlockedIPAddresses
        {
            get
            {
                return _blockedIpAddresses;
            }
            set
            {
                _blockedIpAddresses = value ?? new List<BlockedIPAddressesList>();
            }
        }

        public IEnumerable<string> Ips => BlockedIPAddresses.Where(BlockedIPAddresses => BlockedIPAddresses != null)
                   .SelectMany(BlockedIPAddresses => BlockedIPAddresses.Ips ?? Enumerable.Empty<string>());

        public class BlockedIPAddressesList
        {
            public string Source { get; set; }
            public string Description { get; set; }
            public IEnumerable<string> Ips { get; set; }
        }
    }
}
