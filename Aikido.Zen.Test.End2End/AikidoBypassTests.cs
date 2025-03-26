using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net;
using Aikido.Zen.DotNetCore;
using System.Net.Http.Json;
using SQLiteSampleApp;
using Aikido.Zen.Server.Mock.Models;

namespace Aikido.Zen.Test.End2End
{
    public class AikidoBypassTests : WebApplicationTestBase
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
                            // add our mocked http client
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

        [Test]
        public async Task WhenBypassIpIsConfigured_ShouldAllowRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IP
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "123.123.123.123" }
            };
            var result = await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);

            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "123.123.123.123");
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("rows"));
        }

        [Test]
        public async Task WhenBypassIpIsNotConfigured_ShouldBlockRequest()
        {
            // Arrange
            // Configure mock server to return config without bypass IP
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string>()
            };
            var firewallLists = new FirewallListConfig
            {
                AllowedIPAddresses = []
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "192.168.1.1");
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Request blocked due to security policy."));
        }

        [Test]
        public async Task WhenBypassIpRangesConfigured_ShouldAllowMatchingIp()
        {
            // Arrange
            // Configure mock server to return config with bypass IP ranges
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "124.124.124.0/16" } // Using a public IPv6 address (Google Public DNS)
            };
            var firewallLists = new FirewallListConfig
            {
                AllowedIPAddresses = [new FirewallListConfig.IPList
                {
                    Ips = ["123.123.123.123"],
                    Description = "Allowed IP addresses",
                    Source = "runtime"
                }]
            };
            var configResult = await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            var listsResult = await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "124.124.124.124"); // IP within the 124.124.124.0/16 range

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("["));
        }

        [Test]
        public async Task WhenBypassIpv6IsConfigured_ShouldAllowRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IPv6
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "2001:4860:4860::8888" } // Using a public IPv6 address (Google Public DNS)
            };
            var firewallLists = new FirewallListConfig
            {
                AllowedIPAddresses = [new FirewallListConfig.IPList
                {
                    Ips = ["123.123.123.123"],
                    Description = "Allowed IP addresses",
                    Source = "runtime"
                }]
            };
            var configResult = await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            var listsResult = await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "2001:4860:4860::8888"); // Using the same public IPv6 address

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("["));
        }

        [Test]
        public async Task WhenBypassIpv6IsNotConfigured_ShouldBlockRequest()
        {
            // Arrange
            // Configure mock server to return config without bypass IPv6
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string>()
            };
            var firewallLists = new FirewallListConfig
            {
                AllowedIPAddresses = []
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "2001:0db8:85a3:0000:0000:8a2e:0370:7334");
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Request blocked due to security policy."));
        }
    }
}
