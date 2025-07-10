using System.Net;
using System.Net.Http.Json;
using Aikido.Zen.DotNetCore;
using Aikido.Zen.Server.Mock.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using SQLiteSampleApp;

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
            Assert.That(responseBody, Does.Contain("Your request is blocked: IP is blocked"));
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
            Assert.That(responseBody, Does.Contain("Your request is blocked: IP is not allowed for this endpoint"));
            Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody2 = await response2.Content.ReadAsStringAsync();
            Assert.That(responseBody2, Does.Contain("Your request is blocked: IP is not allowed for this endpoint"));
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
            Assert.That(responseBody, Does.Contain("Your request is blocked: IP is not allowed"));
        }

        /// <summary>
        /// Tests that the endpoint matching logic prioritizes configurations correctly for rate limiting.
        /// Priority Order:
        /// 1. Exact URL & Exact Method
        /// 2. Exact Route & Exact Method
        /// 3. Exact URL & Wildcard Method
        /// 4. Exact Route & Wildcard Method
        /// This test verifies Priority 1 overrides Priority 2.
        /// </summary>
        [Test]
        public async Task WhenExactUrlAndMethodMatch_RateLimit_ShouldPrioritizeOverExactRoute()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["endpoints"] = new List<EndpointConfig>
                {
                    // Priority 2: Exact Route & Exact Method (Less specific)
                    new EndpointConfig {
                        Route = "/api/prioritytest/{id}",
                        Method = "GET",
                        RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 5, WindowSizeInMS = 5000 }
                    },
                    // Priority 1: Exact URL & Exact Method (Most specific)
                    new EndpointConfig {
                        Route = "/api/prioritytest/123", // Matches the exact URL
                        Method = "GET",
                        RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 2, WindowSizeInMS = 5000 }
                    }
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Act & Assert: Send 3 requests, expect the 3rd to be rate limited based on Priority 1 config (MaxRequests = 2)
            for (int i = 0; i < 3; i++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/prioritytest/123");
                request.Headers.Add("X-Forwarded-For", "192.168.1.1");
                var response = await SampleAppClient.SendAsync(request);

                if (i == 2) // Third request
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests), $"Request {i + 1} should be rate limited.");
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Assert.That(responseBody, Does.Contain("You are rate limited by Aikido firewall."));
                }
                else // First two requests
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Request {i + 1} should be allowed.");
                }
            }
        }

        /// <summary>
        /// Tests that the endpoint matching logic prioritizes configurations correctly for IP allow lists.
        /// Priority Order:
        /// 1. Exact URL & Exact Method
        /// 2. Exact Route & Exact Method
        /// 3. Exact URL & Wildcard Method
        /// 4. Exact Route & Wildcard Method
        /// This test verifies Priority 2 overrides Priority 4.
        /// </summary>
        [Test]
        public async Task WhenExactRouteAndMethodMatch_IPAllowList_ShouldPrioritizeOverWildcardMethod()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["endpoints"] = new List<EndpointConfig>
                {
                     // Priority 4: Exact Route & Wildcard Method (Less specific) - Allows different IP
                    new EndpointConfig {
                        Route = "/api/prioritytest/{id}",
                        Method = "*",
                        AllowedIPAddresses = ["222.222.222.222"]
                    },
                    // Priority 2: Exact Route & Exact Method (More specific) - Allows test IP
                    new EndpointConfig {
                        Route = "/api/prioritytest/{id}",
                        Method = "POST",
                        AllowedIPAddresses = ["192.168.1.2"]
                    }
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Act: Send request from the IP allowed by the more specific rule (Priority 2)
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/prioritytest/456");
            request.Headers.Add("X-Forwarded-For", "192.168.1.2");
            request.Content = JsonContent.Create(new { Name = "Priority Test" });
            var response = await SampleAppClient.SendAsync(request);

            // Assert: Request should be allowed based on Priority 2 rule
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Act: Send request from an IP not allowed by the specific rule but allowed by the wildcard rule
            var requestBlocked = new HttpRequestMessage(HttpMethod.Post, "/api/prioritytest/789");
            requestBlocked.Headers.Add("X-Forwarded-For", "222.222.222.222"); // Allowed by wildcard, but not by specific POST rule
            requestBlocked.Content = JsonContent.Create(new { Name = "Priority Test Blocked" });
            var responseBlocked = await SampleAppClient.SendAsync(requestBlocked);

            // Assert: Request should be blocked because the specific rule (Priority 2) takes precedence and does not allow this IP
            Assert.That(responseBlocked.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBodyBlocked = await responseBlocked.Content.ReadAsStringAsync();
            Assert.That(responseBodyBlocked, Does.Contain("Your request is blocked: IP is not allowed for this endpoint"));
        }

        /// <summary>
        /// Verifies that rate limiting is applied correctly to routes detected as single generic parameters
        /// using the {paramName} notation (e.g., /api/v1/{slug}).
        /// </summary>
        [Test]
        public async Task RoutesWithSlugParameter_RateLimit_ShouldApplyToMatchingRequests()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["endpoints"] = new List<EndpointConfig>
                {
                    new EndpointConfig {
                        Route = "/api/v1/{slug}/test", // Matches /api/v1/page-one, /api/v1/another-page etc.
                        Method = "GET",
                        RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 1, WindowSizeInMS = 1000 }
                    },
                    new EndpointConfig {
                        Route = "/api/v1/test", // Matches /api/v1/page-one, /api/v1/another-page etc.
                        Method = "GET",
                        RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 2, WindowSizeInMS = 1000 }
                    },
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Act & Assert: Send 2 requests matching the generic pattern, expect the 2nd to be rate limited
            // First request should be allowed, subsequent requests should be rate limited
            const string ipAddress = "10.0.0.2";
            const int requestCount = 3;

            for (int i = 0; i < requestCount; i++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/{i}/test");
                request.Headers.Add("X-Forwarded-For", ipAddress);
                var response = await SampleAppClient.SendAsync(request);
                var request2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/test");
                request2.Headers.Add("X-Forwarded-For", ipAddress);
                var response2 = await SampleAppClient.SendAsync(request2);

                if (i == 0)
                {
                    // First request should be allowed
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                        $"Request /api/v1/{i}/test should be allowed.");
                    Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                        $"Request /api/v1/test should be allowed.");
                }
                else if (i == 1)
                {
                    // Subsequent requests should be rate limited
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests),
                        $"Request /api/v1/{i}/test should be rate limited.");
                    Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                        $"Request /api/v1/test should be allowed.");
                }
                else
                {
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests),
                        $"Request /api/v1/{i}/test should be rate limited.");
                    Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests),
                        $"Request /api/v1/test should be rate limited.");
                }
            }
        }

        /// <summary>
        /// Verifies that IP allow lists are applied correctly to routes detected as single generic parameters
        /// using the {paramName} notation (e.g., /api/v1/{slug}).
        /// </summary>
        [Test]
        public async Task WhenRouteIsSingleGenericSlugParameter_IPAllowList_ShouldApplyToMatchingRequests()
        {
            // Arrange
            var config = new Dictionary<string, object>
            {
                ["endpoints"] = new List<EndpointConfig>
                {
                    new EndpointConfig {
                        Route = "/api/v1/{slug}", // Matches /api/v1/<any_slug>
                        Method = "GET",
                        AllowedIPAddresses = ["10.10.10.11"]
                    }
                }
            };
            await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Act: Send request from the allowed IP
            var requestAllowed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/allowed-slug");
            requestAllowed.Headers.Add("X-Forwarded-For", "10.10.10.11");
            var responseAllowed = await SampleAppClient.SendAsync(requestAllowed);

            // Assert: Request should be allowed
            Assert.That(responseAllowed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Act: Send request from a blocked IP to a different slug
            var requestBlocked = new HttpRequestMessage(HttpMethod.Get, "/api/v1/blocked-slug");
            requestBlocked.Headers.Add("X-Forwarded-For", "11.11.11.12");
            var responseBlocked = await SampleAppClient.SendAsync(requestBlocked);

            // Assert: Request should be blocked
            Assert.That(responseBlocked.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBodyBlocked = await responseBlocked.Content.ReadAsStringAsync();
            Assert.That(responseBodyBlocked, Does.Contain("Your request is blocked: IP is not allowed for this endpoint"));
        }

        /// <summary>
        /// Tests that query parameter flattening works correctly with multiple parameters
        /// and verifies the endpoint can process them successfully.
        /// </summary>
        [Test]
        public async Task WhenQueryParameterFlattening_WithMultipleParameters_ShouldWorkCorrectly()
        {
            // Arrange
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with multiple safe path parameters to test flattening
            var safePath1 = "safe.txt";
            var safePath2 = "another-safe.txt";
            var safePath3 = "third-safe.txt";
            var queryString = $"path={Uri.EscapeDataString(safePath1)}&path={Uri.EscapeDataString(safePath2)}&path={Uri.EscapeDataString(safePath3)}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "192.168.1.1");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - The request should succeed and demonstrate flattening is working
            // With flattening: path="safe.txt", path[1]="another-safe.txt", path[2]="third-safe.txt"
            // The endpoint uses the first parameter (safe.txt) for file reading
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("allFlattenedParams"), "Response should contain the flattened paths array");
            Assert.That(responseBody, Does.Contain("safe.txt"), "Response should contain the first path used for file reading");
        }

        /// <summary>
        /// Tests that query parameter flattening works correctly with multiple parameters
        /// and verifies the endpoint can process them successfully.
        /// </summary>
        [Test]
        public async Task WhenMultipleQueryParametersWithSafeValues_ShouldProcessCorrectly()
        {
            // Arrange
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with multiple safe path parameters
            var safePath1 = "safe.txt";
            var safePath2 = "another-safe.txt";
            var queryString = $"path={Uri.EscapeDataString(safePath1)}&path={Uri.EscapeDataString(safePath2)}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "192.168.1.1");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - The request should succeed and the flattened parameters should be processed
            // With flattening: path="safe.txt", path[1]="another-safe.txt"
            // The endpoint uses the first parameter (safe.txt) for file reading
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("allFlattenedParams"), "Response should contain the flattened paths array");
        }

        /// <summary>
        /// Tests that path traversal detection works with the path parameter
        /// when using the flattened query parameter functionality.
        /// </summary>
        [Test]
        public async Task WhenPathTraversalInPathParameter_ShouldDetectAndBlockAttack()
        {
            // Arrange
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with path parameter containing path traversal
            var maliciousPath = "../../../etc/passwd";
            var queryString = $"path={Uri.EscapeDataString(maliciousPath)}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "192.168.1.1");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - The path traversal should be detected in the flattened query parameters
            // With flattening: path="../../../etc/passwd"
            // The firewall should detect the "../" pattern when File.ReadAllText is called
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Request blocked due to security policy."));
        }

        /// <summary>
        /// Tests that path traversal detection works correctly when mixing unsafe and safe query parameters
        /// with the same parameter name, verifying that the firewall detects the attack in the flattened parameters.
        /// </summary>
        [Test]
        public async Task WhenMixedSafeAndUnsafePathParameters_ShouldDetectAndBlockAttack()
        {
            // Arrange
            SampleAppClient = CreateSampleAppFactory().CreateClient();
            Thread.Sleep(250);

            // Create a request with mixed safe and unsafe path parameters
            // The first parameter contains path traversal (unsafe), the second is safe
            var unsafePath = "../../../etc/passwd";  // Use the same pattern as the working test
            var safePath = "./safe";
            var queryString = $"path={Uri.EscapeDataString(unsafePath)}&path={Uri.EscapeDataString(safePath)}";

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/path-traversal?{queryString}");
            request.Headers.Add("X-Forwarded-For", "192.168.1.1");

            // Act
            var response = await SampleAppClient.SendAsync(request);

            // Assert - The path traversal should be detected in the flattened query parameters
            // With flattening: path="/../secret.txt", path[1]="./safe"
            // The firewall should detect the "../" pattern in the first parameter when File.ReadAllText is called
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.That(responseBody, Does.Contain("Request blocked due to security policy."));
        }
    }
}
