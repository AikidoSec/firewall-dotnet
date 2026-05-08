using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Aikido.Zen.Core.Patches;
using HarmonyLib;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetCore.Patches
{
    [TestFixture]
    public class OutboundRequestSinkTests
    {
        private const string HarmonyId = "com.aikido.zen.tests.dotnetcore.outbound";
        private Harmony _harmony;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _harmony = new Harmony(HarmonyId);
            Patcher.Patch(_harmony, () => null);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _harmony.UnpatchAll(HarmonyId);
        }

        [Test]
        public void HttpClient_SendAsync_IsPatched()
        {
            var method = typeof(HttpClient).GetMethod(
                "SendAsync",
                new[] { typeof(HttpRequestMessage), typeof(CancellationToken) });

            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Prefixes.Any(p => p.owner == HarmonyId), Is.True);
        }

        [Test]
        public void HttpClient_SendAsync_WithCompletionOption_IsPatched()
        {
            var method = typeof(HttpClient).GetMethod(
                "SendAsync",
                new[] { typeof(HttpRequestMessage), typeof(HttpCompletionOption), typeof(CancellationToken) });

            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Prefixes.Any(p => p.owner == HarmonyId), Is.True);
        }

        [Test]
        public void WebRequest_GetResponseAsync_IsPatched()
        {
            var method = typeof(WebRequest).GetMethod("GetResponseAsync", new System.Type[0]);

            Assert.That(method, Is.Not.Null);
            var patches = Harmony.GetPatchInfo(method);

            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Prefixes.Any(p => p.owner == HarmonyId), Is.True);
        }
    }
}
