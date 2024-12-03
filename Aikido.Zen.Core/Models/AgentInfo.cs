using System.Collections.Generic;

namespace Aikido.Zen.Core.Models {
    public class AgentInfo
    {
        public bool DryMode { get; set; }
        public string Hostname { get; set; }
        public string Version { get; set; }
        public string Library { get; set; }
        public Dictionary<string, string> Packages { get; set; }
        public string IpAddress { get; set; }
        public bool PreventedPrototypePollution { get; set; }
        public IncompatiblePackages IncompatiblePackages { get; set; }
        public Os Os { get; set; }
        public Platform Platform { get; set; }
        public string NodeEnv { get; set; }
        public bool Serverless { get; set; }
        public List<string> Stack { get; set; }
    }
}