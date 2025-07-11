using System;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for resolving LLM method results from various types and wrappers
    /// </summary>
    public static class LLMResultHelper
    {
        /// <summary>
        /// Resolves the actual result from various wrapper types commonly used in LLM operations
        /// </summary>
        /// <param name="result">The result object to resolve</param>
        /// <returns>The resolved result object</returns>
        public static object ResolveResult(object result)
        {
            if (result == null)
                return null;

            object resolvedResult = result;

            try
            {
                var resultType = result.GetType();
                var typeName = resultType.Name;
                var typeFullName = resultType.FullName;

                // Handle ValueTask<T>
                if (typeName.StartsWith("ValueTask`") && resultType.IsGenericType)
                {
                    var resultProperty = resultType.GetProperty("Result");
                    resolvedResult = resultProperty?.GetValue(result);
                }
                // Handle Task<T>
                else if (typeName.StartsWith("Task`") && resultType.IsGenericType)
                {
                    var resultProperty = resultType.GetProperty("Result");
                    resolvedResult = resultProperty?.GetValue(result);
                }
                // Handle IAsyncEnumerable<T>
                else if (typeFullName?.Contains("IAsyncEnumerable") == true)
                {
                    resolvedResult = TryResolveAsyncEnumerable(result);
                }
                // Handle ClientResult<T>
                else if (typeName.StartsWith("ClientResult`") && resultType.IsGenericType)
                {
                    var resultProperty = resultType.GetProperty("Value");
                    resolvedResult = resultProperty?.GetValue(result);
                }
            }
            catch
            {
                resolvedResult = result;
            }

            return resolvedResult;
        }

        /// <summary>
        /// Attempts to resolve IAsyncEnumerable results by serializing to JSON and back
        /// </summary>
        /// <param name="asyncEnumerable">The async enumerable to resolve</param>
        /// <returns>A list of items or the original result if resolution fails</returns>
        private static object TryResolveAsyncEnumerable(object asyncEnumerable)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(asyncEnumerable);
                return System.Text.Json.JsonSerializer.Deserialize<List<object>>(json);
            }
            catch
            {
                return asyncEnumerable;
            }
        }
    }
}
