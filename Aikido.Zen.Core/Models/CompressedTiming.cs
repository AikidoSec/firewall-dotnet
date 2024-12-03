namespace Aikido.Zen.Core.Models
{
    public class CompressedTiming
    {
        public double AverageInMS { get; set; }
        public Dictionary<string, double> Percentiles { get; set; }
        public int CompressedAt { get; set; }
    }
}