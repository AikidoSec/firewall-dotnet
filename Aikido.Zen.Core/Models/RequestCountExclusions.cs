using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    public class RequestCountExclusions
    {
        public HashSet<string> Exact { get; set; }
        public string[] Suffix { get; set; }
    }
}
