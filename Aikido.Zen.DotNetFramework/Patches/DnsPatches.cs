using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetFramework.Patches
{
    internal static class DnsPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(Dns), nameof(Dns.GetHostAddresses), nameof(PostfixGetHostAddresses), typeof(string));
            PatchMethod(harmony, typeof(Dns), nameof(Dns.GetHostAddressesAsync), nameof(PostfixGetHostAddressesAsync), typeof(string));
        }

        private static void PatchMethod(Harmony harmony, System.Type type, string methodName, string postfixMethodName, params System.Type[] parameterTypes)
        {
            var method = AccessTools.Method(type, methodName, parameterTypes);
            if (method == null || method.IsAbstract)
            {
                return;
            }

            var postfix = typeof(DnsPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        }

        private static void PostfixGetHostAddresses(string hostNameOrAddress, IPAddress[] __result)
        {
            InspectResolvedAddresses(hostNameOrAddress, __result);
        }

        private static void PostfixGetHostAddressesAsync(string hostNameOrAddress, ref Task<IPAddress[]> __result)
        {
            __result = InspectResolvedAddressesAsync(hostNameOrAddress, __result);
        }

        private static async Task<IPAddress[]> InspectResolvedAddressesAsync(string hostNameOrAddress, Task<IPAddress[]> resultTask)
        {
            if (resultTask == null)
            {
                return null;
            }

            var resolvedAddresses = await resultTask.ConfigureAwait(false);
            InspectResolvedAddresses(hostNameOrAddress, resolvedAddresses);
            return resolvedAddresses;
        }

        private static void InspectResolvedAddresses(string hostNameOrAddress, IPAddress[] resolvedAddresses)
        {
            DnsPatcher.Inspect(hostNameOrAddress, resolvedAddresses, Zen.GetContext());
        }
    }
}
