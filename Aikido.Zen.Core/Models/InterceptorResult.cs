using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents the result of an SSRF detection operation.
    /// </summary>
    public class InterceptorResult
    {
        /// <summary>
        /// The operation type that triggered the detection.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// The type of detected attack, e.g., "ssrf".
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// The source of the attack.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// A collection of paths leading to the payload.
        /// </summary>
        public List<string> PathsToPayload { get; set; }

        /// <summary>
        /// Additional attack metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// The actual payload string causing the alert.
        /// </summary>
        public string Payload { get; set; }
    }
}
