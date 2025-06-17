using System;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    public static class IOPatcher
    {
        private const string operationKind = "fs_op";
        public static bool OnFileOperation(object[] __args, System.Reflection.MethodBase __originalMethod, Context context)
        {
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var assemblyName = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;
            try
            {
                attackDetected = PathTraversalHelper.DetectPathTraversal(__args, __originalMethod, assemblyName, context, operation);
                blocked = attackDetected && !EnvironmentHelper.DryMode;
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error during Path Traversal detection.");
            }
            stopwatch.Stop();
            try
            {
                Agent.Instance?.Context?.OnInspectedCall(operation, operationKind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error recording OnInspectedCall stats.");
            }
            if (blocked)
            {
                throw AikidoException.PathTraversalDetected(operation, __originalMethod.Name);
            }
            return true;
        }
    }
}
