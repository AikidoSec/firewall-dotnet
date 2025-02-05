using System.Collections.Generic;
using System.Text.Json;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing JSON data.
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Flattens a JSON element into a dictionary with dot-notation keys.
        /// </summary>
        /// <param name="result">The dictionary to store flattened data.</param>
        /// <param name="element">The JSON element to flatten.</param>
        /// <param name="prefix">The prefix for keys in the dictionary.</param>
        public static void FlattenJson(IDictionary<string, string> result, JsonElement element, string prefix)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string arrayPrefix = string.IsNullOrEmpty(prefix) ? index.ToString() : $"{prefix}.{index}";
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        FlattenJson(result, item, arrayPrefix);
                    }
                    else
                    {
                        result[arrayPrefix] = item.ToString();
                    }
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        FlattenJson(result, property.Value, key);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        FlattenJson(result, property.Value, key);
                    }
                    else
                    {
                        result[key] = property.Value.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// Converts a JsonElement to its appropriate native object representation.
        /// </summary>
        /// <param name="element">The JsonElement to convert.</param>
        /// <returns>The converted object.</returns>
        public static object ToJsonObj(JsonElement element)
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

        /// <summary>
        /// Tries to parse a JSON string into a JsonElement.
        /// </summary>
        /// <param name="jsonString">The JSON string to parse.</param>
        /// <param name="jsonElement">The resulting JsonElement if parsing is successful.</param>
        /// <returns>True if parsing is successful, false otherwise.</returns>
        public static bool TryParseJson(string jsonString, out JsonElement jsonElement)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                jsonElement = default;
                return false;
            }

            try
            {
                // check for legal first characters
                if (jsonString[0] != '{' && jsonString[0] != '[')
                {
                    jsonElement = default;
                    return false;
                }

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    jsonElement = doc.RootElement.Clone();
                    return true;
                }
            }
            catch (JsonException)
            {
                jsonElement = default;
                return false;
            }
        }
    }
}
