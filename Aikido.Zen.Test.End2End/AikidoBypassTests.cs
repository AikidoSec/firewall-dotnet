using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net;
using Aikido.Zen.DotNetCore;
using System.Net.Http.Json;
using SQLiteSampleApp;

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
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        }

        [Test]
        public async Task WhenBypassIpIsConfigured_ShouldAllowRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IP
            var firewallLists = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "123.123.123.123" }
            };
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "123.123.123.123");
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task WhenBypassIpIsNotConfigured_ShouldBlockRequest()
        {
            // Arrange
            // Configure mock server to return config without bypass IP
            var firewallLists = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string>()
            };
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "192.168.1.1");
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task WhenBypassIpRangesConfigured_ShouldAllowMatchingIp()
        {
            // Arrange
            // Configure mock server to return config with bypass IP ranges
            var firewallLists = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "10.0.0.0/8", "123.123.123.0/16", "172.16.0.0/12" }
            };
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "123.123.123.123"); // IP within the 123.123.123.0/16 range
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task WhenBypassIpv6IsConfigured_ShouldAllowRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IPv6
            var firewallLists = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "2001:4860:4860::8888" } // Using a public IPv6 address (Google Public DNS)
            };
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
            var result = await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "2001:4860:4860::8888"); // Using the same public IPv6 address
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task WhenBypassIpv6IsNotConfigured_ShouldBlockRequest()
        {
            // Arrange
            // Configure mock server to return config without bypass IPv6
            var firewallLists = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string>()
            };
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", firewallLists);
            SampleAppClient = CreateSampleAppFactory().CreateClient();

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "2001:0db8:85a3:0000:0000:8a2e:0370:7334");
            request.Content = JsonContent.Create(unsafePayload);

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
    }
}
