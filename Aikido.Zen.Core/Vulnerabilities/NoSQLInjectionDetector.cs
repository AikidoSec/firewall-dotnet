using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Aikido.Zen.Core.Helpers;

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
            if (MatchFlattenedInputWithFilter(context.ParsedUserInput, filterElement))
            {
                return true;
            }

            return false;
        }

        private static bool MatchFlattenedInputWithFilter(IDictionary<string, string> userInput, JsonElement filterPart)
        {
            if (filterPart.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in filterPart.EnumerateObject())
                {
                    if (property.Name == "$where")
                    {
                        // Detect JavaScript expressions in $where clauses
                        if (property.Value.ToString().Contains("sleep") || property.Value.ToString().Contains("eval"))
                        {
                            return true;
                        }
                    }
                    foreach (var userKey in userInput.Keys)
                    {
                        if (userKey.EndsWith(property.Name) && userInput[userKey] == property.Value.ToString() && property.Name.StartsWith("$"))
                        {
                            return true;
                        }
                    }
                    if (MatchFlattenedInputWithFilter(userInput, property.Value))
                    {
                        return true;
                    }
                }
            }

            if (filterPart.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in filterPart.EnumerateArray())
                {
                    if (MatchFlattenedInputWithFilter(userInput, item))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
