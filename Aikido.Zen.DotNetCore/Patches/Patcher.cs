using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using CorePatcher = Aikido.Zen.Core.Patches.Patcher;

namespace Aikido.Zen.DotNetCore.Patches
{
    public class Patcher
    {
        public static void Patch()
        {
            if (!CorePatcher.CanPatch(out var message))
            {
                LogHelper.DebugLog(Agent.Logger, message);
                return;
            }
            try
            {
                CorePatcher.Patch();
                var harmony = new Harmony("aikido.zen.dotnetcore");

                // we need to patch the sqlClient patches outside of the Aikido.Zen.Core package, because we need to pass the context, which is different for dotnetcore / dotnetframework
                SqlClientPatches.ApplyPatches(harmony);

                // we need to patch the io patches outside of the Aikido.Zen.Core package, because we need to pass the context, which is different for dotnetcore / dotnetframework
                IOPatches.ApplyPatches(harmony);

                // Patch process execution methods to prevent shell injection
                ProcessPatches.ApplyPatches(harmony);
            }
            catch (Exception ex)
            {
                LogHelper.DebugLog(Agent.Logger, $"AIKIDO: Error patching: {ex.Message}");
            }
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
