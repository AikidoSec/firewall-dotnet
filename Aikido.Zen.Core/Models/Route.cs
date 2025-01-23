namespace Aikido.Zen.Core.Models
{
    public class Route
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public int Hits { get; set; }
        public APISpec ApiSpec { get; set; }
    }
}
