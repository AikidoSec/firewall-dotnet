using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Aikido.Zen.Core.Helpers
{
    // Arguments encodes values in one string; ArgumentList already holds the values.
    // For example, -m "two words" becomes ["-m", "two words"].
    internal static class ProcessHelper
    {
        // Decode process-argument escapes so paths such as C:\temp stay intact and \" supplies a literal quote.
        // Before a quote, backslash pairs become one backslash; an odd remainder escapes the quote.
        private const string literalBackslashes = @"(?<text>\\+)(?![\\""])";
        private const string backslashPairs = @"(?:(?<text>\\)\\)*";
        private const string escapedQuote = backslashPairs + @"\\(?<text>"")";
        // Inside quotes, doubled quotes are literal ("a""b" becomes a"b); .NET also accepts an unclosed quote.
        private static readonly string quotedPart = $@"
            {backslashPairs}""
            (?> (?<text>[^\\""]+) | {literalBackslashes} | {escapedQuote} | {backslashPairs}""(?<text>"") )*
            (?: {backslashPairs}"" | \z )";
        // Process.Start only treats space and tab as separators; a newline belongs to the argument.
        // Atomic groups avoid repeated work on long input by preventing backtracking over consumed separators and fragments.
        private static readonly Regex processArguments = new Regex($@"
            \G(?>[ \t]*)
            (?> (?<text>[^ \t\\""]+) | {literalBackslashes} | {escapedQuote} | {quotedPart} )+",
            RegexOptions.IgnorePatternWhitespace);

        // ArgumentList is not part of netstandard2.0.
        private static readonly PropertyInfo argumentListProperty = typeof(ProcessStartInfo).GetProperty("ArgumentList");

        internal static List<string> GetArguments(ProcessStartInfo startInfo)
        {
            var arguments = string.IsNullOrEmpty(startInfo.Arguments)
                ? GetArgumentList(startInfo)
                : DecodeProcessArguments(startInfo.Arguments);
            return arguments.ToList();
        }

        internal static IEnumerable<string> DecodeProcessArguments(string arguments)
        {
            // Quoted and unquoted fragments can form one argument: "echo hello";whoami becomes echo hello;whoami.
            // Group.Value returns only the last fragment, so use Captures to preserve the complete argument.
            return processArguments.Matches(arguments).Cast<Match>()
                .Select(match => string.Concat(match.Groups["text"].Captures.Cast<Capture>().Select(capture => capture.Value)));
        }

        private static IEnumerable<string> GetArgumentList(ProcessStartInfo startInfo)
        {
            return argumentListProperty == null
                ? Array.Empty<string>()
                : (IEnumerable<string>)argumentListProperty.GetValue(startInfo);
        }
    }
}
