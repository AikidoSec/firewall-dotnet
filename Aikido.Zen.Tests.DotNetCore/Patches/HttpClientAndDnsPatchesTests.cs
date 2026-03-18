using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.DotNetCore.Patches;
using HarmonyLib;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore.Patches
{
    [TestFixture]
    public class HttpClientAndDnsPatchesTests
    {
        private Harmony _harmony;
        private const string HarmonyId = "com.aikido.zen.tests.dotnetcore.httpclient.dns";

        [SetUp]
        public void SetUp()
        {
            _harmony = new Harmony(HarmonyId);
            HttpClientPatches.ApplyPatches(_harmony);
            DnsPatches.ApplyPatches(_harmony);
        }

        [TearDown]
        public void TearDown()
        {
            _harmony.UnpatchAll(HarmonyId);
        }

        [Test]
        public void HttpClient_SendAsync_WithCompletionOption_IsPatched()
        {
            AssertMethodHasPrefix(
                "System.Net.Http",
                "HttpClient",
                "SendAsync",
                "System.Net.Http.HttpRequestMessage",
                "System.Net.Http.HttpCompletionOption",
                "System.Threading.CancellationToken");
        }

        [Test]
        public void HttpClient_SendAsync_IsPatched()
        {
            AssertMethodHasPrefix(
                "System.Net.Http",
                "HttpClient",
                "SendAsync",
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");
        }

        [Test]
        public void HttpClient_Send_IsPatched()
        {
            AssertMethodHasPrefix(
                "System.Net.Http",
                "HttpClient",
                "Send",
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");
        }

        [Test]
        public void Dns_GetHostAddressesAsync_WithCancellationToken_IsPatched()
        {
            AssertMethodHasPostfix(
                "System.Net.NameResolution",
                "System.Net.Dns",
                "GetHostAddressesAsync",
                "System.String",
                "System.Threading.CancellationToken");
        }

        [Test]
        public void Dns_GetHostAddressesAsync_WithAddressFamily_IsPatched()
        {
            AssertMethodHasPostfix(
                "System.Net.NameResolution",
                "System.Net.Dns",
                "GetHostAddressesAsync",
                "System.String",
                "System.Net.Sockets.AddressFamily",
                "System.Threading.CancellationToken");
        }

        [Test]
        public void HttpWebRequest_GetResponse_UsesHttpClientSend()
        {
#pragma warning disable SYSLIB0014
            var request = WebRequest.CreateHttp("http://example.com/test");
#pragma warning restore SYSLIB0014

            var bridgeHarmony = new Harmony("com.aikido.zen.tests.dotnetcore.httpwebrequest.bridge");
            try
            {
                var method = AccessTools.Method(typeof(HttpClient), "Send", new[] { typeof(HttpRequestMessage), typeof(HttpCompletionOption), typeof(CancellationToken) });
                var prefix = typeof(HttpClientAndDnsPatchesTests).GetMethod(nameof(PrefixHttpClientSend), BindingFlags.Static | BindingFlags.NonPublic);
                bridgeHarmony.Patch(method, prefix: new HarmonyMethod(prefix));

                _syncSendCount = 0;
                _lastSyncRequestUri = null;

                using var response = (HttpWebResponse)request.GetResponse();

                Assert.Multiple(() =>
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                    Assert.That(_syncSendCount, Is.EqualTo(1));
                    Assert.That(_lastSyncRequestUri, Is.EqualTo(request.RequestUri));
                });
            }
            finally
            {
                bridgeHarmony.UnpatchAll("com.aikido.zen.tests.dotnetcore.httpwebrequest.bridge");
            }
        }

        private static int _syncSendCount;
        private static Uri? _lastSyncRequestUri;

        private static bool PrefixHttpClientSend(HttpRequestMessage request, ref HttpResponseMessage __result)
        {
            Interlocked.Increment(ref _syncSendCount);
            _lastSyncRequestUri = request?.RequestUri;
            __result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>()),
                RequestMessage = request
            };
            return false;
        }

        private static void AssertMethodHasPrefix(string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            Assert.That(method, Is.Not.Null, $"{typeName}.{methodName} should exist.");

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist.");
            Assert.That(patches.Prefixes.Any(patch => patch.owner == HarmonyId), Is.True, "Our prefix should be applied.");
        }

        private static void AssertMethodHasPostfix(string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            Assert.That(method, Is.Not.Null, $"{typeName}.{methodName} should exist.");

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist.");
            Assert.That(patches.Postfixes.Any(patch => patch.owner == HarmonyId), Is.True, "Our postfix should be applied.");
        }
    }
}
