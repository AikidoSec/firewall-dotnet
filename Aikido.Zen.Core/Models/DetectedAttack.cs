namespace Aikido.Zen.Core.Models {
    public class DetectedAttack
    {
        public string Type { get; set; } = "detected_attack";
        public RequestInfo Request { get; set; }
        public Attack Attack { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time { get; set; }
    }
}
