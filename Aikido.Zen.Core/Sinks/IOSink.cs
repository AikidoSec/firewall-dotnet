using System.IO;
using System.Reflection;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    /// <summary>
    /// Provides the core logic for handling patched file system operations.
    /// </summary>
    internal static class IOSink
    {
        private const string OperationKind = "fs_op";

        [SinkPrefix(typeof(File), "Open", "System.String", "System.IO.FileMode")]
        [SinkPrefix(typeof(File), "OpenRead", "System.String")]
        [SinkPrefix(typeof(File), "OpenWrite", "System.String")]
        [SinkPrefix(typeof(File), "Create", "System.String", "System.Int32", "System.IO.FileOptions")]
        [SinkPrefix(typeof(File), "Delete", "System.String")]
        [SinkPrefix(typeof(File), "ReadAllText", "System.String")]
        [SinkPrefix(typeof(File), "ReadAllBytes", "System.String")]
        [SinkPrefix(typeof(File), "WriteAllText", "System.String", "System.String")]
        [SinkPrefix(typeof(File), "WriteAllBytes", "System.String", "System.Byte[]")]
        [SinkPrefix(typeof(File), "AppendAllText", "System.String", "System.String")]
        [SinkPrefix(typeof(Path), "GetFullPath", "System.String")]
        [SinkPrefix(typeof(Directory), "CreateDirectory", "System.String")]
        [SinkPrefix(typeof(Directory), "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity")]
        [SinkPrefix(typeof(Directory), "Delete", "System.String", "System.Boolean")]
        [SinkPrefix(typeof(Directory), "GetFiles", "System.String")]
        [SinkPrefix(typeof(Directory), "GetFiles", "System.String", "System.String")]
        [SinkPrefix(typeof(Directory), "GetFiles", "System.String", "System.String", "System.IO.SearchOption")]
        [SinkPrefix(typeof(Directory), "GetDirectories", "System.String")]
        [SinkPrefix(typeof(Directory), "GetDirectories", "System.String", "System.String")]
        [SinkPrefix(typeof(Directory), "GetDirectories", "System.String", "System.String", "System.IO.SearchOption")]
        internal static bool OnFileOperationOnePath(string path, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => InspectPaths(context, path));
        }

        [SinkPrefix(typeof(File), "Copy", "System.String", "System.String", "System.Boolean")]
        [SinkPrefix(typeof(File), "Move", "System.String", "System.String")]
        [SinkPrefix(typeof(File), "Move", "System.String", "System.String", "System.Boolean")]
        internal static bool OnFileOperationTwoPaths(string sourceFileName, string destFileName, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => InspectPaths(context, sourceFileName, destFileName));
        }

        [SinkPrefix(typeof(Path), "GetFullPath", "System.String", "System.String")]
        internal static bool OnFileOperationPathWithBasePath(string path, string basePath, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => InspectPaths(context, path, basePath));
        }

        private static InspectionResult InspectPaths(Context context, params string[] paths)
        {
            try
            {
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    var result = PathTraversalHelper.DetectPathTraversal(path, context);
                    if (result.AttackKind.HasValue)
                    {
                        return result;
                    }
                }
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error during Path Traversal detection.");
            }

            return InspectionResult.Allow();
        }
    }
}
