using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Sinks
{
    internal static class DnsSink
    {
        private const string OperationKind = "dns_op";

        [SinkFinalizer(typeof(Dns), "GetHostAddresses", "System.String")]
        [SinkFinalizer(typeof(Dns), "GetHostAddresses", "System.String", "System.Net.Sockets.AddressFamily")]
        internal static Exception OnHostAddressesResolved(string __0, IPAddress[] __result, Exception __exception, MethodBase __originalMethod)
        {
            if (__exception != null)
            {
                return __exception;
            }

            return InspectResolvedAddresses(__result, __originalMethod);
        }

        [SinkFinalizer(typeof(Dns), "GetHostAddressesAsync", "System.String")]
        [SinkFinalizer(typeof(Dns), "GetHostAddressesAsync", "System.String", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(Dns), "GetHostAddressesAsync", "System.String", "System.Net.Sockets.AddressFamily", "System.Threading.CancellationToken")]
        internal static Exception OnHostAddressesResolvedAsync(string __0, ref Task<IPAddress[]> __result, Exception __exception, MethodBase __originalMethod)
        {
            if (__exception != null)
            {
                return __exception;
            }

            __result = InspectResolvedAddressesAsync(__result, __originalMethod);
            return null;
        }

        internal static Exception InspectResolvedAddresses(IPAddress[] addresses, MethodBase originalMethod)
        {
            if (!OutboundRequestSink.TryGetCurrentRequestUri(out var targetUri))
            {
                return null;
            }

            try
            {
                Inspector.Inspect(
                    originalMethod,
                    OperationKind,
                    // DNS can happen after a redirect, so match SSRF against the original outbound URL.
                    context => SSRFDetector.Detect(targetUri, addresses, context, out _));

                return null;
            }
            catch (AikidoException ex)
            {
                return ex;
            }
        }

        private static async Task<IPAddress[]> InspectResolvedAddressesAsync(Task<IPAddress[]> addressesTask, MethodBase originalMethod)
        {
            if (addressesTask == null)
            {
                return null;
            }

            var addresses = await addressesTask.ConfigureAwait(false);
            var exception = InspectResolvedAddresses(addresses, originalMethod);
            if (exception != null)
            {
                throw exception;
            }

            return addresses;
        }
    }
}
