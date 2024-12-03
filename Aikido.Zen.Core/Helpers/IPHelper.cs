using System.Net;

namespace Aikido.Zen.Core.Helpers
{
    public class IPHelper
    {
        public static string Server
        {
            get
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                return ipAddress.ToString();
            }
        }
    }
}
