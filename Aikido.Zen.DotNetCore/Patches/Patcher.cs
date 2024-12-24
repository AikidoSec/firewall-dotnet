
using HarmonyLib;
using CorePatcher = Aikido.Zen.Core.Patches.Patcher;

namespace Aikido.Zen.DotNetCore.Patches
{
    public class Patcher
    {
        public static void Patch()
        {
            CorePatcher.Patch();
            var harmony = new Harmony("aikido.zen.dotnetcore");

            // we need to patch the sqlClient patches outside of the Aikido.Zen.Core package, becasue we need to pass the context, which is different for dotnetcore / dotnetframework
            SqlClientPatches.ApplyPatches(harmony);

        }

        public static void Unpatch()
        {
            CorePatcher.Unpatch();
            if (Harmony.HasAnyPatches("aikido.zen.dotnetcore"))
            {
                var harmony = new Harmony("aikido.zen.dotnetcore");
                harmony.UnpatchAll("aikido.zen.dotnetcore");
            }
        }
    }
}
