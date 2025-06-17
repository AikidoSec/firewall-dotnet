using System;
using System.IO;
using System.Web;

namespace Aikido.Zen.Core.Vulnerabilities
{

    /// <summary>
    /// Detector for path traversal vulnerabilities in a string
    /// examples:
    /// "c:/windows/system32/cmd.exe"
    /// "c:/windows/system32/cmd.exe/../../"
    /// "%USERPROFILE%\\Desktop\\..\\"
    /// </summary>
    public static class PathTraversalDetector
    {
        private static readonly string[] DangerousPatterns = new[]
        {
            "../",
            "..\\",
        };

        private static readonly string[] DangerousPathStarts = new[]
        {
            // Linux root folders
            "/bin/",
            "/boot/",
            "/dev/",
            "/etc/",
            "/home/",
            "/init/",
            "/lib/",
            "/media/",
            "/mnt/",
            "/opt/",
            "/proc/",
            "/root/",
            "/run/",
            "/sbin/",
            "/srv/",
            "/sys/",
            "/tmp/",
            "/usr/",
            "/var/",
            // Windows drives and system paths
            "c:/",
            "c:\\",
            "d:/",
            "d:\\",
            "e:/",
            "e:\\",
            "windows/",
            "windows\\",
            "%windir%",
            "%systemroot%",
            "program files",
            "program files (x86)",
            "documents and settings",
            "users/",
            "%USERPROFILE%",
            "%HOMEPATH%",
            "%HOMEDRIVE%",
            "%PROGRAMDATA%",
            "%PROGRAMFILES%",
            "%PROGRAMFILES(X86)%",
            "%TEMP%",
            "%TMP%",
        };

        /// <summary>
        /// Detects potential path traversal attacks in a string
        /// </summary>
        /// <param name="input">The input string to check</param>
        /// <param name="path">The file path to check against</param>
        /// <param name="checkPathStart">Whether to check for absolute path traversal</param>
        /// <returns>True if potential path traversal is detected, false otherwise</returns>
        public static bool DetectPathTraversal(string input, string path, bool checkPathStart = true)
        {

            // return if path or input is null or empty
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(path))
                return false;
            // ignore if path does not contain user input
            if (!path.Contains(input))
                return false;
            // ignore if input is larger than the path
            if (input.Length > path.Length)
                return false;
            // Ignore single characters since they don't pose a big threat
            if (input.Length <= 1)
                return false;

            // URL decode the input first to catch encoded attacks
            try
            {
                // could be a double encoded path traversal
                input = HttpUtility.UrlDecode(input);
                input = HttpUtility.UrlDecode(input);
                // same for the path
                path = HttpUtility.UrlDecode(path);
                path = HttpUtility.UrlDecode(path);
            }
            catch
            {
                // If URL decode fails, check the raw input
            }

            // Convert to lowercase for case-insensitive matching
            ReadOnlySpan<char> inputSpan = input.ToLowerInvariant().AsSpan();
            ReadOnlySpan<char> pathSpan = path.ToLowerInvariant().AsSpan();


            bool inputHasUnsafeParts = ContainsUnsafePathParts(inputSpan);
            bool pathHasUnsafeParts = ContainsUnsafePathParts(pathSpan);
            if (inputHasUnsafeParts && pathHasUnsafeParts)
                return true;

            if (checkPathStart)
            {
                // Check for absolute path traversal
                foreach (var start in DangerousPathStarts)
                {
                    ReadOnlySpan<char> startSpan = start.AsSpan();

                    if (inputSpan.StartsWith(startSpan, StringComparison.OrdinalIgnoreCase) &&
                        pathSpan.StartsWith(startSpan, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsUnsafePathParts(ReadOnlySpan<char> path)
        {
            // Check for dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                ReadOnlySpan<char> patternSpan = pattern.AsSpan();
                if (path.Contains(patternSpan, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
