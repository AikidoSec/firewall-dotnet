using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace Aikido.Zen.Core.Models
{
    public class AgentInfo
    {
        [JsonPropertyName("dryMode")]
        public bool DryMode { get; set; }
        [JsonPropertyName("hostname")]
        public string Hostname { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("library")]
        public string Library { get; set; } = "firewall-dotnet";
        [JsonPropertyName("packages")]
        public Dictionary<string, string> Packages { get; set; } = new Dictionary<string, string>();
        [JsonPropertyName("ipAddress")]
        public string IpAddress { get; set; }
        [JsonPropertyName("os")]
        public Os Os { get; set; } = new Os();
        [JsonPropertyName("platform")]
        public Platform Platform { get; set; } = new Platform();
        [JsonPropertyName("serverless")]
        public bool Serverless { get; set; } = false;
        [JsonPropertyName("stack")]
        public List<string> Stack { get; set; } = new List<string>();

        // these properties are not needed for the .net firewall, so we might be able to remove this in the near future.
        // For now, the api expects these fields
        [JsonPropertyName("preventedPrototypePollution")]
        public bool PreventedPrototypePollution => false;
        [JsonPropertyName("incompatiblePackages")]
        public IDictionary<string, string> IncompatiblePackages = new Dictionary<string, string> {
            { "incompatiblePackages", null }
        };
        [JsonPropertyName("nodeEnv")]
        public string NodeEnv => "";
    }
}
