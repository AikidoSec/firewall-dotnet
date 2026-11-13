using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing JSON data.
    /// </summary>
    internal static class JsonHelper
    {
        /// <summary>
        /// Flattens a JSON element into a list of values and dot-notation locations.
        /// </summary>
        /// <param name="result">The list to store flattened data.</param>
        /// <param name="element">The JSON element to flatten.</param>
        /// <param name="prefix">The prefix for input locations.</param>
        internal static void FlattenJson(List<UserInput> result, JsonElement element, string prefix)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string arrayPrefix = string.IsNullOrEmpty(prefix) ? index.ToString() : $"{prefix}.{index}";
                    FlattenJson(result, item, arrayPrefix);
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    result.Add(new UserInput(key, property.Name));
                    FlattenJson(result, property.Value, key);
                }
            }
            else
            {
                // Remaining JsonValueKind values are String, Number, True, False, or Null.
                result.Add(new UserInput(prefix ?? string.Empty, element.ToString()));
            }
        }

        /// <summary>
        /// Converts a JsonElement to its appropriate native object representation.
        /// </summary>
        /// <param name="element">The JsonElement to convert.</param>
        /// <returns>The converted object.</returns>
        internal static object ToJsonObj(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ToJsonObj(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ToJsonObj(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.TryGetInt64(out long l) ? l : element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return null;
            }
        }
    }
}
