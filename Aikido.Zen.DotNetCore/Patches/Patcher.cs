
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
