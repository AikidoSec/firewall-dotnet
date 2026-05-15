using System;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    internal sealed class InspectionResult
    {
        private InspectionResult(
            AttackKind? attackKind,
            Source? source,
            string payload,
            IDictionary<string, string> metadata,
            string[] paths,
            bool skipStats)
        {
            AttackKind = attackKind;
            Source = source;
            Payload = payload;
            Metadata = metadata;
            Paths = paths ?? Array.Empty<string>();
            SkipStats = skipStats;
        }

        internal AttackKind? AttackKind { get; }
        internal Source? Source { get; }
        internal string Payload { get; }
        internal IDictionary<string, string> Metadata { get; }
        internal string[] Paths { get; }
        internal bool SkipStats { get; }

        internal static InspectionResult Allow(bool skipStats = false)
        {
            return new InspectionResult(null, null, null, null, null, skipStats);
        }

        internal static InspectionResult Block(
            AttackKind kind,
            Source? source = null,
            string payload = null,
            IDictionary<string, string> metadata = null,
            string[] paths = null,
            bool skipStats = false)
        {
            return new InspectionResult(kind, source, payload, metadata, paths, skipStats);
        }
    }
}
