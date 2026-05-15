using System;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    internal sealed class InspectionResult
    {
        private InspectionResult(
            bool attackDetected,
            bool blocked,
            bool continueOriginal,
            Func<string, Exception> exceptionFactory,
            bool recordStats,
            AttackKind attackKind,
            Source source,
            string payload,
            IDictionary<string, object> metadata,
            string[] paths)
        {
            AttackDetected = attackDetected;
            Blocked = blocked;
            ContinueOriginal = continueOriginal;
            ExceptionFactory = exceptionFactory;
            RecordStats = recordStats;
            AttackKind = attackKind;
            Source = source;
            Payload = payload;
            Metadata = metadata;
            Paths = paths;
        }

        internal bool AttackDetected { get; }
        internal bool Blocked { get; }
        internal bool ContinueOriginal { get; }
        internal bool RecordStats { get; }
        internal AttackKind AttackKind { get; }
        internal Source Source { get; }
        internal string Payload { get; }
        internal IDictionary<string, object> Metadata { get; }
        internal string[] Paths { get; }
        internal Func<string, Exception> ExceptionFactory { get; }

        internal static InspectionResult Continue()
        {
            return new InspectionResult(false, false, true, null, true, default(AttackKind), default(Source), null, null, null);
        }

        internal static InspectionResult Skip()
        {
            return new InspectionResult(false, false, true, null, false, default(AttackKind), default(Source), null, null, null);
        }

        internal static InspectionResult Attack(
            AttackKind kind,
            Source source,
            string payload,
            IDictionary<string, object> metadata,
            string[] paths,
            bool blocked,
            Exception exceptionToThrow)
        {
            return new InspectionResult(true, blocked, true, blocked ? _ => exceptionToThrow : (Func<string, Exception>)null, true, kind, source, payload, metadata, paths);
        }

        internal static InspectionResult Attack(
            AttackKind kind,
            Source source,
            string payload,
            IDictionary<string, object> metadata,
            string[] paths,
            bool blocked,
            Func<string, Exception> exceptionFactory)
        {
            return new InspectionResult(true, blocked, true, blocked ? exceptionFactory : null, true, kind, source, payload, metadata, paths);
        }

        internal static InspectionResult Block(Exception exceptionToThrow)
        {
            return new InspectionResult(false, true, true, _ => exceptionToThrow, true, default(AttackKind), default(Source), null, null, null);
        }
    }
}
