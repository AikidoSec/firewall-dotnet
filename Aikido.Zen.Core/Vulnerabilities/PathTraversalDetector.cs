using System;
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
            "./",
            "../",
            "..\\",
            ";\\",
            ":\\",
            "system32",
            "system",
            "~",
            "web.config",
            "web.config:stream"
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
            "\\\\",
            "//",
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
        /// <param name="isUrl">Whether the input is a URL</param>
        /// <returns>True if potential path traversal is detected, false otherwise</returns>
        public static bool DetectPathTraversal(string input, string path, bool checkPathStart = true, bool isUrl = false)
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
            }
            catch
            {
                // If URL decode fails, check the raw input
            }

            // Convert to lowercase for case-insensitive matching
            ReadOnlySpan<char> inputSpan = input.ToLowerInvariant().AsSpan();
            ReadOnlySpan<char> pathSpan = path.ToLowerInvariant().AsSpan();

            // Check for URL path traversal
            if (isUrl && ContainsUnsafePathParts(inputSpan))
            {
                var filePathFromUrl = ParseAsFileUrl(input.AsSpan());
                if (!string.IsNullOrEmpty(filePathFromUrl) && path.Contains(filePathFromUrl))
                    return true;
            }

            // Check for dangerous patterns in input
            bool inputHasUnsafeParts = ContainsUnsafePathParts(inputSpan);
            if (inputHasUnsafeParts)
                return true;

            // Check for dangerous patterns in path
            bool pathHasUnsafeParts = ContainsUnsafePathParts(pathSpan);
            if (pathHasUnsafeParts)
                return true;

            if (checkPathStart)
            {
                // Check for absolute path traversal
                foreach (var start in DangerousPathStarts)
                {
                    ReadOnlySpan<char> startSpan = start.AsSpan();
                    if (inputSpan.StartsWith(startSpan, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsUnsafePathParts(ReadOnlySpan<char> path)
        {
            // Check for consecutive dots or slashes that might indicate traversal
            bool previousWasDot = false;
            bool previousWasSlash = false;

            foreach (char c in path)
            {
                if (c == '.')
                {
                    if (previousWasDot)
                        return true;
                    previousWasDot = true;
                }
                else
                {
                    previousWasDot = false;
                }

                if (c == '/' || c == '\\')
                {
                    if (previousWasSlash)
                        return true;
                    previousWasSlash = true;
                }
                else
                {
                    previousWasSlash = false;
                }
            }

            // Check for dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                ReadOnlySpan<char> patternSpan = pattern.AsSpan();
                if (path.Contains(patternSpan, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string ParseAsFileUrl(ReadOnlySpan<char> path)
        {
            if (!path.StartsWith("file:".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                if (!path.StartsWith("/".AsSpan()))
                {
                    return ParseAsFileUrl($"/{path.ToString()}".AsSpan());
                }
                return ParseAsFileUrl($"file://{path.ToString()}".AsSpan());
            }

            try
            {
                var uri = new Uri(path.ToString());
                return uri.LocalPath;
            }
            catch
            {
                return null;
            }
        }
    }
}
