using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Configuration;

namespace Aikido.Zen.DotNetFramework
{
    public class Zen
    {
        public static void Start() {
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
