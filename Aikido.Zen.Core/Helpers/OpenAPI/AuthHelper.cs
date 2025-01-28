using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for merging authentication types
    /// </summary>
    public static class AuthHelper
    {
        /// <summary>
        /// Merge two lists of API authentication types
        /// </summary>
        /// <param name="existing">The existing list of authentication types</param>
        /// <param name="newAuth">The new list of authentication types to merge</param>
        /// <returns>A merged list of authentication types</returns>
        public static List<APIAuthType> MergeApiAuthTypes(List<APIAuthType> existing, List<APIAuthType> newAuth)
        {
            if (newAuth == null || !newAuth.Any())
                return existing;

            if (existing == null || !existing.Any())
                return newAuth;

            var result = new List<APIAuthType>(existing);

            foreach (var auth in newAuth)
            {
                if (!result.Any(a => IsEqualAPIAuthType(a, auth)))
                {
                    result.Add(auth);
                }
            }

            return result;
        }

        private static bool IsEqualAPIAuthType(APIAuthType a, APIAuthType b)
        {
            return a.Type == b.Type &&
                   a.In == b.In &&
                   a.Name == b.Name &&
                   a.Scheme == b.Scheme;
        }
    }
}
