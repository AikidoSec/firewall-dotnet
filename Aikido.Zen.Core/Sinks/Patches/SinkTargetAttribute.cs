using System;
using HarmonyLib;

namespace Aikido.Zen.Core.Sinks
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal abstract class SinkTargetAttribute : Attribute
    {
        protected SinkTargetAttribute(
            HarmonyPatchType patchType,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            PatchType = patchType;
            AssemblyName = assemblyName;
            TargetTypeName = targetTypeName;
            TargetMethodName = targetMethodName;
            TargetParameterTypeNames = targetParameterTypeNames ?? Array.Empty<string>();
        }

        public HarmonyPatchType PatchType { get; }
        public string AssemblyName { get; }
        public string TargetTypeName { get; }
        public string TargetMethodName { get; }
        public string[] TargetParameterTypeNames { get; }
    }

    internal sealed class SinkPrefixAttribute : SinkTargetAttribute
    {
        public SinkPrefixAttribute(
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : base(HarmonyPatchType.Prefix, assemblyName, targetTypeName, targetMethodName, targetParameterTypeNames)
        {
        }
    }

    internal sealed class SinkPostfixAttribute : SinkTargetAttribute
    {
        public SinkPostfixAttribute(
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : base(HarmonyPatchType.Postfix, assemblyName, targetTypeName, targetMethodName, targetParameterTypeNames)
        {
        }
    }
}
