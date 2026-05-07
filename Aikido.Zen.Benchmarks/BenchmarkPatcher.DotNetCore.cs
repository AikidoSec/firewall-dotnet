using Aikido.Zen.DotNetCore.Patches;

namespace Aikido.Zen.Benchmarks
{
    internal static class BenchmarkPatcher
    {
        internal static void Patch()
        {
            Patcher.Patch();
        }

        internal static void Unpatch()
        {
            Patcher.Unpatch();
        }
    }
}
