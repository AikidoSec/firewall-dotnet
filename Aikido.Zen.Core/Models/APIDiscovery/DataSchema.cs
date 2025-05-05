using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents the schema of data in an API request or response
    /// </summary>
    public class DataSchema
    {
        /// <summary>
        /// Type of this property (e.g., "string", "number", "object", "array", "null")
        /// </summary>
        [JsonIgnore]
        public string[] Type { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Type of this property (e.g., "string", "number", "object", "array", "null")
        /// defaults to "object" if not set
        /// </summary>
        [JsonPropertyName("type")]
        public string TypeAsString => string.Join("|", Type.Any() ? Type : new[] { "object" });

        /// <summary>
        /// Indicates if this property is optional
        /// </summary>
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        /// <summary>
        /// Properties for an object containing the DataSchema for each property
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, DataSchema> Properties { get; set; }

        /// <summary>
        /// Schema for items if this is an array type
        /// </summary>
        [JsonPropertyName("items")]
        public DataSchema Items { get; set; }

        /// <summary>
        /// Format of the string if this is a string type
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; }

        public override string ToString()
        {
            var options = new JsonSerializerOptions
            {
                MaxDepth = 64,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
