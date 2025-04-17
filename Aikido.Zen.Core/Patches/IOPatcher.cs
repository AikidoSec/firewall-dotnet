using System;
using System.Diagnostics;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    public static class IOPatcher
    {
        private const string kind = "fs_op";
        public static bool OnFileOperation(object[] __args, System.Reflection.MethodBase __originalMethod, Context context)
        {
            var operation = __originalMethod.DeclaringType.FullName;
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;
            try
            {
                attackDetected = PathTraversalHelper.DetectPathTraversal(__args, operation, context, __originalMethod.Name);
                blocked = attackDetected && !EnvironmentHelper.DryMode;
            }
            catch
            {
                LogHelper.DebugLog(Agent.Logger, "Error during Path Traversal detection.");
            }
            stopwatch.Stop();
            try
            {
                Agent.Instance?.Context?.OnInspectedCall(operation, kind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.DebugLog(Agent.Logger, "Error recording OnInspectedCall stats.");
            }
            if (blocked)
            {
                throw AikidoException.PathTraversalDetected(operation, __originalMethod.Name);
            }
            return true;
        }
    }
}
