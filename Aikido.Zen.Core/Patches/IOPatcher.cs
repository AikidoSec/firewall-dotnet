using System;
using System.Diagnostics;
using System.Reflection;

using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Provides the core logic for handling patched file system operations.
    /// </summary>
    public static class IOPatcher
    {
        private const string OperationKind = "fs_op";

        /// <summary>
        /// A generic handler for file operations that checks for path traversal attacks.
        /// </summary>
        /// <param name="paths">The file or directory paths involved in the operation.</param>
        /// <param name="originalMethod">The original method being patched.</param>
        /// <param name="context">The context for the current operation.</param>
        /// <returns>Always returns true. Throws an exception if a blocked attack is detected.</returns>
        public static bool OnFileOperation(string[] paths, MethodBase originalMethod, Context context)
        {
            // Exclude certain assemblies to avoid stack overflow issues
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return true; // Skip processing for excluded assemblies
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
                if (paths != null && paths.Length > 0)
                {
                    attackDetected = PathTraversalHelper.DetectPathTraversal(paths, assemblyName, context, operation);
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
                throw AikidoException.PathTraversalDetected(operation, originalMethod.Name);
            }
            return true;
        }
    }
}
