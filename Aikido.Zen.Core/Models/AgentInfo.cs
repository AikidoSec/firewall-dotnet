using System.Collections.Generic;

namespace Aikido.Zen.Core.Models {
    public class AgentInfo
    {
        public bool DryMode { get; set; }
        public string Hostname { get; set; }
        public string Version { get; set; }
        public string Library { get; set; } = "firewall-dotnet";
        public Dictionary<string, string> Packages { get; set; } = new Dictionary<string, string>();
        public string IpAddress { get; set; }
        public Os Os { get; set; } = new Os();
        public Platform Platform { get; set; } = new Platform();
        public bool Serverless { get; set; } = false;
        public List<string> Stack { get; set; } = new List<string>();

        // these properties are not needed for the .net firewall, so we might be able to remove this in the near future.
        // For now, the api expects these fields
        public bool PreventedPrototypePollution => false;

        public IDictionary<string, string> IncompatiblePackages = new Dictionary<string, string> {
            { "incompatiblePackages", null }
        };
        public string NodeEnv => "";
    }
}
