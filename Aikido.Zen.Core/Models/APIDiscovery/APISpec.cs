using System.Collections.Generic;
using System.Text.Json.Serialization;
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
        [JsonPropertyName("body")]
        public APIBodyInfo Body { get; set; }

        /// <summary>
        /// Schema for query parameters
        /// </summary>
        [JsonPropertyName("query")]
        public DataSchema Query { get; set; }

        /// <summary>
        /// Authentication types supported by the endpoint
        /// </summary>
        [JsonPropertyName("auth")]
        public List<APIAuthType> Auth { get; set; }
    }
}
