using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    public class AiInfo
    {
        public string Provider { get; set; }
        public string Model { get; set; }
        public int Calls { get; set; }
        public AiTokens Tokens { get; set; }
        public IEnumerable<AiRoute> Routes { get; set; }
    }

    public class AiTokens
    {
        public long Input { get; set; }
        public long Output { get; set; }
        public long Total => Input + Output;
    }

    public class AiRoute : Route
    {
        public int Requests { get; set; }
        public int Calls { get; set; }
        public AiTokens Tokens { get; set; }
    }
}
