using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Configuration;
using Aikido.Zen.Core.Patches;

namespace Aikido.Zen.DotNetFramework
{
    public class Zen
    {
        // we need to reference Harmony somewhere to ensure it is copied with our package
        private static HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("reference");
        public static void Start()
        {
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return;
            }
            // patch the sinks
            Patcher.Patch();
            // setup the agent
            if (Agent.Instance == null)
            {
                var baseUrl = Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev";
                var uri = new Uri(baseUrl);
                var apiClient = new ReportingAPIClient(uri);
                var zenApi = new ZenApi(apiClient);
                Agent.GetInstance(zenApi);
            }
            Agent.Instance.Start(AikidoConfiguration.Options.AikidoToken);
        }
    }
}
