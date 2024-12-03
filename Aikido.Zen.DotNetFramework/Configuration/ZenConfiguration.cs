
using Aikido.Zen.Core;
using System.Configuration;

namespace Aikido.Zen.DotNetFramework.Configuration
{
	public class ZenConfiguration
	{
		public static AikidoZenConfig Config  {
			get { return ConfigurationManager.GetSection("AikidoZen") as AikidoZenConfig; }
		}
	}
}
