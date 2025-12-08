namespace Aikido.Zen.Core.Models
{

    public class RateLimitingConfig
    {
        public bool Enabled { get; set; } = false;
        public int MaxRequests { get; set; } = 0;
        public int WindowSizeInMS { get; set; } = 0;
    }
}
