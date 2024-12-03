using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Configuration;

namespace Aikido.Zen.DotNetFramework
{
    public class Firewall
    {
    private static Agent _agent;
        public static Agent Agent {
            get {
                if (_agent == null)
                {

                    _agent = new Agent(
                        new ZenApi(
                            new ReportingAPIClient(new Uri(Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev"))
                            )
                        );
                }
				return _agent;
			}
        }

        public static void Start() {
            // Send the started event
            Agent.Start(ZenConfiguration.Config.ZenToken);
        }
    }
}
