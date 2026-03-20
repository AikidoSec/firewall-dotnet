using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
    public class HttpClientRedirectTests
    {
        private const string HarmonyId = "com.aikido.zen.tests.dotnetframework.httpclient.redirects";
        private const string DefaultServerUrl = "https://app.local/outbound";
        private const string ExpectedSsrfMessage = "Zen has blocked a server-side request forgery: HttpClient.SendAsync originating from query.url";

        // Python parity: keep only URL forms that .NET Uri can represent faithfully on the HttpClient path.
        private static readonly object[] SsrfUrls =
        {
            new object[] { "http://ssrf-redirects.testssandbox.com/ssrf-test" },
            new object[] { "http://ssrf-redirects.testssandbox.com/ssrf-test-twice" },
            new object[] { "http://ssrf-redirects.testssandbox.com/ssrf-test-domain" },
            new object[] { "http://ssrf-redirects.testssandbox.com/ssrf-test-domain-twice" },
            new object[] { "http://ssrf-rÃ©directs.testssandbox.com/ssrf-test" },
            new object[] { "http://xn--ssrf-rdirects-ghb.testssandbox.com/ssrf-test" },
            new object[] { "http://ssrf-rÃ©directs.testssandbox.com/ssrf-test-domain-twice" },
            new object[] { "http://firewallssrfredirects-env-2.eba-7ifve22q.eu-north-1.elasticbeanstalk.com/ssrf-test" },
            new object[] { "http://firewallssrfredirects-env-2.eba-7ifve22q.eu-north-1.elasticbeanstalk.com/ssrf-test-domain-twice" },
            new object[] { "http://[::1]:8081" },
            new object[] { "http://[::1]:8081/" },
            new object[] { "http://[::1]:8081/test" },
            new object[] { "http://[0000:0000:0000:0000:0000:0000:0000:0001]:8081" },
            new object[] { "http://[0000:0000:0000:0000:0000:0000:0000:0001]:8081/" },
            new object[] { "http://[0000:0000:0000:0000:0000:0000:0000:0001]:8081/test" },
            new object[] { "http://2130706433:8081" },
            new object[] { "http://0x7f000001:8081/" },
            new object[] { "http://0x7f.0x0.0x0.0x1:8081/" },
            new object[] { "http://[::ffff:127.0.0.1]:8081" },
            new object[] { "http://169.254.169.254/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://[fd00:0ec2:0000:0000:0000:0000:0000:0254]:7000/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://0xa9.0xfe.0xa9.0xfe/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://0xA9FEA9FE/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://2852039166/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://[::ffff:169.254.169.254]:8081/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://[fd00:ec2::254]/latest/meta-data/iam/security-credentials/" },
            new object[] { "http://169.254.169.254:4000" },
            new object[] { "http://localhost:8081" },
            new object[] { "http://localhost:8081/" },
            new object[] { "http://localhost:8081/test" },
            new object[] { "http://localhost:8081/test/2" },
            new object[] { "http://localHost:8081" }
        };

        private static readonly object[] VariantSsrfCases =
        {
            new object[] { "http://localhost:8081/test/2", "http://localhost:8081/chicken/3", DefaultServerUrl },
            new object[] { "http://localHost:8081", "http://LOCALHOST:8081", DefaultServerUrl },
            new object[] { "http://localHost:8081", "http://Localhost:8081/", DefaultServerUrl },
            new object[] { "http://localHost:8081", "http://localHost:8081/test", DefaultServerUrl },
            new object[] { "http://localhost:5000/test/2", "http://localhost:5000/test/2", "http://localhost:4999/test/2" }
        };

        private static readonly object[] NonSsrfCases =
        {
            new object[] { "http://firewallssrfredirects-env-2.eba-7ifve22q.eu-north-1.elasticbeanstalk.com/ssrf-test-domain-twice", "http://ssrf-redirects.testssandbox.com/ssrf-test-domain-twice", DefaultServerUrl },
            new object[] { "http://localhost:8080", "http://localhost:8080", "http://localhost:8080/outbound" },
            new object[] { "http://localhost:8081/", "http://localhost:5002/test", DefaultServerUrl },
            new object[] { "http://localhost:5000/test/1", "http://localhost:5000/test/1", "http://localhost:5000/outbound" },
            new object[] { "https://localhost/test/3", "https://localhost/test/3", "http://localhost:80/outbound" },
            new object[] { "http://localhost/test/4", "https://localhost/test/4", "http://localhost:443/outbound" }
        };

        [TestCaseSource(nameof(SsrfUrls))]
        public async Task HttpClient_WhenSsrfUrl_IsBlocked(string url)
        {
            await RunWithPatchedHttpClient(async delegate
            {
                await AssertSsrfBlockedAsync(url, url, DefaultServerUrl);
            });
        }

        [TestCaseSource(nameof(VariantSsrfCases))]
        public async Task HttpClient_WhenVariantMatchesSsrfBehavior_IsBlocked(string userInputUrl, string requestUrl, string serverUrl)
        {
            await RunWithPatchedHttpClient(async delegate
            {
                await AssertSsrfBlockedAsync(userInputUrl, requestUrl, serverUrl);
            });
        }

        [TestCaseSource(nameof(NonSsrfCases))]
        public async Task HttpClient_WhenCaseShouldNotRaiseSsrf_DoesNotThrowAikidoException(string userInputUrl, string requestUrl, string serverUrl)
        {
            await RunWithPatchedHttpClient(async delegate
            {
                await AssertNetworkFailureAsync(userInputUrl, requestUrl, serverUrl);
            });
        }

        private static Agent CreateAgent()
        {
            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
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
            return (Agent)newInstanceMethod.Invoke(null, new object[] { zenApiMock.Object, 20000 });
        }

        private static HttpContext CreateHttpContext(string serverUrl)
        {
            var request = new HttpRequest(string.Empty, serverUrl, string.Empty);
            var response = new HttpResponse(new StringWriter());
            return new HttpContext(request, response);
        }

        private void SetCurrentContext(Uri initialUri)
        {
            SetCurrentContext(initialUri, new Uri(DefaultServerUrl));
        }

        private void SetCurrentContext(Uri userInputUri, Uri serverUri)
        {
            var httpContext = CreateHttpContext(serverUri.ToString());
            httpContext.Items["Aikido.Zen.Context"] = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = serverUri.ToString(),
                RemoteAddress = "203.0.113.10",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", userInputUri.ToString() }
                }
            };

            HttpContext.Current = httpContext;
        }

        private async Task AssertSsrfBlockedAsync(string userInputUrl, string requestUrl, string serverUrl)
        {
            var userInputUri = new Uri(userInputUrl);
            var requestUri = new Uri(requestUrl);
            var serverUri = new Uri(serverUrl);

            SetCurrentContext(userInputUri, serverUri);

            using (var client = CreateClient())
            {
                var exception = Assert.ThrowsAsync<AikidoException>(async delegate
                {
                    await client.GetAsync(requestUri);
                });

                Assert.That(exception.Message, Is.EqualTo(ExpectedSsrfMessage));
            }
        }

        private async Task AssertNetworkFailureAsync(string userInputUrl, string requestUrl, string serverUrl)
        {
            var userInputUri = new Uri(userInputUrl);
            var requestUri = new Uri(requestUrl);
            var serverUri = new Uri(serverUrl);

            SetCurrentContext(userInputUri, serverUri);

            using (var client = CreateClient())
            {
                var exception = Assert.ThrowsAsync(Is.InstanceOf<Exception>(), async delegate
                {
                    await client.GetAsync(requestUri);
                });

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception, Is.Not.TypeOf<AikidoException>());
            }
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true
            })
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        private static async Task RunWithPatchedHttpClient(Func<Task> action)
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
                // .NET Framework HttpClient flows through HttpWebRequest, so we need that patch chain
                // active as well to observe redirect and DNS behavior end-to-end.
                WebRequestPatches.ApplyPatches(harmony);
                HttpClientPatches.ApplyPatches(harmony);
                DnsPatches.ApplyPatches(harmony);

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
