using System.Collections.Generic;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for OpenAPI specification
    /// </summary>
    public static class OpenAPIHelper
    {
        private const int MaxDepth = 20;
        private const int MaxProperties = 100;

        /// <summary>
        /// Get the format of a string according to OpenAPI specification
        /// </summary>
        /// <param name="str">The string to analyze</param>
        /// <returns>The OpenAPI format of the string, or null if no specific format is detected</returns>
        public static string GetStringFormat(string str) => StringFormatHelper.GetStringFormat(str);

        /// <summary>
        /// Get the authentication type from a Context
        /// </summary>
        /// <param name="context">The context to extract API information from</param>
        /// <returns>List of API authentication types, or null if none found</returns>
        public static List<APIAuthType> GetApiAuthType(Context context) => ApiAuthTypeHelper.GetApiAuthType(context);

        /// <summary>
        /// Get the body data type based on content type header
        /// </summary>
        /// <param name="headers">Dictionary of HTTP headers</param>
        /// <returns>The data type as a string, or null if not determined</returns>
        public static string GetBodyDataType(Dictionary<string, string[]> headers) => ApiDataTypeHelper.GetBodyDataType(headers);

        /// <summary>
        /// Get API information from a context
        /// </summary>
        /// <param name="context">The context to extract API information from</param>
        /// <returns>API specification or null if no relevant information is found</returns>
        public static APISpec GetApiInfo(Context context) => ApiInfoHelper.GetApiInfo(context);

        /// <summary>
        /// Updates existing route API information with new context information
        /// </summary>
        /// <param name="context">The context containing new information</param>
        /// <param name="existingRoute">The existing route to update</param>
        /// <param name="maxSamples">Maximum number of samples to process</param>
        public static void UpdateApiInfo(Context context, Route existingRoute, int maxSamples) =>
            ApiInfoHelper.UpdateApiInfo(context, existingRoute, maxSamples);

        /// <summary>
        /// Merge two data schemas into one
        /// </summary>
        /// <param name="first">The first schema to merge</param>
        /// <param name="second">The second schema to merge</param>
        /// <returns>A merged schema combining both input schemas</returns>
        public static DataSchema MergeDataSchemas(DataSchema first, DataSchema second) =>
            SchemaHelper.MergeDataSchemas(first, second);

        /// <summary>
        /// Merge two lists of API authentication types
        /// </summary>
        /// <param name="existing">The existing list of authentication types</param>
        /// <param name="newAuth">The new list of authentication types to merge</param>
        /// <returns>A merged list of authentication types</returns>
        public static List<APIAuthType> MergeApiAuthTypes(List<APIAuthType> existing, List<APIAuthType> newAuth) =>
            AuthHelper.MergeApiAuthTypes(existing, newAuth);

        /// <summary>
        /// Get the schema of data (e.g., HTTP JSON body)
        /// </summary>
        /// <param name="data">The data to analyze</param>
        /// <param name="depth">Current recursion depth</param>
        /// <returns>A DataSchema describing the data structure</returns>
        public static DataSchema GetDataSchema(object data, int depth = 0) =>
            SchemaHelper.GetDataSchema(data, depth);
    }
}
