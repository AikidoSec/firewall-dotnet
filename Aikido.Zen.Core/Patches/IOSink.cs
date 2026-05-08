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

        internal static bool OnPathOperation(object[] __args, MethodBase __originalMethod)
        {
            return OnFileOperation(GetStringArguments(__args, 1), __originalMethod);
        }

        internal static bool OnTwoPathOperation(object[] __args, MethodBase __originalMethod)
        {
            return OnFileOperation(GetStringArguments(__args, 2), __originalMethod);
        }

        private static bool OnFileOperation(string[] paths, MethodBase originalMethod)
        {
            if (IsProcessing.Value)
            {
                return true;
            }

            try
            {
                IsProcessing.Value = true;
                return paths.Length == 0
                    ? true
                    : OnFileOperation(paths, originalMethod, Patcher.GetContext());
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

        private static string[] GetStringArguments(object[] args, int count)
        {
            if (args == null || count <= 0)
            {
                return Array.Empty<string>();
            }

            return args
                .OfType<string>()
                .Where(path => !string.IsNullOrEmpty(path))
                .Take(count)
                .ToArray();
        }

    }
}
