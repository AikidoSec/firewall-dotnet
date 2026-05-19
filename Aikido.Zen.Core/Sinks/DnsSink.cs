using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class DnsSink
    {
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
                !TryGetPrivateOrLocalIPAddress(addresses, out var privateIPAddress))
            {
                return null;
            }

            var context = Patcher.GetContext();
            var result = OutboundRequestSink.DetectResolvedSSRF(hostname, privateIPAddress, context);
            if (!result.AttackKind.HasValue)
            {
                return null;
            }

            var operation = ReflectionHelper.GetMethodOperation(originalMethod);
            var module = ReflectionHelper.GetMethodModule(originalMethod);
            var blocked = !EnvironmentHelper.DryMode;

            Agent.Instance.SendAttackEvent(
                kind: result.AttackKind.Value,
                source: result.Source,
                payload: result.Payload,
                operation: operation,
                context: context,
                module: module,
                metadata: result.Metadata,
                blocked: blocked,
                paths: result.Paths);

            if (context != null)
            {
                context.AttackDetected = true;
            }

            return blocked
                ? AikidoException.Blocked(result.AttackKind.Value, Inspector.GetBlockedOperation(operation, result))
                : null;
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

        private static bool TryGetPrivateOrLocalIPAddress(IPAddress[] addresses, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (addresses == null)
            {
                return false;
            }

            foreach (var address in addresses)
            {
                var candidate = address?.ToString();
                if (!string.IsNullOrWhiteSpace(candidate) && IPHelper.IsPrivateOrLocalIp(candidate))
                {
                    privateIPAddress = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
