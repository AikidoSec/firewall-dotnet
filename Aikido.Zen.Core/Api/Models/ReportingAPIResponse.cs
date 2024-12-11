using Aikido.Zen.Core.Models;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Api
{
    public class ReportingAPIResponse : APIResponse
    {
        public long ConfigUpdatedAt { get; set; }
        public int HeartbeatIntervalInMS { get; set; }
        public IEnumerable<EndpointConfig> Endpoints { get; set; }
        public IEnumerable<string> BlockedUserIds { get; set; }
        public IEnumerable<string> AllowedIPAddresses { get; set; }
        public bool ReceivedAnyStats { get; set; }
    }
}
