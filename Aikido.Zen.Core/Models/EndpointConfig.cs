using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{

    public class EndpointConfig
    {
        public string Method { get; set; }
        public string Route { get; set; }
        public bool ForceProtectionOff { get; set; }
        [JsonPropertyName("graphql")]
        public GraphQLConfig GraphQL { get; set; } = null;
        public IEnumerable<string> AllowedIPAddresses { get; set; }
        public RateLimitingConfig RateLimiting { get; set; }
    }
}
