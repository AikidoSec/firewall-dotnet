using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents the complete specification of an API endpoint
    /// </summary>
    public class APISpec
    {
        /// <summary>
        /// Information about the request body
        /// </summary>
        public APIBodyInfo Body { get; set; }

        /// <summary>
        /// Schema for query parameters
        /// </summary>
        public DataSchema Query { get; set; }

        /// <summary>
        /// Authentication types supported by the endpoint
        /// </summary>
        public List<APIAuthType> Auth { get; set; }
    }
}