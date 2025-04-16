using System;
using System.Diagnostics;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    public static class IOPatcher
    {
        public static bool OnFileOperation(object[] __args, System.Reflection.MethodBase __originalMethod, Context context)
        {
            var assembly = __originalMethod.DeclaringType.Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.None)[0];
            var stopwatch = Stopwatch.StartNew();
            var sink = assembly;
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;
            try
            {
                attackDetected = PathTraversalHelper.DetectPathTraversal(__args, assembly, context, __originalMethod.Name);
                blocked = attackDetected && !EnvironmentHelper.DryMode;
            }
            catch
            {
                LogHelper.DebugLog(Agent.Logger, "Error during Path Traversal detection.");
            }
            stopwatch.Stop();
            try
            {
                Agent.Instance?.Context?.OnInspectedCall(sink, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.DebugLog(Agent.Logger, "Error recording OnInspectedCall stats.");
            }
            if (blocked)
            {
                throw AikidoException.PathTraversalDetected(assembly, __originalMethod.Name);
            }
            return true;
        }
    }
}
