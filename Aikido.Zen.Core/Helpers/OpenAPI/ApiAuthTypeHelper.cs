using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for handling API authentication types
    /// </summary>
    public static class ApiAuthTypeHelper
    {
        private static readonly string[] CommonApiKeyHeaderNames = new string[]
        {
            "x-api-key",
            "api-key",
            "apikey",
            "x-token",
            "token"
        };

        private static readonly string[] CommonAuthCookieNames = new string[]
        {
            "auth",
            "session",
            "jwt",
            "token",
            "sid",
            "connect.sid",
            "auth_token",
            "access_token",
            "refresh_token"
        }.Concat(CommonApiKeyHeaderNames).ToArray();

        /// <summary>
        /// Get the authentication type from a Context
        /// </summary>
        /// <param name="context">The context containing headers and cookies</param>
        /// <returns>List of API authentication types, or null if none found</returns>
        public static List<APIAuthType> GetApiAuthType(Context context)
        {
            if ((context.Headers == null || !context.Headers.Any()) && (context.Cookies == null || !context.Cookies.Any()))
                return null;

            var result = new List<APIAuthType>();

            // Check Authorization header
            if (context.Headers.TryGetValue("authorization", out var authHeaderValue))
            {
                var authType = GetAuthorizationHeaderType(authHeaderValue);
                if (authType != null)
                    result.Add(authType);
            }

            // Check for API keys
            result.AddRange(FindApiKeys(context));

            return result.Any() ? result : null;
        }

        private static APIAuthType GetAuthorizationHeaderType(string authHeader)
        {
            if (string.IsNullOrWhiteSpace(authHeader))
                return null;

            if (authHeader.Contains(' '))
            {
                var parts = authHeader.Split(' ');
                var type = parts[0].ToLowerInvariant();

                if (IsHttpAuthScheme(type))
                {
                    return new APIAuthType
                    {
                        Type = "http",
                        Scheme = type
                    };
                }
            }

            return new APIAuthType
            {
                Type = "apiKey",
                In = "header",
                Name = "Authorization"
            };
        }

        private static bool IsHttpAuthScheme(string scheme)
        {
            return scheme == "bearer" || scheme == "basic" || scheme == "digest";
        }

        private static IEnumerable<APIAuthType> FindApiKeys(Context context)
        {
            var result = new List<APIAuthType>();

            // Check headers
            foreach (var header in CommonApiKeyHeaderNames)
            {
                if (context.Headers.ContainsKey(header))
                {
                    result.Add(new APIAuthType
                    {
                        Type = "apiKey",
                        In = "header",
                        Name = header
                    });
                }
            }

            // Check cookies
            if (context.Cookies != null && context.Cookies.Any())
            {
                var relevantCookies = context.Cookies.Keys
                    .Where(cookie => CommonAuthCookieNames.Contains(cookie.ToLowerInvariant()));

                foreach (var cookie in relevantCookies)
                {
                    result.Add(new APIAuthType
                    {
                        Type = "apiKey",
                        In = "cookie",
                        Name = cookie
                    });
                }
            }

            return result;
        }
    }
}
