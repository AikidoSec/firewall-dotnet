using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class ProcessExecutionPatches
    {
        [SinkPrefix("System.Diagnostics.Process", "System.Diagnostics.Process", "Start")]
        [SinkPrefix("System", "System.Diagnostics.Process", "Start")]
        private static bool ProcessStart(Process __instance, MethodBase __originalMethod)
        {
            return ProcessExecutionSink.OnProcessStart(__instance, __originalMethod, Patcher.GetContext());
        }
    }
}
