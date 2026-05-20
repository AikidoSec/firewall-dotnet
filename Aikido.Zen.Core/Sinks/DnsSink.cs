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

            return InspectResolvedAddresses(__0, __result, __originalMethod);
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

            __result = InspectResolvedAddressesAsync(__0, __result, __originalMethod);
            return null;
        }

        internal static Exception InspectResolvedAddresses(string hostname, IPAddress[] addresses, MethodBase originalMethod)
        {
            if (!OutboundRequestSink.IsRequestingOutbound() ||
                !SSRFDetector.TryGetPrivateOrLocalIPAddress(addresses, out var privateIPAddress))
            {
                return null;
            }

            try
            {
                Inspector.Inspect(
                    originalMethod,
                    OperationKind,
                    context => SSRFDetector.Detect(hostname, null, privateIPAddress, context));

                return null;
            }
            catch (AikidoException ex)
            {
                return ex;
            }
        }

        private static async Task<IPAddress[]> InspectResolvedAddressesAsync(string hostname, Task<IPAddress[]> addressesTask, MethodBase originalMethod)
        {
            if (addressesTask == null)
            {
                return null;
            }

            var addresses = await addressesTask.ConfigureAwait(false);
            var exception = InspectResolvedAddresses(hostname, addresses, originalMethod);
            if (exception != null)
            {
                throw exception;
            }

            return addresses;
        }
    }
}
