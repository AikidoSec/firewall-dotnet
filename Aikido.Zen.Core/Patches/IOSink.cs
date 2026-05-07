using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    public static class IOSink
    {
        private const string OperationKind = "fs_op";
        private static readonly ThreadLocal<bool> IsProcessing = new ThreadLocal<bool>(() => false);

        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "Open", "System.String", "System.IO.FileMode", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "OpenRead", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "OpenWrite", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "Delete", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean", PathArgumentIndexes = new int[] { 0, 1 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "Move", "System.String", "System.String", PathArgumentIndexes = new int[] { 0, 1 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "Move", "System.String", "System.String", "System.Boolean", PathArgumentIndexes = new int[] { 0, 1 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "ReadAllText", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "ReadAllBytes", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "WriteAllText", "System.String", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.File", "AppendAllText", "System.String", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Path", "GetFullPath", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Path", "GetFullPath", "System.String", "System.String", PathArgumentIndexes = new int[] { 0, 1 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "CreateDirectory", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "Delete", "System.String", "System.Boolean", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "GetFiles", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "GetFiles", "System.String", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "GetDirectories", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String", PathArgumentIndexes = new int[] { 0 })]
        [PatchTarget(PatchKind.Prefix, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption", PathArgumentIndexes = new int[] { 0 })]
        private static bool OnFileOperation(object[] __args, MethodBase __originalMethod)
        {
            if (IsProcessing.Value)
            {
                return true;
            }

            try
            {
                IsProcessing.Value = true;
                var paths = GetPaths(__args, __originalMethod);
                return paths.Length == 0
                    ? true
                    : OnFileOperation(paths, __originalMethod, Patcher.GetContext());
            }
            finally
            {
                IsProcessing.Value = false;
            }
        }

        public static bool OnFileOperation(string[] paths, MethodBase originalMethod, Context context)
        {
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return true;
            }

            if (Context.IsBypassed(context))
            {
                return true;
            }

            var methodInfo = originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var assemblyName = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            var stopwatch = Stopwatch.StartNew();
            var withoutContext = context == null;
            var attackDetected = false;
            var blocked = false;

            try
            {
                if (paths != null && paths.Length > 0 &&
                    !Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    foreach (var path in paths)
                    {
                        if (PathTraversalHelper.DetectPathTraversal(path, context, assemblyName, operation))
                        {
                            attackDetected = true;
                            break;
                        }
                    }
                }

                blocked = attackDetected && !EnvironmentHelper.DryMode;
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error during Path Traversal detection.");
            }
            finally
            {
                stopwatch.Stop();
                try
                {
                    Agent.Instance?.Context?.OnInspectedCall(operation, OperationKind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
                }
                catch
                {
                    LogHelper.ErrorLog(Agent.Logger, "Error recording OnInspectedCall stats.");
                }
            }

            if (blocked)
            {
                throw AikidoException.PathTraversalDetected(operation);
            }

            return true;
        }

        private static string[] GetPaths(object[] args, MethodBase originalMethod)
        {
            var pathArgumentIndexes = Patcher.GetPatchTarget(originalMethod)?.PathArgumentIndexes;
            if (args == null || pathArgumentIndexes == null || pathArgumentIndexes.Length == 0)
            {
                return Array.Empty<string>();
            }

            return pathArgumentIndexes
                .Where(index => index >= 0 && index < args.Length)
                .Select(index => args[index] as string)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();
        }

    }
}
