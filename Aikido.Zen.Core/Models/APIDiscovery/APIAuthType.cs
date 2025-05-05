using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents authentication type information for an API.
    /// https://swagger.io/docs/specification/authentication/
    /// </summary>
    public class APIAuthType
    {
        /// <summary>
        /// Type of authentication - either "http" for authorization header or "apiKey"
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Authentication scheme if Type is "http" (e.g., "basic", "bearer")
        /// </summary>
        [JsonPropertyName("scheme")]
        public string Scheme { get; set; }

        /// <summary>
        /// Location of the API key - either "header" or "cookie"
        /// </summary>
        [JsonPropertyName("in")]
        public string In { get; set; }

        /// <summary>
        /// Name of the header or cookie if Type is "apiKey"
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Optional type of the bearer token (e.g., JWT)
        /// </summary>
        [JsonPropertyName("bearerFormat")]
        public string BearerFormat { get; set; }
    }
}
