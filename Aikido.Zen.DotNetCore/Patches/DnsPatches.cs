using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class DnsPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(
                harmony,
                "System.Net.NameResolution",
                "System.Net.Dns",
                "GetHostAddresses",
                nameof(PostfixGetHostAddresses),
                "System.String",
                "System.Net.Sockets.AddressFamily");

            PatchMethod(
                harmony,
                "System.Net.NameResolution",
                "System.Net.Dns",
                "GetHostAddressesAsync",
                nameof(PostfixGetHostAddressesAsync),
                "System.String");

            PatchMethod(
                harmony,
                "System.Net.NameResolution",
                "System.Net.Dns",
                "GetHostAddressesAsync",
                nameof(PostfixGetHostAddressesAsync),
                "System.String",
                "System.Threading.CancellationToken");

            PatchMethod(
                harmony,
                "System.Net.NameResolution",
                "System.Net.Dns",
                "GetHostAddressesAsync",
                nameof(PostfixGetHostAddressesAsync),
                "System.String",
                "System.Net.Sockets.AddressFamily",
                "System.Threading.CancellationToken");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, string postfixMethodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
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
