using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net;
using Aikido.Zen.DotNetCore;
using System.Net.Http.Json;
using SQLiteSampleApp;
using Aikido.Zen.Server.Mock.Models;

namespace Aikido.Zen.Test.End2End
{
    /// <summary>
    /// End-to-end tests for Aikido blocking and rate limiting features including user blocking, IP blocking, allowed IPs,
    /// endpoint-specific IPs, rate limiting, and user agent blocking.
    /// </summary>
    public class AikidoBlockingTests : WebApplicationTestBase
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

        [Test]
        public async Task WhenUserIsBlocked_ShouldBlockRequest()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["blockedUserIds"] = new[] { "malicious_user" },
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "111.111.111.111");
            request.Headers.Add("user", "malicious_user");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Your request is blocked: User is blocked"));
        }

        [Test]
        public async Task WhenIPIsBlocked_ShouldBlockRequest()
        {
            // Arrange
            var blockedIPs = new FirewallListConfig
            {
                BlockedIPAddresses = [new FirewallListConfig.IPList
                {
                    Ips = ["123.123.123.123"],
                    Description = "Blocked IPs",
                    Source = "runtime"
                }]
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", blockedIPs);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "123.123.123.123");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Your request is blocked: IP is not allowed"));
        }

        [Test]
        public async Task WhenUserAgentIsBlocked_ShouldBlockRequest()
        {
            // Arrange
            var blockedUserAgents = new FirewallListConfig
            {
                BlockedUserAgents = "malicious-bot"
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", blockedUserAgents);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("User-Agent", "malicious-bot");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Your request is blocked: You are not allowed to access this resource because you have been identified as a bot."));
        }

        [Test]
        public async Task WhenRateLimitExceeded_ShouldBlockRequest()
        {
            // Arrange
            var rateLimitConfig = new Dictionary<string, object>
            {
                ["endpoints"] = new List<EndpointConfig>
                {
                    new EndpointConfig
                    {
                        Route = "/api/pets",
                        Method = "GET",
                        RateLimiting = new RateLimitingConfig
                        {
                            Enabled = true,
                            MaxRequests = 2,
                            WindowSizeInMS = 1000
                        }
                    },
                    new EndpointConfig
                    {
                        Route = "/api/pets/{id:int}",
                        Method = "POST",
                        RateLimiting = new RateLimitingConfig
                        {
                            Enabled = true,
                            MaxRequests = 2,
                            WindowSizeInMS = 1000
                        }
                    }
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", rateLimitConfig);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            for (int i = 0; i < 3; i++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
                request.Headers.Add("X-Forwarded-For", "127.0.0.1");
                var response = await SampleAppClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (i == 2)
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
                    Assert.That(responseBody, Does.Contain("You are rate limited by Aikido firewall."));
                }
                else
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                }
            }
        }

        [Test]
        public async Task WhenEndpointSpecificIPAllowed_ShouldAllowRequest()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["endpoints	"] = new List<EndpointConfig>
                {
                    new EndpointConfig {
                        Route = "/api/pets/create",
                        Method = "POST",
                        AllowedIPAddresses = ["123.123.123.123"]
                    }
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "123.123.123.123");
            request.Content = JsonContent.Create(new { Name = "Test Pet" });

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task WhenEndpointSpecificIPNotAllowed_ShouldBlockRequest()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["endpoints"] = new List<EndpointConfig>
                {
                    new EndpointConfig {
                        Route = "/api/pets/create",
                        Method = "POST",
                        AllowedIPAddresses = ["123.123.123.123"]
                    },
                    new EndpointConfig {
                        Route = "/api/pets/{id:int}",
                        Method = "GET",
                        AllowedIPAddresses = ["123.123.123.123"]
                    }
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/pets/create");
            request.Headers.Add("X-Forwarded-For", "123.123.123.1");
            request.Content = JsonContent.Create(new { Name = "Test Pet" });

            var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/pets/123");
            request2.Headers.Add("X-Forwarded-For", "123.123.123.1");

            // Act
            var response = await SampleAppClient.SendAsync(request);
            var response2 = await SampleAppClient.SendAsync(request2);
            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Your request is blocked: Ip is not allowed"));
            Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody2 = await response2.Content.ReadAsStringAsync();
            Assert.That(responseBody2, Does.Contain("Your request is blocked: Ip is not allowed"));
        }

        [Test]
        public async Task WhenGlobalAllowedIPListExactMatch_ShouldAllowRequest()
        {
            // Arrange
            var allowedIPs = new FirewallListConfig
            {
                AllowedIPAddresses = [new FirewallListConfig.IPList
                {
                    Ips = ["123.123.123.123"],
                    Description = "Allowed IPs",
                    Source = "runtime"
                }]
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", allowedIPs);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "123.123.123.123");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("["));
        }

        [Test]
        public async Task WhenGlobalAllowedIPRangeMatch_ShouldAllowRequest()
        {
            // Arrange
            var allowedIPs = new FirewallListConfig
            {
                AllowedIPAddresses = [new FirewallListConfig.IPList
                {
                    Ips = ["222.222.222.0/24"],
                    Description = "Allowed IP Range",
                    Source = "runtime"
                }]
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", allowedIPs);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "222.222.222.123");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("["));
        }

        [Test]
        public async Task WhenIPNotInGlobalAllowedList_ShouldBlockRequest()
        {
            // Arrange
            var allowedIPs = new FirewallListConfig
            {
                AllowedIPAddresses = [new FirewallListConfig.IPList
                {
                    Ips = ["123.123.123.123", "112.112.112.0/24"],
                    Description = "Allowed IPs",
                    Source = "runtime"
                }]
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/firewall/lists", allowedIPs);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/pets");
            request.Headers.Add("X-Forwarded-For", "111.111.111.111");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Your request is blocked: Ip is not allowed"));
        }
    }
}
