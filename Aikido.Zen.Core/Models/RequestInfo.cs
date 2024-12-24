using System.Collections.Generic;

namespace Aikido.Zen.Core.Models {
    public class RequestInfo
    {
        public string Method { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Url { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
        public string Source { get; set; }
        public string Route { get; set; }
    }
}
