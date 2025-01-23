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
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Authentication scheme if Type is "http" (e.g., "basic", "bearer")
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        /// Location of the API key - either "header" or "cookie"
        /// </summary>
        public string In { get; set; }

        /// <summary>
        /// Name of the header or cookie if Type is "apiKey"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional type of the bearer token (e.g., JWT)
        /// </summary>
        public string BearerFormat { get; set; }
    }
}