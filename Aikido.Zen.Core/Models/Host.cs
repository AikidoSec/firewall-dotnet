namespace Aikido.Zen.Core.Models
{
    public class Host : HitCount
    {
        public string Hostname { get; set; }
        public int? Port { get; set; }
    }
}
