
using Aikido.Zen.Core;
using System;
using System.Configuration;

namespace Aikido.Zen.DotNetFramework.Configuration
{
	public class AikidoConfiguration
	{
		public static AikidoOptions Options  {
			get {
                return
                new AikidoOptions {
                    AikidoToken = ConfigurationManager.AppSettings["Aikido:AikidoToken"]
                        ?? Environment.GetEnvironmentVariable("AIKIDO_TOKEN")
                };
            }
		}
	}
}
