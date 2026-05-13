using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class ProcessExecutionPatches
    {
        [SinkPrefix(typeof(Process), "Start")]
        internal static bool OnProcessStart(Process __instance, MethodBase __originalMethod)
        {
            return ProcessExecutionSink.OnProcessStart(__instance, __originalMethod, Patcher.GetContext());
        }
    }
}
