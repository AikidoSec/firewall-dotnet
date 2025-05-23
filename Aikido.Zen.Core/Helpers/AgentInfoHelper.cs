using Aikido.Zen.Core.Models;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
[assembly: InternalsVisibleTo("Aikido.Zen.DotNetCore")]
[assembly: InternalsVisibleTo("Aikido.Zen.DotNetFramework")]
namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for retrieving agent information about the current runtime environment.
    /// </summary>
    internal class AgentInfoHelper
    {
        public static string ZenVersion => CleanVersion(typeof(AgentInfoHelper).Assembly.GetName().Version.ToString());
        // Cache the AgentInfo object to avoid repeated creation
        private static readonly AgentInfo _cachedAgentInfo = new AgentInfo
        {
            Hostname = Environment.MachineName,
            Os = new Os
            {
                Version = Environment.OSVersion.VersionString,
                Name = Environment.OSVersion.Platform.ToString()
            },
            Platform = new Platform
            {
                Version = Environment.Version.ToString(),
                Arch = RuntimeInformation.ProcessArchitecture.ToString()
            },
            IpAddress = IPHelper.Server,
            Library = "firewall-dotnet",
            Version = ZenVersion,
            // Determine if running in a serverless environment
            Serverless = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") != null || Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") != null
        };

        /// <summary>
        /// Gets information about the current runtime environment including OS, platform, and configuration details.
        /// </summary>
        /// <returns>An AgentInfo object containing the environment information.</returns>
        public static AgentInfo GetInfo()
        {
            // Update only the fields that can change
            _cachedAgentInfo.DryMode = EnvironmentHelper.DryMode;
            _cachedAgentInfo.Serverless = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") != null || Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") != null;
            _cachedAgentInfo.Version = ZenVersion;
            return _cachedAgentInfo;
        }

        internal static string CleanVersion(string version)
        {
            // remove the build number
            version = version.Split('+')[0];
            // remove 4th version number, not used by nuget
            version = string.Join(".", version.Split('.').Take(3));
            // remove the prerelease version
            version = version.Split('-')[0];
            return version;
        }

        internal static void SetVersion(string version)
        {
            _cachedAgentInfo.Version = CleanVersion(version);
        }
    }
}
