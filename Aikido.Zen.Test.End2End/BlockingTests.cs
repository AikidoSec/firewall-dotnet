using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using SQLiteSampleApp;

namespace Aikido.Zen.Test.End2End;

/// <summary>
/// End-to-end tests for blocking functionality including IP blocking, user agent blocking,
/// and endpoint-specific allow rules
/// </summary>
[TestFixture]
public class BlockingTests : WebApplicationTestBase
{
    private string _mockServerToken;
    private HttpClient _mockServerClient;

    private WebApplicationFactory<SQLiteStartup> CreateSampleAppFactory()
    {
        var factory = new WebApplicationFactory<SQLiteStartup>()
            .WithWebHostBuilder(builder =>
            {
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

    protected override async Task SetupDatabaseContainers()
    {
        // SQLite uses in-memory database, no container needed
        await Task.CompletedTask;
    }

    [OneTimeSetUp]
    public override async Task OneTimeSetUp()
    {
        await base.OneTimeSetUp();

        // Setup mock server client
        _mockServerClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        // Create a new app and get the token
        var response = await _mockServerClient.PostAsync("/api/runtime/apps", null);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        _mockServerToken = result["token"];
        _mockServerClient.DefaultRequestHeaders.Add("Authorization", _mockServerToken);
    }

    private async Task UpdateMockServerConfig(Dictionary<string, object> config)
    {
        await _mockServerClient.PostAsync("/api/runtime/config", JsonContent.Create(config));
    }

    private async Task UpdateFirewallLists(List<string> blockedIps, string blockedUserAgents)
    {
        var lists = new Dictionary<string, object>
        {
            ["blockedIPAddresses"] = blockedIps,
            ["blockedUserAgents"] = blockedUserAgents
        };
        await _mockServerClient.PostAsync("/api/runtime/firewall/lists", JsonContent.Create(lists));
    }

    [OneTimeTearDown]
    public override async Task OneTimeTearDown()
    {
        _mockServerClient?.Dispose();
        await base.OneTimeTearDown();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestIPBlocking_WhenIPIsBlocked_ShouldReturnForbidden()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateFirewallLists(new List<string> { "192.168.1.100" }, "");
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.100");

        // Act
        var response = await client.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestIPBlocking_WhenIPIsAllowed_ShouldSucceed()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateMockServerConfig(new Dictionary<string, object>
        {
            ["allowedIPAddresses"] = new[] { "192.168.1.200" }
        });
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.200");

        // Act
        var response = await client.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestUserAgentBlocking_WhenUserAgentIsBlocked_ShouldReturnForbidden()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateFirewallLists(new List<string>(), "malicious-bot");
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "malicious-bot");

        // Act
        var response = await client.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestEndpointSpecificAllowRule_WhenIPAllowedForEndpoint_ShouldSucceed()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateMockServerConfig(new Dictionary<string, object>
        {
            ["endpoints"] = new[]
            {
                new
                {
                    method = "GET",
                    route = "api/pets",
                    allowedIPAddresses = new[] { "192.168.1.150" }
                }
            }
        });
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.150");

        // Act
        var response = await client.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestEndpointSpecificAllowRule_WhenIPNotAllowedForEndpoint_ShouldReturnForbidden()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateMockServerConfig(new Dictionary<string, object>
        {
            ["endpoints"] = new[]
            {
                new
                {
                    method = "GET",
                    route = "api/pets",
                    allowedIPAddresses = new[] { "123.123.123.123" }
                }
            }
        });
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "123.123.123.121");

        // Act
        var response = await client.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestPrivateIP_ShouldNotBeBlocked_EvenWhenExplicitlyBlocked()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateFirewallLists(
            new List<string> { "192.168.1.1", "10.0.0.1", "172.16.0.1" },
            ""
        );
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();

        // Test multiple private IP ranges
        var privateIps = new[] {
            "192.168.1.1",    // Class C private
            "10.0.0.1",       // Class A private
            "172.16.0.1"      // Class B private
        };

        foreach (var ip in privateIps)
        {
            // Act
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            var response = await client.GetAsync("/api/pets");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Private IP {ip} should not be blocked even when explicitly added to blocklist");
        }
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestLocalIP_ShouldNotBeBlocked_EvenWhenExplicitlyBlocked()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateFirewallLists(
            new List<string> { "127.0.0.1", "::1" },
            ""
        );
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();

        // Test both IPv4 and IPv6 localhost
        var localIps = new[] {
            "127.0.0.1",      // IPv4 localhost
            "::1"             // IPv6 localhost
        };

        foreach (var ip in localIps)
        {
            // Act
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            var response = await client.GetAsync("/api/pets");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Local IP {ip} should not be blocked even when explicitly added to blocklist");
        }
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestPrivateAndLocalIP_ShouldNotBeBlocked_WhenEndpointHasAllowList()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateMockServerConfig(new Dictionary<string, object>
        {
            ["endpoints"] = new[]
            {
                new
                {
                    method = "GET",
                    route = "api/pets",
                    allowedIPAddresses = new[] { "8.8.8.8" }
                }
            }
        });
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();

        var privateAndLocalIps = new[] {
            "127.0.0.1",      // IPv4 localhost
            "::1",            // IPv6 localhost
            "192.168.1.1",    // Class C private
            "10.0.0.1",       // Class A private
            "172.16.0.1"      // Class B private
        };

        foreach (var ip in privateAndLocalIps)
        {
            // Act
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            var response = await client.GetAsync("/api/pets");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Private/Local IP {ip} should not be blocked even when endpoint has specific allow list");
        }
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestMultipleBlockingRules_WhenBothIPAndUserAgentBlocked_ShouldReturnForbidden()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        await UpdateFirewallLists(
            new List<string> { "192.168.1.180" },
            "bad-bot"
        );
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.180");
        client.DefaultRequestHeaders.Add("User-Agent", "bad-bot");

        // Act
        var response = await client.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
