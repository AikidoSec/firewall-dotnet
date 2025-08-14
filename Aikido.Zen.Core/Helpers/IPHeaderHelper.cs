using System;
using System.Linq;

namespace Aikido.Zen.Core.Helpers
{
    public class IPHeaderHelper
    {
        public static string[] ParseIpHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return Array.Empty<string>();
            }

            return header
                .Split(',')
                .Select(ip => ip.Trim())
                .Where(ip => !string.IsNullOrEmpty(ip))
                .Select(ip =>
                {
                    // We do a first check here to make sure that valid IPv6 addresses don't
                    // get split on ":" below.
                    if (IPHelper.IsValidIp(ip))
                    {
                        return ip;
                    }

                    // According to RFC7239 the X-Forwarded-For header can contain port numbers.
                    // If the IP includes a port number, remove it.
                    if (ip.Contains(':'))
                    {
                        // Split the IP to remove the port
                        // IPv4: Split using :
                        // IPv6: Split using ]: because the IP address itself contains colons
                        var splitWith = ip.StartsWith("[") ? "]:" : ":";
                        var parts = ip.Split(
                            new[] { splitWith },
                            StringSplitOptions.RemoveEmptyEntries
                        );

                        if (parts.Length == 2)
                        {
                            ip = parts[0].Trim();
                            // Remove opening bracket for IPv6 with port
                            // the closing bracket will be removed by the split
                            if (parts[0].StartsWith("["))
                            {
                                return parts[0].Substring(1);
                            }
                            return parts[0];
                        }
                    }

                    // Normalize IPv6 by removing the brackets
                    if (ip.StartsWith("[") && ip.EndsWith("]"))
                    {
                        return ip.Substring(1, ip.Length - 2);
                    }

                    return ip;
                })
                .ToArray();
        }
    }
}
