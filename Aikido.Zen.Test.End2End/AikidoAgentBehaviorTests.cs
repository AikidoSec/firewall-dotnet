using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net;
using Aikido.Zen.DotNetCore;
using System.Net.Http.Json;
using SQLiteSampleApp;
using Aikido.Zen.Server.Mock.Models;
using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Exceptions;

namespace Aikido.Zen.Test.End2End
{
    /// <summary>
    /// End-to-end tests focusing on the behavior of the Aikido Agent,
    /// such as event aggregation and reporting logic.
    /// </summary>
    [TestFixture] // Added TestFixture attribute
    public class AikidoAgentBehaviorTests : WebApplicationTestBase
    {
        /// <summary>
        /// Sets up database containers if needed (currently none required for these tests).
        /// </summary>
        protected override Task SetupDatabaseContainers()
        {
            // No database containers needed specifically for agent behavior tests yet.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates a factory for the sample application instance used in tests.
        /// </summary>
        private WebApplicationFactory<SQLiteStartup> CreateSampleAppFactory()
        {
            var factory = new WebApplicationFactory<SQLiteStartup>()
             .WithWebHostBuilder(builder =>
             {
                 builder.ConfigureServices(services =>
                 {
                     services.AddZenFirewall(options => options.UseHttpClient(MockServerClient));
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

        /// <summary>
        /// Performs setup actions once before any tests in this fixture run.
        /// </summary>
        [OneTimeSetUp]
        public override async Task OneTimeSetUp()
        {
            await base.OneTimeSetUp();
        }

        /// <summary>
        /// Verifies that requests resulting in non-successful status codes (e.g., 404)
        /// are not counted by the agent, while successful requests (e.g., 200) are counted.
        /// This tests the change where request counting is tied to route discovery.
        /// </summary>
        [Test, NonParallelizable]
        public async Task WhenRequestReturnsNonSuccess_ShouldNotIncrementRequestCount()
        {
            // Arrange
            Agent.Instance.ClearContext(); // Clear context before the test
            await MockServerClient.DeleteAsync("/events"); // Clear previous events on the mock server (still good practice)
            SampleAppClient = CreateSampleAppFactory().CreateClient(); // Create a client for the sample app
            await Task.Delay(250); // Allow time for app/agent initialization and route discovery

            // Act
            // Send request to a non-existent path (should result in 404, not counted by Agent)
            var requestNotFound = new HttpRequestMessage(HttpMethod.Get, "/non-existent-path-for-agent-test");
            requestNotFound.Headers.Add("X-Forwarded-For", "15.16.17.18"); // Use an arbitrary IP
            using var responseNotFound = await SampleAppClient.SendAsync(requestNotFound);
            Assert.That(responseNotFound.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "Request to non-existent path should return 404.");

            // Send a request to a known valid endpoint to increment the count
            var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/health");
            healthRequest.Headers.Add("X-Forwarded-For", "15.16.17.18");
            using var healthResponse = await SampleAppClient.SendAsync(healthRequest);
            Assert.That(healthResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Request to /health should return 200 OK.");

            // Assert
            var stats = await GetStatsAsync();
            Assert.That(stats["requests"], Is.EqualTo("1"), "Expected AgentContext.Requests to be 1, counting only the successful request.");
        }

        /// <summary>
        /// Verifies that making outbound HTTP requests via the sample app's endpoint
        /// updates the Hostnames collection in the AgentContext.
        /// </summary>
        [Test, NonParallelizable]
        public async Task WhenOutboundRequestMade_ShouldUpdateHostnamesList()
        {
            // Arrange
            Agent.Instance.ClearContext();
            SampleAppClient = CreateSampleAppFactory().CreateClient(); // Need the client

            // Act
            // Make requests using the simplified endpoint with the 'uri' parameter
            // Note: We use non-routable/example domains to avoid actual external traffic during tests.
            // The agent should still capture the attempt.
            await SampleAppClient.GetAsync("/api/outboundRequest?uri=" + Uri.EscapeDataString("https://test.example.com"));
            await SampleAppClient.GetAsync("/api/outboundRequest?uri=" + Uri.EscapeDataString("http://test.example.com"));
            await SampleAppClient.GetAsync("/api/outboundRequest?uri=" + Uri.EscapeDataString("http://another.domain.net:8080"));
            await SampleAppClient.GetAsync("/api/outboundRequest?uri=" + Uri.EscapeDataString("http://192.168.1.100:9000"));

            // Short delay to allow agent processing of background hostname capture
            await Task.Delay(200);

            // Assert
            var stats = await GetStatsAsync();
            var hostnames = stats["domains"]?.Split(',')
                                         .Select(h => h.Split(':'))
                                         .Where(parts => parts.Length == 2 && int.TryParse(parts[1], out _))
                                         .Select(parts => new Host { Hostname = parts[0], Port = int.Parse(parts[1]) })
                                         .ToList() ?? new List<Host>();

            // The context stores host+port combinations
            // The agent normalizes schemes (https -> 443, http -> 80 if not specified)
            // Ensure the count reflects unique host:port pairs
            Assert.That(hostnames.Count(), Is.GreaterThanOrEqualTo(4), "Expected at least 4 unique host:port combinations based on the URIs provided.");
            Assert.That(hostnames.Any(h => h.Hostname == "test.example.com" && h.Port == 443), Is.True, "Expected https://test.example.com (port 443)");
            Assert.That(hostnames.Any(h => h.Hostname == "test.example.com" && h.Port == 80), Is.True, "Expected http://test.example.com (port 80)");
            Assert.That(hostnames.Any(h => h.Hostname == "another.domain.net" && h.Port == 8080), Is.True, "Expected http://another.domain.net:8080");
            Assert.That(hostnames.Any(h => h.Hostname == "192.168.1.100" && h.Port == 9000), Is.True, "Expected http://192.168.1.100:9000");
        }

        /// <summary>
        /// Verifies that sending a detected (but not blocked) attack event via an HTTP request increments the AttacksDetected count.
        /// </summary>
        [Test, NonParallelizable]
        public async Task WhenAttackDetected_ShouldIncrementAttackDetectedCount()
        {
            // Arrange
            await SetMode(false, false);
            Agent.Instance.ClearContext();
            SampleAppClient = CreateSampleAppFactory().CreateClient();

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

            // Act
            try
            {
                var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", unsafePayload);
                var content = await response.Content.ReadAsStringAsync();
                var stats = await GetStatsAsync();
                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(stats["attacksDetected"], Is.EqualTo("1"), "Expected AttacksDetected count to be 1.");
                Assert.That(stats["attacksBlocked"], Is.EqualTo("0"), "Expected AttacksBlocked count to be 0.");
            }
            catch (AikidoException ex)
            {
                Assert.That(ex.Message, Does.Contain("SQL injection detected"));
            }

        }

        /// <summary>
        /// Verifies that sending a blocked attack event via an HTTP request increments both AttacksDetected and AttacksBlocked counts.
        /// </summary>
        [Test, NonParallelizable]
        public async Task WhenAttackBlocked_ShouldIncrementAttackBlockedCount()
        {
            /// Arrange
            await SetMode(false, true);
            SampleAppClient = CreateSampleAppFactory().CreateClient();

            var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

            // Act
            try
            {
                var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", unsafePayload);
                var content = await response.Content.ReadAsStringAsync();
                var stats = await GetStatsAsync();
                // Assert
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                Assert.That(stats["attacksDetected"], Is.EqualTo("1"), "Expected AttacksDetected count to be 1.");
                Assert.That(stats["attacksBlocked"], Is.EqualTo("1"), "Expected AttacksBlocked count to be 1.");
            }
            catch (AikidoException ex)
            {
                Assert.That(ex.Message, Does.Contain("SQL injection detected"));
            }
        }

        /// <summary>
        /// Fetches the current agent stats from the sample application's /api/getStats endpoint.
        /// </summary>
        /// <returns>A dictionary containing the agent statistics.</returns>
        /// <exception cref="HttpRequestException">Thrown if the request to fetch stats fails.</exception>
        private async Task<IDictionary<string, string>> GetStatsAsync()
        {
            if (SampleAppClient == null)
            {
                throw new InvalidOperationException("SampleAppClient is not initialized. Ensure CreateSampleAppFactory().CreateClient() has been called.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/getStats");
            // Use a distinct IP for stats requests to avoid interfering with specific test IPs if needed
            request.Headers.Add("X-Forwarded-For", "10.10.10.10");
            using var response = await SampleAppClient.SendAsync(request);

            response.EnsureSuccessStatusCode(); // Throw if the status code is not 2xx

            var stats = await response.Content.ReadFromJsonAsync<IDictionary<string, string>>();
            if (stats == null)
            {
                throw new InvalidOperationException("Failed to deserialize stats from /api/getStats endpoint.");
            }
            return stats;
        }
    }
}
