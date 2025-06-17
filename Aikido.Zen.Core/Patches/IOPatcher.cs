using System;
using System.Collections.Generic;
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
                var arguments = ArgumentHelper.BuildArgumentDictionary(__args, __originalMethod);
                var paths = new List<string>();

                foreach (var arg in arguments)
                {
                    // Heuristic to identify path arguments. Most are named "path", some are "sourceFileName" or "destFileName"
                    var paramName = arg.Key.ToLowerInvariant();
                    if (paramName != "path" && paramName != "paths" && !paramName.EndsWith("filename"))
                        continue;

                    switch (arg.Value)
                    {
                        case string p when !string.IsNullOrEmpty(p):
                            paths.Add(p);
                            break;
                        case string[] ps:
                            paths.AddRange(ps);
                            break;
                    }
                }

                if (paths.Count > 0)
                {
                    attackDetected = PathTraversalHelper.DetectPathTraversal(paths.ToArray(), assemblyName, context, operation);
                }

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
