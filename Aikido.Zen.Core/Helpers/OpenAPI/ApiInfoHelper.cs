using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers.OpenAPI;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for handling API information
    /// </summary>
    public static class ApiInfoHelper
    {
        /// <summary>
        /// Get API information from a context
        /// </summary>
        /// <param name="context">The context to extract API information from</param>
        /// <returns>API specification or null if no relevant information is found</returns>
        public static APISpec GetApiInfo(Context context)
        {
            try
            {
                APIBodyInfo bodyInfo = null;
                DataSchema queryInfo = null;

                if (context.ParsedBody != null && !context.IsGraphQL)
                {
                    bodyInfo = new APIBodyInfo
                    {
                        Type = ApiDataTypeHelper.GetBodyDataType(context.Headers) ?? "unknown",
                        Schema = OpenAPIHelper.GetDataSchema(context.ParsedBody)
                    };
                }

                if (context.Query != null && context.Query.Any())
                {
                    var queryInfoProperties = context.Query?
                        .Select(x => new KeyValuePair<string, DataSchema>(x.Key, OpenAPIHelper.GetDataSchema(x.Value?.FirstOrDefault() ?? string.Empty)))
                        .ToDictionary(x => x.Key, x => x.Value);
                    queryInfo = new DataSchema
                    {
                        Properties = queryInfoProperties
                    };
                }

                var authInfo = ApiAuthTypeHelper.GetApiAuthType(context);

                if (bodyInfo == null && queryInfo == null && authInfo == null)
                    return null;

                return new APISpec
                {
                    Body = bodyInfo,
                    Query = queryInfo,
                    Auth = authInfo
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Updates existing route API information with new context information
        /// </summary>
        /// <param name="context">The context containing new information</param>
        /// <param name="existingRoute">The existing route to update</param>
        /// <param name="maxSamples">Maximum number of samples to process</param>
        public static void UpdateApiInfo(Context context, Route existingRoute, int maxSamples)
        {
            if (existingRoute.Hits > maxSamples)
                return;

            try
            {
                var newInfo = GetApiInfo(context);
                if (newInfo == null)
                    return;

                var existingSpec = existingRoute.ApiSpec;

                // Merge body schemas
                if (existingSpec.Body != null && newInfo.Body != null)
                {
                    existingSpec.Body = new APIBodyInfo
                    {
                        Type = newInfo.Body.Type,
                        Schema = SchemaHelper.MergeDataSchemas(existingSpec.Body.Schema, newInfo.Body.Schema)
                    };
                }
                else if (newInfo.Body != null)
                {
                    existingSpec.Body = newInfo.Body;
                }

                // Merge query schemas
                if (newInfo.Query != null && newInfo.Query.Properties?.Any() == true)
                {
                    if (existingSpec.Query != null)
                    {
                        existingSpec.Query = SchemaHelper.MergeDataSchemas(existingSpec.Query, newInfo.Query);
                    }
                    else
                    {
                        existingSpec.Query = newInfo.Query;
                    }
                }

                // Merge auth types
                existingSpec.Auth = AuthHelper.MergeApiAuthTypes(existingSpec.Auth, newInfo.Auth);

                // Normalize empty spec
                if (existingSpec.Body == null && existingSpec.Query == null && existingSpec.Auth == null)
                {
                    existingRoute.ApiSpec = new APISpec();
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
