using HarmonyLib;

using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.DotNetCore.Patches
{
    public class Patcher
    {
        private const string HarmonyId = "aikido.zen.dotnetcore";

        public static void Patch()
        {
            try
            {
                var harmony = new Harmony(HarmonyId);

                // we need to patch the sqlClient patches outside of the Aikido.Zen.Core package, because we need to pass the context, which is different for dotnetcore / dotnetframework
                SqlClientPatches.ApplyPatches(harmony);

                // we need to patch the io patches outside of the Aikido.Zen.Core package, because we need to pass the context, which is different for dotnetcore / dotnetframework
                IOPatches.ApplyPatches(harmony);

                // Patch process execution methods to prevent shell injection
                ProcessPatches.ApplyPatches(harmony);

                // Patch outbound HTTP methods outside of the core package so SSRF inspection can use Zen.GetContext().
                HttpClientPatches.ApplyPatches(harmony);
                WebRequestPatches.ApplyPatches(harmony);

                // Patch LLM client methods to monitor LLM API calls
                LLMPatches.ApplyPatches(harmony);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error patching: {ex.Message}");
            }
        }

        public static void Unpatch()
        {
            if (Harmony.HasAnyPatches(HarmonyId))
            {
                var harmony = new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
        }
    }
}
