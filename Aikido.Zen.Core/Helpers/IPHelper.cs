using System.Net;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Aikido.Zen.Core.Helpers
{
    public class IPHelper
    {
        private static readonly IPRange _privateIpRanges;

        static IPHelper ()
        {
            _privateIpRanges = new IPRange();

            // IPv4 private ranges
            var ipv4Ranges = new[]
            {
                "0.0.0.0/8",
                "10.0.0.0/8",
                "100.64.0.0/10",
                "127.0.0.0/8",
                "169.254.0.0/16",
                "172.16.0.0/12",
                "192.0.0.0/24",
                "192.0.2.0/24",
                "192.31.196.0/24",
                "192.52.193.0/24",
                "192.88.99.0/24",
                "192.168.0.0/16",
                "192.175.48.0/24",
                "198.18.0.0/15",
                "198.51.100.0/24",
                "203.0.113.0/24",
                "240.0.0.0/4",
                "224.0.0.0/4",
                "255.255.255.255/32"
            };

            // IPv6 private ranges
            var ipv6Ranges = new[]
            {
                "::/128",           // Unspecified address
                "::1/128",          // Loopback address
                "fc00::/7",         // Unique local address (ULA)
                "fe80::/10",        // Link-local address (LLA)
                "::ffff:127.0.0.1/128", // IPv4-mapped address
                "::ffff:0:0/96"     // IPv4-mapped addresses
            };

            foreach (var range in ipv4Ranges.Concat(ipv6Ranges))
            {
                _privateIpRanges.InsertRange(range);
            }
        }

        public static string Server
        {
            get
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return ipAddress?.ToString() ?? "127.0.0.1";
            }
        }

        /// <summary>
        /// Checks if a given IP address is valid.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP address is valid, false otherwise.</returns>
        public static bool IsValidIp (string ip)
        {
            return IPAddress.TryParse(ip, out _);
        }

        /// <summary>
        /// Converts an IP address range to a list of CIDR strings.
        /// </summary>
        /// <param name="startIp">The start IP address.</param>
        /// <param name="endIp">The end IP address.</param>
        /// <returns>A list of CIDR string representations of the IP address range.</returns>
        public static List<string> ToCidrString (string startIp, string endIp = null)
        {
            if (endIp == null)
            {
                return new List<string> { startIp };
            }
            // if already a CIDR, return it
            if (startIp.Contains("/"))
            {
                return new List<string> { startIp };
            }

            long start = IpToLong(startIp);
            long end = IpToLong(endIp);
            var result = new List<string>();

            while (end >= start)
            {
                byte maxSize = 32;
                while (maxSize > 0)
                {
                    long mask = IMask(maxSize - 1);
                    long maskBase = start & mask;

                    if (maskBase != start)
                    {
                        break;
                    }

                    maxSize--;
                }
                double x = Math.Log(end - start + 1) / Math.Log(2);
                byte maxDiff = (byte)(32 - Math.Floor(x));
                if (maxSize < maxDiff)
                {
                    maxSize = maxDiff;
                }
                result.Add(LongToIp(start) + "/" + maxSize);
                start += (long)Math.Pow(2, (32 - maxSize));
            }
            return result;
        }

        /// <summary>
        /// Checks if a given IP address is a private or local IP address.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP address is a private or local IP address, false otherwise.</returns>
        public static bool IsPrivateOrLocalIp (string ip)
        {
            if (!IPAddress.TryParse(ip, out var parsedIp))
            {
                return false;
            }
            return IPAddress.IsLoopback(parsedIp) || IsPrivateIPAddress(parsedIp);
        }

        /// <summary>
        /// Converts an IP address to a long integer.
        /// </summary>
        /// <param name="ipAddress">The IP address as a string.</param>
        /// <returns>The IP address as a long integer.</returns>
        private static long IpToLong (string ipAddress)
        {
            IPAddress ip;
            if (IPAddress.TryParse(ipAddress, out ip))
            {
                byte[] bytes = ip.GetAddressBytes();
                return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
            }
            return -1;
        }

        /// <summary>
        /// Converts a long integer to an IP address string.
        /// </summary>
        /// <param name="ipAddress">The IP address as a long integer.</param>
        /// <returns>The IP address as a string.</returns>
        private static string LongToIp (long ipAddress)
        {
            // Ensure the IP address is within the valid range for IPv4
            if (ipAddress < 0 || ipAddress > 0xFFFFFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(ipAddress), "The IP address is out of range for IPv4.");
            }

            // Convert the long to a 4-byte array
            byte[] bytes = new byte[4];
            bytes[0] = (byte)((ipAddress >> 24) & 0xFF);
            bytes[1] = (byte)((ipAddress >> 16) & 0xFF);
            bytes[2] = (byte)((ipAddress >> 8) & 0xFF);
            bytes[3] = (byte)(ipAddress & 0xFF);

            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// Generates a mask for a given size.
        /// </summary>
        /// <param name="s">The size of the mask.</param>
        /// <returns>The mask as a long integer.</returns>
        private static long IMask (int s)
        {
            return (long)(Math.Pow(2, 32) - Math.Pow(2, (32 - s)));
        }

        /// <summary>
        /// Checks if an IP address is a private IP address.
        /// Supports both IPv4 and IPv6 address formats using a comprehensive list of private IP ranges.
        /// https://github.com/AikidoSec/firewall-node/blob/02f25f1e2566c84e695b9f4b7d1723138485654d/library/vulnerabilities/ssrf/isPrivateIP.ts
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP address is private, false otherwise.</returns>
        private static bool IsPrivateIPAddress (IPAddress ip)
        {
            return _privateIpRanges.IsIpInRange(ip.ToString());
        }
    }
}
