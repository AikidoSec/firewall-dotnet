using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events {
    public class DetectedAttack : IEvent
    {
        public string Type => "detected_attack";
        public RequestInfo Request { get; set; }
        public Attack Attack { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();
    }
}
