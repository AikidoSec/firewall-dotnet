using Aikido.Zen.Core;
using System.Configuration;
using System.Xml;
using System.Linq;
using System;


namespace Aikido.Zen.DotNetFramework.Configuration
{
	/// <summary>
	/// Configuration section handler for AikidoZen
	/// e.g. web.config
	/// <!-- define the configuration section handler -->
	/// <configSections>
	///   <section name ="aikidoZenConfig" type="Aikido.Zen.DotNetFramework.Configuration.AikidoZenConfigSectionHandler, Aikido.Zen.DotNetFramework" />
	/// </configSections>
	/// ...
	/// <!-- AikidoZen Configuration -->
	/// <AikidoZen>
	///		<zenToken>myToken</zenToken>
	/// </AikidoZen>
	/// </summary>
	public class AikidoZenConfigSectionHandler : IConfigurationSectionHandler
	{
		public object Create(object parent, object configContext, XmlNode section)
		{
			var config = new AikidoZenConfig();

			if (section != null)
			{
				XmlNode zenTokenNode = section.ChildNodes
					 .Cast<XmlNode?>()
					.FirstOrDefault(cn => cn != null && cn.Name.Equals("zenToken", StringComparison.OrdinalIgnoreCase));
				if (zenTokenNode != null && zenTokenNode.Attributes["value"] != null)
				{
					config.ZenToken = zenTokenNode.Attributes["value"].Value;
				}
			}

			return config;
		}
	}
}
