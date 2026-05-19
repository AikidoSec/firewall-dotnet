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

        protected SinkTargetAttribute(HarmonyPatchType patchType)
        {
            PatchType = patchType;
            TargetParameterTypeNames = Array.Empty<string>();
        }

        /// <summary>
        /// Use this constructor for framework/runtime types that Core already references.
        /// The Type is immediately converted to assembly/type names, so the patcher can use the same reflection path
        /// while still letting the runtime choose the correct implementation assembly for .NET Core or .NET Framework.
        /// This avoids fallback assembly lists such as System.Private.CoreLib/mscorlib or System.Data.Common/System.Data.
        /// Keep the assembly-name constructor for optional third-party libraries so they do not become package dependencies.
        /// </summary>
        protected SinkTargetAttribute(
            HarmonyPatchType patchType,
            Type targetType,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            PatchType = patchType;
            AssemblyName = targetType?.Assembly.GetName().Name;
            TargetTypeName = targetType?.FullName;
            TargetMethodName = targetMethodName;
            TargetParameterTypeNames = targetParameterTypeNames ?? Array.Empty<string>();
        }

        public HarmonyPatchType PatchType { get; }
        public string AssemblyName { get; }
        public string TargetTypeName { get; }
        public string TargetMethodName { get; }
        public string[] TargetParameterTypeNames { get; }
        public bool HasTarget => !string.IsNullOrWhiteSpace(AssemblyName) &&
                                 !string.IsNullOrWhiteSpace(TargetTypeName) &&
                                 !string.IsNullOrWhiteSpace(TargetMethodName);
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

        /// <summary>
        /// Targets framework/runtime types that are already referenced by Core, while allowing the runtime
        /// to choose the correct implementation assembly for .NET Core or .NET Framework.
        /// </summary>
        public SinkPrefixAttribute(
            Type targetType,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : base(HarmonyPatchType.Prefix, targetType, targetMethodName, targetParameterTypeNames)
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

        /// <summary>
        /// Targets framework/runtime types that are already referenced by Core, while allowing the runtime
        /// to choose the correct implementation assembly for .NET Core or .NET Framework.
        /// </summary>
        public SinkPostfixAttribute(
            Type targetType,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : base(HarmonyPatchType.Postfix, targetType, targetMethodName, targetParameterTypeNames)
        {
        }
    }

    internal sealed class SinkFinalizerAttribute : SinkTargetAttribute
    {
        public SinkFinalizerAttribute()
            : base(HarmonyPatchType.Finalizer)
        {
        }

        public SinkFinalizerAttribute(
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : base(HarmonyPatchType.Finalizer, assemblyName, targetTypeName, targetMethodName, targetParameterTypeNames)
        {
        }

        public SinkFinalizerAttribute(
            Type targetType,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : base(HarmonyPatchType.Finalizer, targetType, targetMethodName, targetParameterTypeNames)
        {
        }
    }
}
