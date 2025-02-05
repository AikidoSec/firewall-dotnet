using System;
using System.Text;
using System.Text.Json;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing JWT tokens from authorization headers.
    /// </summary>
    public static class JwtHelper
    {
        /// <summary>
        /// Tries to decode a JWT token from the authorization header.
        /// </summary>
        /// <param name="authHeader">The authorization header value.</param>
        /// <returns>A tuple indicating if the token is a JWT and the decoded object if applicable.</returns>
        public static (bool IsJwt, object DecodedObject) TryDecodeAsJwt(string authHeader)
        {
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.Contains("."))
            {
                return (false, null);
            }
            authHeader = authHeader.Replace('-', '+').Replace('_', '/');
            var parts = authHeader.Split('.');

            if (parts.Length != 3)
            {
                return (false, null);
            }

            try
            {
                var payload = parts[1];
                // ensure padding
                if (payload.Length % 4 == 2)
                {
                    payload += "==";
                }
                else if (payload.Length % 4 == 3)
                {
                    payload += "=";
                }
                var jsonBytes = Convert.FromBase64String(payload);
                var jsonString = Encoding.UTF8.GetString(jsonBytes);
                var decodedObject = JsonSerializer.Deserialize<object>(jsonString);
                return (true, decodedObject);
            }
            catch (Exception ex)
            {
                return (false, null);
            }
        }
    }
}
