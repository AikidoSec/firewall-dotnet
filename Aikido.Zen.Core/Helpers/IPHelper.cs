using System.Net;
using System.Linq;
using System.Net.Sockets;
using System;
using Aikido.Zen.Core.Models.Ip;
using System.Collections;
using NetTools;

namespace Aikido.Zen.Core.Helpers
{
    public class IPHelper
    {
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
        /// Check if an IP address is in a subnet
        /// Example:
        /// Mask: 192.168.1.0/24
        /// Address: 192.168.1.1
        /// </summary>
        /// <param name="address"></param>
        /// <param name="subnet"></param>
        /// <returns></returns>
        public static bool IsInSubnet(IPAddress address, IPAddressRange subnet)
        {
            return subnet.Contains(address);
        }

        /// <summary>
        /// Checks if a given ip string is a subnet
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool IsSubnet(string ip)
        {
            return ip.Contains("/") && IPAddressRange.TryParse(ip, out _);
        }
    }
}
