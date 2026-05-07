using System;

namespace Aikido.Zen.Core.Patches
{
    internal enum PatchKind
    {
        Prefix,
        Postfix
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class PatchTargetAttribute : Attribute
    {
        internal PatchTargetAttribute(
            PatchKind kind,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
            : this(
                kind,
                string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName },
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames)
        {
        }

        internal PatchTargetAttribute(
            PatchKind kind,
            string[] assemblyNames,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            Kind = kind;
            AssemblyNames = assemblyNames ?? Array.Empty<string>();
            TargetTypeName = targetTypeName;
            TargetMethodName = targetMethodName;
            TargetParameterTypeNames = targetParameterTypeNames ?? Array.Empty<string>();
        }

        internal PatchKind Kind { get; }
        internal string[] AssemblyNames { get; }
        internal string TargetTypeName { get; }
        internal string TargetMethodName { get; }
        internal string[] TargetParameterTypeNames { get; }
        public int[] PathArgumentIndexes { get; set; } = Array.Empty<int>();
    }
}
