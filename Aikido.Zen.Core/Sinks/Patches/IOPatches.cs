using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class IOPatches
    {
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "Open", "System.String", "System.IO.FileMode")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "OpenRead", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "OpenWrite", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "Delete", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "ReadAllText", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "ReadAllBytes", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "WriteAllText", "System.String", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "AppendAllText", "System.String", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Path", "GetFullPath", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "CreateDirectory", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "Delete", "System.String", "System.Boolean")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "GetFiles", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "GetFiles", "System.String", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "GetDirectories", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "GetDirectories", "System.String", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption")]
        internal static bool OnePath(string path, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var pathAllowed = IOSink.OnFileOperation(path, __originalMethod, context);

            return pathAllowed;
        }

        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "Move", "System.String", "System.String")]
        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.File", "Move", "System.String", "System.String", "System.Boolean")]
        internal static bool TwoFilePaths(string sourceFileName, string destFileName, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var sourceAllowed = IOSink.OnFileOperation(sourceFileName, __originalMethod, context);
            var destinationAllowed = IOSink.OnFileOperation(destFileName, __originalMethod, context);

            return sourceAllowed && destinationAllowed;
        }

        [SinkPrefix(new[] { "System.Private.CoreLib", "mscorlib" }, "System.IO.Path", "GetFullPath", "System.String", "System.String")]
        internal static bool PathWithBasePath(string path, string basePath, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var pathAllowed = IOSink.OnFileOperation(path, __originalMethod, context);
            var basePathAllowed = IOSink.OnFileOperation(basePath, __originalMethod, context);

            return pathAllowed && basePathAllowed;
        }
    }
}
