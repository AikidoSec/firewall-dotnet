
using HarmonyLib;

namespace Aikido.Zen.Core.Patches {
    public class Patcher {
        public static void Patch() {
           var harmony = new Harmony("aikido.zen");
           var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
           harmony.PatchAll(executingAssembly);
        }

        public static void Unpatch() {
            if (Harmony.HasAnyPatches("aikido.zen")) {
                var harmony = new Harmony("aikido.zen");
                harmony.UnpatchAll("aikido.zen");
            }
        }
    }
}
