namespace Aikido.Zen.Server.Mock.Models;

public class EndpointConfig
{
    public string Method { get; set; }
    public string Route { get; set; }
    public bool ForceProtectionOff { get; set; }
    public GraphQLConfig? GraphQL { get; set; } = null;
    public IEnumerable<string> AllowedIPAddresses { get; set; }
    public RateLimitingConfig RateLimiting { get; set; }
}

public class GraphQLConfig
{
    public string Name { get; set; }
    public string Type { get; set; }
}
