using System.Text.Json.Serialization;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events
{
    public class Started : IEvent
    {
        [JsonPropertyName("type")]
        public string Type => "started";
        [JsonPropertyName("agent")]
        public AgentInfo Agent { get; set; }
        [JsonPropertyName("time")]
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();

        public static Started Create()
        {
            return new Started
            {
                Agent = AgentInfoHelper.GetInfo()
            };
        }
    }
}
