using Aikido.Zen.Core.Models;
using System;

namespace Aikido.Zen.Core.Helpers
{
	public class AgentInfoHelper
	{
		public static AgentInfo GetInfo()
		{
			return new AgentInfo
			{
				Hostname = Environment.MachineName,
				Os = new Os
				{
					Version = Environment.OSVersion.VersionString,
					Name = Environment.OSVersion.Platform.ToString()
				},
				Platform = new Platform
				{
					// version
					Version = Environment.Version.ToString(),
					// core or framework
					Arch = Environment.Version.Major >= 5 ? "core" : "framework"
				},
				IpAddress = IPHelper.Server,
				DryMode = Environment.GetEnvironmentVariable("AIKIDO_BLOCKING") == "true",
				// check if lambda or azure function
				Serverless = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") != null || Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") != null,
				Library = "firewall-dotnet",
				Version = "1.0.0"
			};
		}
	}
}
