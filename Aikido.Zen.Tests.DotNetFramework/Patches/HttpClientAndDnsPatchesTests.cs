using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.DotNetFramework.Patches;
using HarmonyLib;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetFramework.Patches
{
    [TestFixture]
    [NonParallelizable]
    public class HttpClientAndDnsPatchesTests
    {
        private Harmony _harmony;
        private const string HarmonyId = "com.aikido.zen.tests.dotnetframework.httpclient.dns";

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
                ReflectionHelper.GetMethodFromAssembly(
                    "System.Net.Http",
                    "HttpClient",
                    "SendAsync",
                    "System.Net.Http.HttpRequestMessage",
                    "System.Net.Http.HttpCompletionOption",
                    "System.Threading.CancellationToken"),
                "HttpClient.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)");
        }

        [Test]
        public void HttpClient_SendAsync_IsPatched()
        {
            AssertMethodHasPrefix(
                ReflectionHelper.GetMethodFromAssembly(
                    "System.Net.Http",
                    "HttpClient",
                    "SendAsync",
                    "System.Net.Http.HttpRequestMessage",
                    "System.Threading.CancellationToken"),
                "HttpClient.SendAsync(HttpRequestMessage, CancellationToken)");
        }

        [Test]
        public void HttpClient_Send_WhenAvailable_IsPatched()
        {
            var method = ReflectionHelper.GetMethodFromAssembly(
                "System.Net.Http",
                "HttpClient",
                "Send",
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");

            if (method == null)
            {
                Assert.Pass("HttpClient.Send is not available on this .NET Framework surface.");
                return;
            }

            AssertMethodHasPrefix(
                method,
                "HttpClient.Send(HttpRequestMessage, CancellationToken)");
        }

        [Test]
        public void Dns_GetHostAddresses_IsPatched()
        {
            AssertMethodHasPostfix(
                typeof(Dns).GetMethod("GetHostAddresses", new[] { typeof(string) }),
                "Dns.GetHostAddresses(string)");
        }

        [Test]
        public void Dns_GetHostAddressesAsync_IsPatched()
        {
            AssertMethodHasPostfix(
                typeof(Dns).GetMethod("GetHostAddressesAsync", new[] { typeof(string) }),
                "Dns.GetHostAddressesAsync(string)");
        }

        private static void AssertMethodHasPrefix(MethodInfo method, string description)
        {
            Assert.That(method, Is.Not.Null, description + " should exist.");

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist.");
            Assert.That(
                patches.Prefixes.Any(patch => patch.owner == HarmonyId),
                Is.True,
                "Our prefix should be applied.");
        }

        private static void AssertMethodHasPostfix(MethodInfo method, string description)
        {
            Assert.That(method, Is.Not.Null, description + " should exist.");

            var patches = Harmony.GetPatchInfo(method);
            Assert.That(patches, Is.Not.Null, "Harmony patches should exist.");
            Assert.That(
                patches.Postfixes.Any(patch => patch.owner == HarmonyId),
                Is.True,
                "Our postfix should be applied.");
        }
    }
}
