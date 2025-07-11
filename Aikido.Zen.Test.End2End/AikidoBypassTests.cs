using System.Net;
using System.Net.Http.Json;
using Aikido.Zen.DotNetCore;
using Aikido.Zen.Server.Mock.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
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

        /// <summary>
        /// Tests that path traversal attacks are bypassed when coming from a bypassed IP address,
        /// even when using multiple query parameters with the flattened query parameter functionality.
        /// </summary>
        [Test]
        public async Task WhenPathTraversalFromBypassedIP_ShouldAllowRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IP
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "10.0.0.100" }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with path traversal attack from bypassed IP
            var maliciousPath = "/../../../nonexistent/file/that/will/fail.txt";
            var safePath = "/safe.txt";
            var queryString = $"path={Uri.EscapeDataString(maliciousPath)}&path={Uri.EscapeDataString(safePath)}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "10.0.0.100"); // Bypassed IP

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - Request should be allowed because IP is bypassed
            // Even though path traversal is present, the bypass should take precedence
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Path traversal attack should be bypassed for allowed IP, returning BadRequest from endpoint logic rather than Forbidden from firewall");
        }

        /// <summary>
        /// Tests that path traversal attacks are blocked when NOT coming from a bypassed IP address,
        /// verifying that the flattened query parameters are properly checked for attacks.
        /// </summary>
        [Test]
        public async Task WhenPathTraversalFromNonBypassedIP_ShouldBlockRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IP (different from test IP)
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "10.0.0.100" }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with path traversal attack from non-bypassed IP
            var maliciousPath = "../../../etc/passwd";
            var anotherMaliciousPath = "/../secret.txt";
            var queryString = $"file={Uri.EscapeDataString(maliciousPath)}&path={Uri.EscapeDataString(anotherMaliciousPath)}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "192.168.1.50"); // Non-bypassed IP

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - Request should be blocked because IP is not bypassed and path traversal is detected
            // With flattening: file="../../../etc/passwd", path="/../secret.txt"
            // The firewall should detect the "../" pattern in both parameters
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Request blocked due to security policy."));
        }

        /// <summary>
        /// Tests that path traversal attacks in indexed query parameters are bypassed
        /// when coming from an IP range that is configured for bypass.
        /// </summary>
        [Test]
        public async Task WhenPathTraversalFromBypassedIPRange_ShouldAllowRequest()
        {
            // Arrange
            // Configure mock server to return config with bypass IP range
            var bypassList = new Dictionary<string, object>
            {
                ["allowedIPAddresses"] = new List<string> { "172.16.0.0/16" }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", bypassList);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with multiple query parameters containing path traversal
            var safePath = "/safe.txt";
            var maliciousPath = "/../../../etc/passwd";
            var queryString = $"path={Uri.EscapeDataString(safePath)}&path={Uri.EscapeDataString(maliciousPath)}&path=another_safe_path";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "172.16.123.45"); // IP within the bypassed range

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - Request should be allowed because IP is within bypassed range
            // With flattening: path="/safe.txt", path[1]="/../../../etc/passwd", path[2]="another_safe_path"
            // Even though the second parameter contains path traversal, the bypass should take precedence
            // The endpoint only uses the first parameter (safe.txt) so it will succeed
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Path traversal attack should be bypassed for IP within allowed range, and endpoint should process the safe first parameter successfully");
        }
    }
}
