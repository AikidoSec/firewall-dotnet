using System.Collections.Generic;

namespace Aikido.Zen.Core.Models {
    public class Attack
    {
        public Kind Kind { get; set; }
        public string Operation { get; set; }
        public string Module { get; set; }
        public bool Blocked { get; set; }
        public Source Source { get; set; }
        public string Path { get; set; }
        public string Stack { get; set; }
        public string Payload { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public User User { get; set; }
    }
}
