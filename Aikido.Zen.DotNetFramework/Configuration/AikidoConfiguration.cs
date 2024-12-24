
using Aikido.Zen.Core;
using System;
using System.Configuration;

namespace Aikido.Zen.DotNetFramework.Configuration
{
	public class AikidoConfiguration
	{
		public static AikidoOptions Options  {
			get {
                Init();
                return
                new AikidoOptions {
                    AikidoToken = ConfigurationManager.AppSettings["Aikido:AikidoToken"]
                        ?? Environment.GetEnvironmentVariable("AIKIDO_TOKEN"),
                    AikidoUrl = ConfigurationManager.AppSettings["Aikido:AikidoUrl"]
                        ?? Environment.GetEnvironmentVariable("AIKIDO_URL")
                };
            }
		}

        internal static void Init() {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_TOKEN")))
            {
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", ConfigurationManager.AppSettings["Aikido:AikidoToken"]);
            }
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_URL")))
            {
                Environment.SetEnvironmentVariable("AIKIDO_URL", ConfigurationManager.AppSettings["Aikido:AikidoUrl"]);
            }
        }
	}
}
