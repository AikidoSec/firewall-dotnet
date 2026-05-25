using System;
using System.IO;

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
            // Linux specific
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
            // Common container/cloud directories
            "/app/",
            "/code/",
            // macOS specific
            "/applications/",
            "/cores/",
            "/library/",
            "/private/",
            "/users/",
            "/system/",
            "/volumes/",
            // Windows specific
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

        private static readonly char[] PathStartNoise = { '/', '\\', '.', '?' };

        private static readonly string[] NormalizedDangerousPathStarts = Array.ConvertAll(DangerousPathStarts, pathStart => pathStart.TrimStart(PathStartNoise));

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

            ReadOnlySpan<char> inputSpan = input.AsSpan();
            ReadOnlySpan<char> pathSpan = path.AsSpan();

            bool inputHasUnsafeParts = ContainsUnsafePathParts(inputSpan);
            bool pathHasUnsafeParts = ContainsUnsafePathParts(pathSpan);
            if (inputHasUnsafeParts && pathHasUnsafeParts)
                return true;

            if (checkPathStart && StartsWithUnsafePath(pathSpan, inputSpan))
                return true;

            return false;
        }

        private static bool StartsWithUnsafePath(ReadOnlySpan<char> path, ReadOnlySpan<char> input)
        {
            ReadOnlySpan<char> normalizedInput = input.TrimStart(PathStartNoise.AsSpan());
            ReadOnlySpan<char> normalizedPath = path.TrimStart(PathStartNoise.AsSpan());

            // Check for absolute path traversal
            foreach (var start in NormalizedDangerousPathStarts)
            {
                ReadOnlySpan<char> startSpan = start.AsSpan();

                if (normalizedInput.StartsWith(startSpan, StringComparison.OrdinalIgnoreCase) &&
                    normalizedPath.StartsWith(normalizedInput, StringComparison.OrdinalIgnoreCase))
                {
                    // Bare root directories, such as /etc/ or /app/, are not enough to flag traversal.
                    if (startSpan.Length > 0 &&
                        startSpan[startSpan.Length - 1] == '/' &&
                        normalizedInput.Equals(startSpan, StringComparison.OrdinalIgnoreCase))
                        return false;

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
