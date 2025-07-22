using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using SQLiteSampleApp;
using FirewallListConfig = Aikido.Zen.Server.Mock.Models.FirewallListConfig;
using MockUserAgentDetails = Aikido.Zen.Server.Mock.Models.UserAgentDetails;

namespace Aikido.Zen.Test.End2End
{
    public class AikidoMonitoringTests : WebApplicationTestBase
    {
        protected override Task SetupDatabaseContainers()
        {
            return Task.CompletedTask;
        }

        private WebApplicationFactory<SQLiteStartup> CreateSampleAppFactory()
        {
            var factory = new WebApplicationFactory<SQLiteStartup>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddZenFirewall(options =>
                        {
                            options.UseHttpClient(MockServerClient);
                        });
                    });
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        foreach (var envVar in SampleAppEnvironmentVariables)
                        {
                            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
                        }
                    });
                });
            return factory;
        }

        [OneTimeSetUp]
        public override async Task OneTimeSetUp()
        {
            await base.OneTimeSetUp();
            await SetMode(false, true);
        }

        [SetUp]
        public async Task Setup()
        {
            await MockServerClient.DeleteAsync("/api/monitoring/events");
        }

        [Test, Timeout(90000)] // 90 second timeout to allow for heartbeat
        public async Task WhenAgentIsRunning_ShouldSendHeartbeat()
        {
            // Arrange
            SampleAppClient = CreateSampleAppFactory().CreateClient();

            // Act
            // Wait for the heartbeat interval (1 minute in debug mode)
            await Task.Delay(TimeSpan.FromMinutes(1.1));

            // Assert
            var response = await MockServerClient.GetAsync("/api/monitoring/events");
            response.EnsureSuccessStatusCode();
            var events = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            var heartbeat = events.FirstOrDefault(e => e.TryGetProperty("type", out var type) && type.GetString() == "heartbeat");

            Assert.That(heartbeat.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined), "Heartbeat event not found");
        }

        [Test, Timeout(90000)] // 90 second timeout to allow for heartbeat
        public async Task MonitoredStats_AreReportedInHeartbeat()
        {
            // Arrange
            var monitoredIps = new List<FirewallListConfig.IPList>
            {
                new FirewallListConfig.IPList { Key = "monitored1", Ips = new[] { "1.1.1.1" } }
            };
            var userAgentDetails = new List<MockUserAgentDetails>
            {
                new MockUserAgentDetails { Key = "TestBot", Value = "TestBot", Monitored = true }
            };
            var regexPattern = "(?<TestBot>TestBot)";

            var firewallConfig = new FirewallListConfig
            {
                MonitoredIPAddresses = monitoredIps,
                MonitoredUserAgents = regexPattern,
                UserAgentDetails = userAgentDetails
            };

            await MockServerClient.PostAsJsonAsync("/api/monitoring/configure", firewallConfig);

            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250); // Allow agent to fetch config

            // Act
            var request1 = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request1.Headers.Add("X-Forwarded-For", "1.1.1.1");
            await SampleAppClient.SendAsync(request1);

            var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request2.Headers.Add("User-Agent", "TestBot");
            await SampleAppClient.SendAsync(request2);

            var request3 = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request3.Headers.Add("User-Agent", "TestBot");
            await SampleAppClient.SendAsync(request3); // Send second time

            // Wait for heartbeat
            await Task.Delay(TimeSpan.FromMinutes(1.1));

            // Assert
            var response = await MockServerClient.GetAsync("/api/monitoring/events");
            response.EnsureSuccessStatusCode();
            var events = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            var heartbeat = events.FirstOrDefault(e => e.TryGetProperty("type", out var type) && type.GetString() == "heartbeat");

            Assert.That(heartbeat.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined), "Heartbeat event not found");

            var stats = heartbeat.GetProperty("stats");
            var ipAddresses = stats.GetProperty("ipAddresses").GetProperty("breakdown");
            var userAgents = stats.GetProperty("userAgents").GetProperty("breakdown");

            Assert.That(ipAddresses.GetProperty("monitored1").GetInt64(), Is.EqualTo(1));
            Assert.That(userAgents.GetProperty("TestBot").GetInt64(), Is.EqualTo(2));
        }
    }
}
