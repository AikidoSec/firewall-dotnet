using System;
using System.Net.Http;
using System.Security.Cryptography;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Adds the common agent identification headers to every request sent to Aikido.
    /// </summary>
    internal static class AgentRequestHeaders
    {
        private static readonly string SessionId = NewUuidV7();

        internal static void AddTo(HttpRequestMessage request)
        {
            var agentInfo = AgentInfoHelper.GetInfo();
            var hostname = string.IsNullOrEmpty(agentInfo.Hostname) ? "unknown" : agentInfo.Hostname;
            var ipAddress = string.IsNullOrEmpty(agentInfo.IpAddress) ? "unknown" : agentInfo.IpAddress;

            request.Headers.Add("X-Agent-Platform", "dotnet");
            request.Headers.Add("X-Agent-Library", agentInfo.Library);
            request.Headers.Add("X-Agent-Version", AgentInfoHelper.ZenVersion);
            try
            {
                request.Headers.Add("X-Agent-Hostname", hostname);
            }
            catch (FormatException)
            {
                // Headers.Add throws on values with line breaks.
                request.Headers.Add("X-Agent-Hostname", "unknown");
            }
            request.Headers.Add("X-Agent-IP-Address", ipAddress);
            request.Headers.Add("X-Agent-Session-Id", SessionId);
        }

        // netstandard2.0 has no Guid.CreateVersion7.
        internal static string NewUuidV7()
        {
            var bytes = new byte[16];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            var milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (var i = 0; i < 6; i++)
            {
                bytes[i] = (byte)(milliseconds >> (8 * (5 - i)));
            }

            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);

            var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return $"{hex.Substring(0, 8)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20)}";
        }
    }
}
