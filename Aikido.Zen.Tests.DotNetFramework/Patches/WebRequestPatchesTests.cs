using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.DotNetFramework.Patches;
using HarmonyLib;
using Moq;
using NUnit.Framework;

namespace Aikido.Zen.Tests.DotNetFramework.Patches
{
    [TestFixture]
    [NonParallelizable]
    [Ignore("Applying WebRequest patches inside the net48 vstest host currently crashes the runner.")]
    public class WebRequestPatchesTests
    {
        private const string HarmonyId = "com.aikido.zen.tests.dotnetframework.webrequest";

        [Test]
        public void WebRequest_GetResponse_IsPatched()
        {
            RunWithPatchedWebRequest(delegate
            {
                AssertMethodHasPrefix(typeof(WebRequest).GetMethod("GetResponse", Type.EmptyTypes), "WebRequest.GetResponse()");
            });
        }

        [Test]
        public void WebRequest_GetResponseAsync_IsPatched()
        {
            RunWithPatchedWebRequest(delegate
            {
                AssertMethodHasPrefix(typeof(WebRequest).GetMethod("GetResponseAsync", Type.EmptyTypes), "WebRequest.GetResponseAsync()");
            });
        }

        [Test]
        public void HttpWebRequest_GetResponse_IsPatched()
        {
            RunWithPatchedWebRequest(delegate
            {
                AssertMethodHasPrefix(typeof(HttpWebRequest).GetMethod("GetResponse", Type.EmptyTypes), "HttpWebRequest.GetResponse()");
            });
        }

        [Test]
        public void HttpWebRequest_GetResponseAsync_IsPatched()
        {
            RunWithPatchedWebRequest(delegate
            {
                AssertMethodHasPrefix(typeof(HttpWebRequest).GetMethod("GetResponseAsync", Type.EmptyTypes), "HttpWebRequest.GetResponseAsync()");
            });
        }

        [Test]
        public void HttpWebRequest_GetResponse_WhenDirectPrivateIpFromUserInput_IsBlocked()
        {
            RunWithPatchedWebRequest(delegate
            {
                SetCurrentContext("http://127.0.0.1/admin", "https://app.local/outbound");

                var request = WebRequest.CreateHttp("http://127.0.0.1/admin");
                var exception = Assert.Throws<AikidoException>(delegate
                {
                    request.GetResponse();
                });

                Assert.That(
                    exception.Message,
                    Is.EqualTo("Zen has blocked a server-side request forgery: HttpWebRequest.GetResponse originating from query.url"));
            });
        }

        [Test]
        public async Task HttpWebRequest_GetResponseAsync_WhenDirectPrivateIpFromUserInput_IsBlocked()
        {
            await RunWithPatchedWebRequest(async delegate
            {
                SetCurrentContext("http://127.0.0.1/admin", "https://app.local/outbound");

                var request = WebRequest.CreateHttp("http://127.0.0.1/admin");
                var exception = Assert.ThrowsAsync<AikidoException>(async delegate
                {
                    await request.GetResponseAsync();
                });

                Assert.That(
                    exception.Message,
                    Is.EqualTo("Zen has blocked a server-side request forgery: HttpWebRequest.GetResponseAsync originating from query.url"));
            });
        }

        private static Agent CreateAgent()
        {
            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            var zenApiMock = new Mock<IZenApi>();
            zenApiMock.Setup(z => z.Reporting).Returns(reportingApiMock.Object);
            zenApiMock.Setup(z => z.Runtime).Returns(runtimeApiMock.Object);

            var newInstanceMethod = typeof(Agent).GetMethod("NewInstance", BindingFlags.Static | BindingFlags.NonPublic);
            return (Agent)newInstanceMethod.Invoke(null, new object[] { zenApiMock.Object });
        }

        private static void SetCurrentContext(string userInputUrl, string serverUrl)
        {
            var request = new HttpRequest(string.Empty, serverUrl, string.Empty);
            var response = new HttpResponse(new StringWriter());
            var httpContext = new HttpContext(request, response);

            httpContext.Items["Aikido.Zen.Context"] = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = serverUrl,
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", userInputUrl }
                }
            };

            HttpContext.Current = httpContext;
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

        private static void RunWithPatchedWebRequest(Action action)
        {
            var originalHttpContext = HttpContext.Current;
            var originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            var originalBlock = Environment.GetEnvironmentVariable("AIKIDO_BLOCK");
            var originalTrustProxy = Environment.GetEnvironmentVariable("AIKIDO_TRUST_PROXY");
            var agent = default(Agent);
            var harmony = default(Harmony);

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", string.Empty);
                Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
                Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");

                agent = CreateAgent();
                agent.ClearContext();

                harmony = new Harmony(HarmonyId);
                WebRequestPatches.ApplyPatches(harmony);

                action();
            }
            finally
            {
                harmony?.UnpatchAll(HarmonyId);
                agent?.Dispose();
                HttpContext.Current = originalHttpContext;
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", originalToken);
                Environment.SetEnvironmentVariable("AIKIDO_BLOCK", originalBlock);
                Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", originalTrustProxy);
            }
        }

        private static async Task RunWithPatchedWebRequest(Func<Task> action)
        {
            var originalHttpContext = HttpContext.Current;
            var originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            var originalBlock = Environment.GetEnvironmentVariable("AIKIDO_BLOCK");
            var originalTrustProxy = Environment.GetEnvironmentVariable("AIKIDO_TRUST_PROXY");
            var agent = default(Agent);
            var harmony = default(Harmony);

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", string.Empty);
                Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
                Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");

                agent = CreateAgent();
                agent.ClearContext();

                harmony = new Harmony(HarmonyId);
                WebRequestPatches.ApplyPatches(harmony);

                await action();
            }
            finally
            {
                harmony?.UnpatchAll(HarmonyId);
                agent?.Dispose();
                HttpContext.Current = originalHttpContext;
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", originalToken);
                Environment.SetEnvironmentVariable("AIKIDO_BLOCK", originalBlock);
                Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", originalTrustProxy);
            }
        }
    }
}
