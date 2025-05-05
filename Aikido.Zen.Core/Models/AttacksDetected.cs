namespace Aikido.Zen.Core.Models
{
    public class AttacksDetected
    {
        public int Total; // must be a field to be thread safe
        public int Blocked; // must be a field to be thread safe
    }
}
