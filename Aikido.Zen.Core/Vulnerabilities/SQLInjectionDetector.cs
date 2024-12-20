using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Vulnerabilities
{

    /// <summary>
    /// Detector for SQL injection vulnerabilities in query strings
    /// </summary>
    public class SQLInjectionDetector
    {
        /// <summary>
        /// Detects potential SQL injection vulnerabilities in a query string
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="userInput">The user input to check for injection attempts</param>
        /// <param name="dialect">The SQL dialect identifier</param>
        /// <returns>True if SQL injection is detected, false otherwise</returns>
        public static bool IsSQLInjection(string query, string userInput, SQLDialect dialect)
        {
            return ZenInternals.IsSQLInjection(query, userInput, dialect.ToRustDialectInt());
        }
    }
}
