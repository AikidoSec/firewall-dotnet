namespace Aikido.Zen.Server.Mock.Models;

public class RateLimitingConfig
{
    public bool Enabled { get; set; }
    public int MaxRequests { get; set; }
    public int WindowSizeInMS { get; set; }
}