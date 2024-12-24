
using HarmonyLib;
using CorePatcher = Aikido.Zen.Core.Patches.Patcher;

namespace Aikido.Zen.DotNetFramework.Patches
{
    public class Patcher
    {
        public static void Patch()
        {
            CorePatcher.Patch();
            var harmony = new Harmony("aikido.zen.dotnetframework");
            // we need to patch the sqlClient patches outside of the Aikido.Zen.Core package, becasue we need to pass the context, which is different for dotnetcore / dotnetframework
            SqlClientPatches.ApplyPatches(harmony);

        }

        public static void Unpatch()
        {
            CorePatcher.Unpatch();
            if (Harmony.HasAnyPatches("aikido.zen.dotnetframework"))
            {
                var harmony = new Harmony("aikido.zen.dotnetframework");
                harmony.UnpatchAll("aikido.zen.dotnetframework");
            }
        }
    }
}
