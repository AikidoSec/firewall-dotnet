using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{

    public class EndpointConfig
    {
        public string Method { get; set; }
        public string Route { get; set; }
        public bool ForceProtectionOff { get; set; }
        public string GraphQL { get; set; }
        public IEnumerable<string> AllowedIPAddresses { get; set; }
        public RateLimitingConfig RateLimiting { get; set; }
    }
}
