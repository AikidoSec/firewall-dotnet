
using System.Runtime.InteropServices;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    public class Patcher
    {
        public static void Patch()
        {
            var harmony = new Harmony("aikido.zen");
            // patch the web request class
            WebRequestPatches.ApplyPatches(harmony);
            // patch the file httpclient class
            HttpClientPatches.ApplyPatches(harmony);
        }

        public static void Unpatch()
        {
            if (Harmony.HasAnyPatches("aikido.zen"))
            {
                var harmony = new Harmony("aikido.zen");
                harmony.UnpatchAll("aikido.zen");
            }
        }

        public static bool CanPatch(out string message)
        {
            // if we are running on arm64, we can't patch

            // for m1 chips, we have a more specific message
            if (
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm)
            )
            {
                message = "Apple silicon is currently not supported.";
                return false;
            }
            // for linux or windows arm64, we have a more specific message
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm)
            {
                message = "ARM/ARM64 is currently not supported.";
                return false;
            }
            message = "";
            return true;
        }
    }
}
