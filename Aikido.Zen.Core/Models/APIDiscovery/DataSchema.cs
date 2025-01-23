using System;
using System.Collections.Generic;
using System.Text.Json;

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
        public string[] Type { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Indicates if this property is optional
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Properties for an object containing the DataSchema for each property
        /// </summary>
        public Dictionary<string, DataSchema> Properties { get; set; }

        /// <summary>
        /// Schema for items if this is an array type
        /// </summary>
        public DataSchema Items { get; set; }

        /// <summary>
        /// Format of the string if this is a string type
        /// </summary>
        public string Format { get; set; }

        public override string ToString()
        {
            var options = new JsonSerializerOptions
            {
                MaxDepth = 64,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
