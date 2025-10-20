using System.Reflection;

using HarmonyLib;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches.LLMs;
using Aikido.Zen.Core.Models.LLMs.Sinks;

namespace Aikido.Zen.DotNetCore.Patches
{
    /// <summary>
    /// Patches for LLM client operations in .NET Core applications
    /// </summary>
    internal static class LLMPatches
    {
        /// <summary>
        /// Applies patches to various LLM client methods using Harmony.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            foreach(var sink in LLMSinks.Sinks)            
                foreach(var method in sink.Methods)                
                    PatchMethod(harmony, sink.Assembly, method.Type, method.Name, method.Parameters.ToArray());
        }

        /// <summary>
        /// Patches a method using Harmony by dynamically retrieving it via reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        /// <param name="assemblyName">The name of the assembly containing the type.</param>
        /// <param name="typeName">The name of the type containing the method.</param>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="parameterTypeNames">The names of the parameter types for the method.</param>
        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null)
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(LLMPatches).GetMethod(nameof(OnLLMCallCompleted), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        /// <summary>
        /// Callback method executed after the original LLM method is completed.
        /// </summary>
        /// <param name="__args">The arguments passed to the original method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="__instance">The instance of the LLM client being executed.</param>
        /// <param name="__result">The result returned by the original method.</param>
        private static void OnLLMCallCompleted(object[] __args, MethodBase __originalMethod, object __instance, object __result)
        {
            var assembly = __instance?.GetType().Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.RemoveEmptyEntries)[0] ?? string.Empty;
            var resolvedResult = LLMResultHelper.ResolveResult(__result);

            LLMPatcher.OnLLMCallCompleted(__args, __originalMethod, assembly, resolvedResult, Zen.GetContext());
        }
    }
}
