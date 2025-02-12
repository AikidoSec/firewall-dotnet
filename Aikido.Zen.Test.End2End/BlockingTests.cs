using System.Net;
using System.Net.Http.Json;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using SQLiteSampleApp;
using Aikido.Zen.DotNetCore;
namespace Aikido.Zen.Test.End2End;

/// <summary>
/// End-to-end tests for blocking functionality including IP blocking, user agent blocking,
/// and endpoint-specific allow rules
/// </summary>
[TestFixture]
public class BlockingTests : WebApplicationTestBase
{

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
                    Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
                });
            });
        return factory;
    }


    protected override async Task SetupDatabaseContainers()
    {
        // SQLite uses in-memory database, no container needed
        await Task.CompletedTask;
    }

    private async Task UpdateFirewallLists(List<string> blockedIps, string blockedUserAgents)
    {
        var lists = new Dictionary<string, object>
        {
            ["blockedIPAddresses"] = blockedIps,
            ["blockedUserAgents"] = blockedUserAgents
        };
        await MockServerClient.PostAsync("/api/runtime/firewall/lists", JsonContent.Create(lists));
    }

    [OneTimeTearDown]
    public override async Task OneTimeTearDown()
    {
        SampleAppClient?.Dispose();
        await base.OneTimeTearDown();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestIPBlocking_WhenIPIsBlocked_ShouldReturnForbidden()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["allowedIpAddresses"] = new[] { "127.0.0.1" },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", "123.123.123.213");

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestIPBlocking_WhenIPIsAllowed_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["allowedIpAddresses"] = new[] { "192.168.1.200" },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.200");

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestUserAgentBlocking_WhenUserAgentIsBlocked_ShouldReturnForbidden()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["allowedIpAddresses"] = new[] { "127.0.0.1" },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        await UpdateFirewallLists(new List<string>(), "malicious-bot");
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        SampleAppClient.DefaultRequestHeaders.Add("User-Agent", "malicious-bot");
        SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", "123.123.123.123");

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestEndpointSpecificAllowRule_WhenIPAllowedForEndpoint_ShouldSucceed()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["endpoints"] = new[]
            {
                new
                {
                    method = "GET",
                    route = "api/pets",
                    allowedIPAddresses = new[] { "192.168.1.150" }
                }
            },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.150");

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestEndpointSpecificAllowRule_WhenIPNotAllowedForEndpoint_ShouldReturnForbidden()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["endpoints"] = new[]
            {
                new
                {
                    method = "GET",
                    route = "api/pets",
                    allowedIPAddresses = new[] { "123.123.123.123" }
                }
            },
            ["block"] = true,
        };
        var configResponse = await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", "123.123.123.121");

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestPrivateIP_ShouldNotBeBlocked_EvenWhenExplicitlyBlocked()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["allowedIpAddresses"] = new[] { "127.0.0.1" },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        await UpdateFirewallLists(
            new List<string> { "192.168.1.1", "10.0.0.1", "172.16.0.1" },
            ""
        );

        // Test multiple private IP ranges
        var privateIps = new[] {
            "192.168.1.1",    // Class C private
            "10.0.0.1",       // Class A private
            "172.16.0.1"      // Class B private
        };
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        foreach (var ip in privateIps)
        {
            // Act
            SampleAppClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
            SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            var response = await SampleAppClient.GetAsync("/api/pets");

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
        var config = new Dictionary<string, object>
        {
            ["allowedIpAddresses"] = new[] { "127.0.0.1" },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        await UpdateFirewallLists(
            new List<string> { "127.0.0.1", "::1" },
            ""
        );

        // Test both IPv4 and IPv6 localhost
        var localIps = new[] {
            "127.0.0.1",      // IPv4 localhost
            "::1"             // IPv6 localhost
        };
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        foreach (var ip in localIps)
        {
            // Act
            SampleAppClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
            SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            var response = await SampleAppClient.GetAsync("/api/pets");

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
        var config = new Dictionary<string, object>
        {
            ["endpoints"] = new[]
            {
                new
                {
                    method = "GET",
                    route = "api/pets",
                    allowedIPAddresses = new[] { "8.8.8.8" }
                }
            },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);

        var privateAndLocalIps = new[] {
            "127.0.0.1",      // IPv4 localhost
            "::1",            // IPv6 localhost
            "192.168.1.1",    // Class C private
            "10.0.0.1",       // Class A private
            "172.16.0.1"      // Class B private
        };
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        foreach (var ip in privateAndLocalIps)
        {
            // Act
            SampleAppClient.DefaultRequestHeaders.Remove("X-Forwarded-For");
            SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            var response = await SampleAppClient.GetAsync("/api/pets");

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
        var config = new Dictionary<string, object>
        {
            ["allowedIpAddresses"] = new[] { "123.123.123.123" },
            ["block"] = true,
        };
        await MockServerClient.PostAsJsonAsync("/api/runtime/config", config);
        await UpdateFirewallLists(
            new List<string> { "124.124.124.124" },
            "bad-bot"
        );
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        SampleAppClient.DefaultRequestHeaders.Add("X-Forwarded-For", "125.125.125.125");
        SampleAppClient.DefaultRequestHeaders.Add("User-Agent", "bad-bot");

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
