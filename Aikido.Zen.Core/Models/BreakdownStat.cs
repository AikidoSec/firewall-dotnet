using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    public class BreakdownStat
    {
        public IDictionary<string, int> Breakdown { get; set; }

        public BreakdownStat(IDictionary<string, int> breakdown)
        {
            Breakdown = breakdown;
        }

        public BreakdownStat()
        {
            Breakdown = new Dictionary<string, int>();
        }
    }
}

