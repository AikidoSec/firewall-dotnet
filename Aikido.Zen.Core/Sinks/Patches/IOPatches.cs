using System.IO;
using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class IOPatches
    {
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
            var context = Patcher.GetContext();
            var pathAllowed = IOSink.OnFileOperation(path, __originalMethod, context);

            return pathAllowed;
        }

        [SinkPrefix(typeof(File), "Copy", "System.String", "System.String", "System.Boolean")]
        [SinkPrefix(typeof(File), "Move", "System.String", "System.String")]
        [SinkPrefix(typeof(File), "Move", "System.String", "System.String", "System.Boolean")]
        internal static bool OnFileOperationTwoPaths(string sourceFileName, string destFileName, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var sourceAllowed = IOSink.OnFileOperation(sourceFileName, __originalMethod, context);
            var destinationAllowed = IOSink.OnFileOperation(destFileName, __originalMethod, context);

            return sourceAllowed && destinationAllowed;
        }

        [SinkPrefix(typeof(Path), "GetFullPath", "System.String", "System.String")]
        internal static bool OnFileOperationPathWithBasePath(string path, string basePath, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var pathAllowed = IOSink.OnFileOperation(path, __originalMethod, context);
            var basePathAllowed = IOSink.OnFileOperation(basePath, __originalMethod, context);

            return pathAllowed && basePathAllowed;
        }
    }
}
