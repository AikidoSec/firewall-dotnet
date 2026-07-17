using Aikido.Zen.DotNetCore;
using Aikido.Zen.Core;
using Aikido.Zen.Server.Mock.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using SQLiteSampleApp;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aikido.Zen.Test.End2End
{
    [TestFixture]
    [NonParallelizable]
    public class RealtimeConfigUpdateTests : WebApplicationTestBase
    {
        private WebApplicationFactory<SQLiteStartup>? _sampleAppFactory;

        protected override Task SetupDatabaseContainers()
        {
            return Task.CompletedTask;
        }

        [OneTimeSetUp]
        public override async Task OneTimeSetUp()
        {
            await base.OneTimeSetUp();
            SampleAppEnvironmentVariables["AIKIDO_FEATURE_SSE"] = "true";
            await SetMode(disabled: false, block: true);

            _sampleAppFactory = new WebApplicationFactory<SQLiteStartup>()
                .WithWebHostBuilder(ConfigureSampleApp);
            SampleAppClient = _sampleAppFactory.CreateClient();
        }

        [OneTimeTearDown]
        public override async Task OneTimeTearDown()
        {
            _sampleAppFactory?.Dispose();
            Environment.SetEnvironmentVariable("AIKIDO_FEATURE_SSE", null);
            await base.OneTimeTearDown();
        }

        [Test]
        public async Task MockRealtimeStream_EmitsConfigUpdates()
        {
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var streamRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/runtime/stream");
            using var streamResponse = await MockServerClient.SendAsync(
                streamRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationSource.Token);
            streamResponse.EnsureSuccessStatusCode();

            using var stream = await streamResponse.Content.ReadAsStreamAsync(cancellationSource.Token);
            using var reader = new StreamReader(stream);
            var initialTimestamp = await ReadConfigUpdatedAt(reader, cancellationSource.Token);

            await MockServerClient.PostAsJsonAsync(
                "/api/runtime/config",
                new Dictionary<string, object> { ["block"] = true },
                cancellationSource.Token);

            var updatedTimestamp = await ReadConfigUpdatedAt(reader, cancellationSource.Token);

            Assert.That(updatedTimestamp, Is.GreaterThan(initialTimestamp));
        }

        [Test]
        public async Task FirewallListUpdate_IsAppliedThroughRealtimeStream()
        {
            const string blockedIp = "123.123.123.124";

            using (var beforeRequest = CreateRequest(blockedIp))
            using (var beforeResponse = await SampleAppClient.SendAsync(beforeRequest))
            {
                Assert.That(beforeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }

            var startupDeadline = DateTime.UtcNow.AddSeconds(5);
            while (Agent.Instance.Context.Config.ConfigLastUpdated == 0 &&
                   DateTime.UtcNow < startupDeadline)
            {
                await Task.Delay(100);
            }

            var previousConfigUpdatedAt = Agent.Instance.Context.Config.ConfigLastUpdated;
            Assert.That(
                previousConfigUpdatedAt,
                Is.GreaterThan(0),
                "The sample app must load its startup config before testing a realtime update.");

            await MockServerClient.PostAsJsonAsync(
                "/api/runtime/firewall/lists",
                new FirewallListConfig
                {
                    BlockedIPAddresses =
                    [
                        new FirewallListConfig.IPList
                        {
                            Key = "realtime-test",
                            Ips = [blockedIp],
                            Description = "Realtime test",
                            Source = "runtime"
                        }
                    ]
                });

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!Agent.Instance.Context.Config
                       .GetMatchingBlockedIPListKeys(blockedIp)
                       .Any() &&
                   DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            var currentConfigUpdatedAt = Agent.Instance.Context.Config.ConfigLastUpdated;
            var matchingLists = Agent.Instance.Context.Config
                .GetMatchingBlockedIPListKeys(blockedIp)
                .ToArray();

            using var request = CreateRequest(blockedIp);
            using var response = await SampleAppClient.SendAsync(request);

            Assert.Multiple(() =>
            {
                Assert.That(
                    currentConfigUpdatedAt,
                    Is.GreaterThan(previousConfigUpdatedAt),
                    "The realtime event should advance the local config timestamp.");
                Assert.That(
                    matchingLists,
                    Does.Contain("realtime-test"),
                    "The realtime refresh should apply the updated firewall list.");
                Assert.That(
                    response.StatusCode,
                    Is.EqualTo(HttpStatusCode.Forbidden),
                    "The firewall list update should be applied through SSE well before the one-minute polling fallback.");
            });
        }

        private void ConfigureSampleApp(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddZenFirewall(options => options.UseHttpClient(MockServerClient));
            });
            builder.ConfigureAppConfiguration((_, _) =>
            {
                foreach (var envVar in SampleAppEnvironmentVariables)
                {
                    Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
                }
            });
        }

        private static HttpRequestMessage CreateRequest(string ipAddress)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", ipAddress);
            return request;
        }

        private static async Task<long> ReadConfigUpdatedAt(
            StreamReader reader,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    throw new EndOfStreamException("Realtime stream ended before a config update was received.");
                }

                if (!line.StartsWith("data: "))
                {
                    continue;
                }

                using var data = JsonDocument.Parse(line.Substring("data: ".Length));
                return data.RootElement.GetProperty("configUpdatedAt").GetInt64();
            }
        }
    }
}
