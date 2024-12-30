
using HarmonyLib;

namespace Aikido.Zen.Core.Patches {
    public class Patcher {
        public static void Patch() {
           var harmony = new Harmony("aikido.zen");
           // patch the web request class
           WebRequestPatches.ApplyPatches(harmony);
           // patch the file httpclient class
           HttpClientPatches.ApplyPatches(harmony);
        }

        public static void Unpatch() {
            if (Harmony.HasAnyPatches("aikido.zen")) {
                var harmony = new Harmony("aikido.zen");
                harmony.UnpatchAll("aikido.zen");
            }
        }
    }
}
