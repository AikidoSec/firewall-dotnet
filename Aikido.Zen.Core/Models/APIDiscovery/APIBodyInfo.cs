using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents information about an API request body
    /// </summary>
    public class APIBodyInfo
    {
        /// <summary>
        /// Type of the body data
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Schema of the body data
        /// </summary>
        [JsonPropertyName("schema")]
        public DataSchema Schema { get; set; } = new DataSchema();
    }
}
