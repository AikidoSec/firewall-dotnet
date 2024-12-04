using System.Net;
using System.Linq;

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
    }
}
