using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{

    public class EndpointConfig
    {
        public string Method { get; set; }
        public string Route { get; set; }
        public bool ForceProtectionOff { get; set; }
        // The GraphQL property is deprecated and should be removed in the future
        [System.Obsolete("This property is deprecated and should be removed in the future")]
        public bool GraphQL { get; set; }
        public IEnumerable<string> AllowedIPAddresses { get; set; }
        public RateLimitingConfig RateLimiting { get; set; }
    }
}
