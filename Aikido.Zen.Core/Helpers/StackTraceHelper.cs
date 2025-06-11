using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]
namespace Aikido.Zen.Core.Helpers

{
    public static class StackTraceHelper
    {
        /// <summary>
        /// Returns a cleaned and truncated stack trace.
        /// </summary>
        /// <param name="stackTrace">The stack trace to clean and truncate. If not provided, the current stack trace is used.</param>
        /// <returns>A cleaned and truncated stack trace.</returns>
        public static string CleanedStackTrace(string stackTrace = null)
        {
            if (stackTrace == null)
            {
                stackTrace = new StackTrace().ToString();
            }
            return stackTrace
            .CleanStackTrace()
            .TruncateStackTrace();
        }

        internal static string TruncateStackTrace(this string stackTrace, int maxLength = 8096)
        {
            if (string.IsNullOrEmpty(stackTrace) || maxLength <= 3)
            {
                return stackTrace;
            }
            if (stackTrace.Length > maxLength - 3)
            {
                return stackTrace.Substring(0, maxLength - 3) + "...";
            }
            return stackTrace;
        }

        internal static string CleanStackTrace(this string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return stackTrace;
            }
            // We want no Zen related lines in the stack trace
            var lines = stackTrace.Split('\n');
            var zenRegex = new Regex(@"Aikido\.Zen\..*");
            var cleanedLines = lines.Where(line => !zenRegex.IsMatch(line)).ToArray();
            return string.Join("\n", cleanedLines);
        }
    }
}
