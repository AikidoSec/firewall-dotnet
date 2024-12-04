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
        public bool PreventedPrototypePollution { get; set; }
        public IncompatiblePackages IncompatiblePackages { get; set; } = new IncompatiblePackages();
        public Os Os { get; set; } = new Os();
        public Platform Platform { get; set; } = new Platform();
        public string NodeEnv { get; set; } = "";
        public bool Serverless { get; set; } = false;
        public List<string> Stack { get; set; } = new List<string>();
    }
}
