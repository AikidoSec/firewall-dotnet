using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Configuration;

namespace Aikido.Zen.DotNetFramework
{
    public class Zen
    {
    private static Agent _agent;
        public static Agent Agent {
            get {
                if (_agent == null)
                {
                    var baseUrl = Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev";
                    var uri = new Uri(baseUrl);
                    var apiClient = new ReportingAPIClient(uri);
                    var zenApi = new ZenApi(apiClient);
                    _agent = new Agent(zenApi);
                }
				return _agent;
			}
        }

        public static void Start() {
            // Send the started event
            Agent.Start(AikidoConfiguration.Options.AikidoToken);
        }
    }
}
