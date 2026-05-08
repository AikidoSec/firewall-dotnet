using System;
using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal enum PatchKind
    {
        Prefix,
        Postfix
    }

    internal sealed class PatchDefinition
    {
        private PatchDefinition(
            PatchKind kind,
            MethodInfo patchMethod,
            string[] assemblyNames,
            string targetTypeName,
            string targetMethodName,
            string[] targetParameterTypeNames)
        {
            Kind = kind;
            PatchMethod = patchMethod ?? throw new ArgumentNullException(nameof(patchMethod));
            AssemblyNames = assemblyNames ?? Array.Empty<string>();
            TargetTypeName = targetTypeName;
            TargetMethodName = targetMethodName;
            TargetParameterTypeNames = targetParameterTypeNames ?? Array.Empty<string>();
        }

        internal PatchKind Kind { get; private set; }
        internal MethodInfo PatchMethod { get; private set; }
        internal string[] AssemblyNames { get; private set; }
        internal string TargetTypeName { get; private set; }
        internal string TargetMethodName { get; private set; }
        internal string[] TargetParameterTypeNames { get; private set; }

        internal static PatchDefinition Prefix(
            MethodInfo patchMethod,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            return Prefix(
                patchMethod,
                string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName },
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames);
        }

        internal static PatchDefinition Prefix(
            MethodInfo patchMethod,
            string[] assemblyNames,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            return new PatchDefinition(
                PatchKind.Prefix,
                patchMethod,
                assemblyNames,
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames);
        }

        internal static PatchDefinition Postfix(
            MethodInfo patchMethod,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            return new PatchDefinition(
                PatchKind.Postfix,
                patchMethod,
                string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName },
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames);
        }

    }
}
