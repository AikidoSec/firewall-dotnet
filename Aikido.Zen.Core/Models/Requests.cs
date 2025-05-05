namespace Aikido.Zen.Core.Models
{
    public class Requests
    {
        public int Total; // must be a field to be thread safe
        public int Aborted; // must be a field to be thread safe
        public AttacksDetected AttacksDetected { get; set; }
    }
}
