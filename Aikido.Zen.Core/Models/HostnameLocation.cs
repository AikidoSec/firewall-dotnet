namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents where a hostname was found in the context.
    /// Inherits from Host to reuse hostname and port information.
    /// </summary>
    public class HostnameLocation : Host
    {
        /// <summary>
        /// The source where the hostname was found (e.g., "query", "headers", "body").
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The path to the payload in the source.
        /// </summary>
        public string PathToPayload { get; set; }

        /// <summary>
        /// The actual payload containing the hostname.
        /// </summary>
        public string Payload { get; set; }
    }
}
