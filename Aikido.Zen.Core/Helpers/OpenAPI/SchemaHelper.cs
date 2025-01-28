using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for merging schemas and generating data schemas
    /// </summary>
    public static class SchemaHelper
    {
        private const int MaxDepth = 20;
        private const int MaxProperties = 100;

        private const int MaxItemsToMerge = 10;

        /// <summary>
        /// Merge two data schemas into one
        /// </summary>
        /// <param name="first">The first schema to merge</param>
        /// <param name="second">The second schema to merge</param>
        /// <returns>A merged schema combining both input schemas</returns>
        public static DataSchema MergeDataSchemas(DataSchema first, DataSchema second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;
            var result = new DataSchema
            {
                Type = first.Type,
                Format = first.Format,
                Optional = first.Optional
            };

            if (!IsSameType(first.Type, second.Type))
            {
                return MergeTypes(first, second);
            }

            if (first.Properties != null && second.Properties != null)
            {
                result.Properties = new Dictionary<string, DataSchema>();

                foreach (var kvp in second.Properties)
                {
                    if (first.Properties.ContainsKey(kvp.Key))
                    {
                        result.Properties[kvp.Key] = MergeDataSchemas(first.Properties[kvp.Key], kvp.Value);
                    }
                    else
                    {
                        result.Properties[kvp.Key] = kvp.Value;
                        result.Properties[kvp.Key].Optional = true;
                    }
                }

                foreach (var kvp in first.Properties)
                {
                    if (!second.Properties.ContainsKey(kvp.Key))
                    {
                        result.Properties[kvp.Key] = kvp.Value;
                        result.Properties[kvp.Key].Optional = true;
                    }
                }
            }

            if (first.Items != null && second.Items != null)
            {
                result.Items = MergeDataSchemas(first.Items, second.Items);
            }

            if (first.Format != null && second.Format != null && first.Format != second.Format)
            {
                result.Format = null;
            }

            return result;
        }

        /// <summary>
        /// Get the schema of data (e.g., HTTP JSON body)
        /// </summary>
        /// <param name="data">The data to analyze</param>
        /// <param name="depth">Current recursion depth</param>
        /// <returns>A DataSchema describing the data structure</returns>
        public static DataSchema GetDataSchema(object data, int depth = 0)
        {
            if (depth >= MaxDepth)
                return new DataSchema { Type = new[] { "object" } };
            if (data == null)
                return new DataSchema { Type = new[] { "null" } };

            if (data is string str)
            {
                var format = OpenAPIHelper.GetStringFormat(str);
                return new DataSchema
                {
                    Type = new[] { "string" },
                    Format = format
                };
            }

            if (data is int || data is long || data is float || data is double || data is decimal)
                return new DataSchema { Type = new[] { "number" } };

            if (data is bool)
                return new DataSchema { Type = new[] { "boolean" } };

            if (data is IDictionary dict)
            {
                var schema = new DataSchema
                {
                    Type = new[] { "object" },
                    Properties = new Dictionary<string, DataSchema>()
                };

                if (depth < MaxDepth)
                {
                    var propertiesCount = 0;
                    foreach (DictionaryEntry kvp in dict)
                    {
                        if (propertiesCount >= MaxProperties)
                            break;

                        // Ensure the key is a string before proceeding
                        if (kvp.Key is string key)
                        {
                            schema.Properties[key] = GetDataSchema(kvp.Value, depth + 1);
                            propertiesCount++;
                        }
                    }
                }

                return schema;
            }

            if (data is IEnumerable enumerable)
            {
                // Initialize a variable to hold the schema of the items
                DataSchema itemsSchema = null;
                var itemsMerged = 0;

                // Iterate over the enumerable to determine the schema of the items
                foreach (var item in enumerable)
                {
                    itemsMerged++;
                    if (itemsMerged >= MaxItemsToMerge)
                        break;

                    // Merge the schema of each item with the existing itemsSchema
                    var right = GetDataSchema(item, depth + 1);
                    if (itemsSchema == null)
                    {
                        itemsSchema = right;
                    }
                    else
                    {
                        itemsSchema = MergeDataSchemas(itemsSchema, right);
                    }
                }

                return new DataSchema
                {
                    Type = new[] { "array" },
                    Items = itemsSchema
                };
            }

            return new DataSchema { Type = new[] { "object" } };
        }

        private static bool IsSameType(string[] first, string[] second)
        {
            return first.OrderBy(x => x).SequenceEqual(second.OrderBy(x => x));
        }

        private static DataSchema MergeTypes(DataSchema first, DataSchema second)
        {
            if (!OnlyContainsPrimitiveTypes(first.Type) || !OnlyContainsPrimitiveTypes(second.Type))
            {
                return first.Type.Contains("null") ? second : first;
            }

            first.Type = MergeTypeArrays(first.Type, second.Type);
            return first;
        }

        private static bool OnlyContainsPrimitiveTypes(string[] types)
        {
            return !types.Any(t => t == "object" || t == "array");
        }

        private static string[] MergeTypeArrays(string[] first, string[] second)
        {
            return first.Union(second).Distinct().ToArray();
        }
    }
}
