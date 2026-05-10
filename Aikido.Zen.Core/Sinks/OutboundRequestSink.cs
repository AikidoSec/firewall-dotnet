using System;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly Uri ParsedAikidoUrl = new Uri(EnvironmentHelper.AikidoUrl);
        private static readonly Uri ParsedAikidoRealtimeUrl = new Uri(EnvironmentHelper.AikidoRealtimeUrl);

        internal static bool OnRequest(Uri targetUri, MethodBase originalMethod, Context context)
        {
            if (targetUri == null)
            {
                return true;
            }

            var operation = ReflectionHelper.GetMethodOperation(originalMethod);
            var withoutContext = context == null;
            var blocked = false;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var hostname = targetUri.Host;
                var port = UriHelper.GetPort(targetUri);
                Agent.Instance.CaptureOutboundRequest(hostname, port);

                if (Context.IsBypassed(context))
                {
                    return true;
                }

                if (EnvironmentHelper.DryMode || IsAikidoInternalTarget(targetUri))
                {
                    return true;
                }

                if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
                {
                    blocked = true;
                    throw AikidoException.OutboundConnectionBlocked(hostname);
                }

                if (Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    return true;
                }

                return true;
            }
            finally
            {
                stopwatch.Stop();
                Agent.Instance?.Context?.OnInspectedCall(
                    operation,
                    OperationKind,
                    stopwatch.Elapsed.TotalMilliseconds,
                    false,
                    blocked,
                    withoutContext);
            }
        }

        private static bool IsAikidoInternalTarget(Uri targetUri)
        {
            if (targetUri == null)
            {
                return false;
            }

            var targetPort = UriHelper.GetPort(targetUri);
            var targetHost = targetUri.Host;

            if (string.Equals(targetHost, ParsedAikidoUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == UriHelper.GetPort(ParsedAikidoUrl))
            {
                return true;
            }

            if (string.Equals(targetHost, ParsedAikidoRealtimeUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == UriHelper.GetPort(ParsedAikidoRealtimeUrl))
            {
                return true;
            }

            return false;
        }

    }
}
