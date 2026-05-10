using System.Diagnostics;
using System.Reflection;
using System.Threading;

using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Sinks
{
    /// <summary>
    /// Provides the core logic for handling patched file system operations.
    /// </summary>
    internal static class IOSink
    {
        private const string OperationKind = "fs_op";
        private static readonly ThreadLocal<bool> IsProcessing = new ThreadLocal<bool>(() => false);

        /// <summary>
        /// A generic handler for file operations that checks for path traversal attacks.
        /// </summary>
        /// <param name="path">The file or directory path involved in the operation.</param>
        /// <param name="originalMethod">The original method being patched.</param>
        /// <param name="context">The context for the current operation.</param>
        /// <returns>Always returns true. Throws an exception if a blocked attack is detected.</returns>
        internal static bool OnFileOperation(string path, MethodBase originalMethod, Context context)
        {
            if (IsProcessing.Value)
            {
                return true;
            }

            try
            {
                IsProcessing.Value = true;
                // Exclude certain assemblies to avoid stack overflow issues
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
                var module = methodInfo?.DeclaringType?.Assembly.GetName().Name;
                var stopwatch = Stopwatch.StartNew();
                var withoutContext = context == null;
                var attackDetected = false;
                var blocked = false;

                try
                {
                    if (!string.IsNullOrEmpty(path) &&
                        !Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                    {
                        attackDetected = PathTraversalHelper.DetectPathTraversal(path, context, module, operation);
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
            finally
            {
                IsProcessing.Value = false;
            }
        }

    }
}
