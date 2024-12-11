
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Api
{
    public class BlockedIpsAPIResponse : APIResponse
    {
        public IEnumerable<BlockedIPAddressesList> BlockedIPAddresses { get; set; }

        public IEnumerable<string> Ips() => BlockedIPAddresses.SelectMany(ipList => ipList.Ips);

        public class BlockedIPAddressesList
        {
            public string Source { get; set; }
            public string Description { get; set; }
            public IEnumerable<string> Ips { get; set; }
        }

    }
}
