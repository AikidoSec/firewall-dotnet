using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Aikido.Zen.Core.Vulnerabilities
{
    /// <summary>
    /// Detector for NoSQL injection vulnerabilities in input data.
    /// </summary>
    public class NoSQLInjectionDetector
    {
        /// <summary>
        /// Detects potential NoSQL injection vulnerabilities in input data.
        /// </summary>
        /// <param name="context">The context containing user input data.</param>
        /// <param name="filter">The filter to check against the input data.</param>
        /// <returns>True if NoSQL injection is detected, false otherwise.</returns>
        public static bool DetectNoSQLInjection(Context context, object filter)
        {
            if (!(filter is JsonElement filterElement) || filterElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var source in context.GetSources())
            {
                if (source.ValueKind == JsonValueKind.Object || source.ValueKind == JsonValueKind.Array)
                {
                    if (FindFilterPartWithOperators(source, filterElement))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool FindFilterPartWithOperators(JsonElement userInput, JsonElement filterPart)
        {
            if (filterPart.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in filterPart.EnumerateObject())
                {
                    if (property.Name.StartsWith("$") && userInput.TryGetProperty(property.Name, out var userValue))
                    {
                        if (userValue.ValueKind == property.Value.ValueKind && userValue.ToString() == property.Value.ToString())
                        {
                            return true;
                        }
                    }

                    if (FindFilterPartWithOperators(userInput, property.Value))
                    {
                        return true;
                    }
                }
            }

            if (filterPart.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in filterPart.EnumerateArray())
                {
                    if (FindFilterPartWithOperators(userInput, item))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
