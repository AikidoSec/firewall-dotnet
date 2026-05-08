using System;

namespace Aikido.Zen.Core.Sinks
{
    internal enum PatchKind
    {
        Prefix,
        Postfix
    }

    internal enum SinkKind
    {
        IOPath,
        IOTwoPaths,
        LLM,
        OutboundRequest,
        ProcessExecution,
        SqlClient
    }

    internal sealed class PatchDefinition
    {
        private PatchDefinition(
            PatchKind kind,
            SinkKind sink,
            string[] assemblyNames,
            string targetTypeName,
            string targetMethodName,
            string[] targetParameterTypeNames)
        {
            Kind = kind;
            Sink = sink;
            AssemblyNames = assemblyNames ?? Array.Empty<string>();
            TargetTypeName = targetTypeName;
            TargetMethodName = targetMethodName;
            TargetParameterTypeNames = targetParameterTypeNames ?? Array.Empty<string>();
        }

        internal PatchKind Kind { get; private set; }
        internal SinkKind Sink { get; private set; }
        internal string[] AssemblyNames { get; private set; }
        internal string TargetTypeName { get; private set; }
        internal string TargetMethodName { get; private set; }
        internal string[] TargetParameterTypeNames { get; private set; }

        internal static PatchDefinition Prefix(
            SinkKind sink,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            return Prefix(
                sink,
                string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName },
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames);
        }

        internal static PatchDefinition Prefix(
            SinkKind sink,
            string[] assemblyNames,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            return new PatchDefinition(
                PatchKind.Prefix,
                sink,
                assemblyNames,
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames);
        }

        internal static PatchDefinition Postfix(
            SinkKind sink,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            return new PatchDefinition(
                PatchKind.Postfix,
                sink,
                string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName },
                targetTypeName,
                targetMethodName,
                targetParameterTypeNames);
        }

    }
}
