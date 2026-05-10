using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class IOPatches
    {
        [SinkPrefix("", "System.IO.File", "Open", "System.String", "System.IO.FileMode")]
        [SinkPrefix("", "System.IO.File", "OpenRead", "System.String")]
        [SinkPrefix("", "System.IO.File", "OpenWrite", "System.String")]
        [SinkPrefix("", "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions")]
        [SinkPrefix("", "System.IO.File", "Delete", "System.String")]
        [SinkPrefix("", "System.IO.File", "ReadAllText", "System.String")]
        [SinkPrefix("", "System.IO.File", "ReadAllBytes", "System.String")]
        [SinkPrefix("", "System.IO.File", "WriteAllText", "System.String", "System.String")]
        [SinkPrefix("", "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]")]
        [SinkPrefix("", "System.IO.File", "AppendAllText", "System.String", "System.String")]
        [SinkPrefix("", "System.IO.Path", "GetFullPath", "System.String")]
        [SinkPrefix("", "System.IO.Directory", "CreateDirectory", "System.String")]
        [SinkPrefix("", "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity")]
        [SinkPrefix("", "System.IO.Directory", "Delete", "System.String", "System.Boolean")]
        [SinkPrefix("", "System.IO.Directory", "GetFiles", "System.String")]
        [SinkPrefix("", "System.IO.Directory", "GetFiles", "System.String", "System.String")]
        [SinkPrefix("", "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption")]
        [SinkPrefix("", "System.IO.Directory", "GetDirectories", "System.String")]
        [SinkPrefix("", "System.IO.Directory", "GetDirectories", "System.String", "System.String")]
        [SinkPrefix("", "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption")]
        internal static bool OnePath(string path, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var pathAllowed = IOSink.OnFileOperation(path, __originalMethod, context);

            return pathAllowed;
        }

        [SinkPrefix("", "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean")]
        [SinkPrefix("", "System.IO.File", "Move", "System.String", "System.String")]
        [SinkPrefix("", "System.IO.File", "Move", "System.String", "System.String", "System.Boolean")]
        internal static bool TwoFilePaths(string sourceFileName, string destFileName, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var sourceAllowed = IOSink.OnFileOperation(sourceFileName, __originalMethod, context);
            var destinationAllowed = IOSink.OnFileOperation(destFileName, __originalMethod, context);

            return sourceAllowed && destinationAllowed;
        }

        [SinkPrefix("", "System.IO.Path", "GetFullPath", "System.String", "System.String")]
        internal static bool PathWithBasePath(string path, string basePath, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var pathAllowed = IOSink.OnFileOperation(path, __originalMethod, context);
            var basePathAllowed = IOSink.OnFileOperation(basePath, __originalMethod, context);

            return pathAllowed && basePathAllowed;
        }
    }
}
