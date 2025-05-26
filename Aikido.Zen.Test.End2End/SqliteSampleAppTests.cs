using System.Net;
using System.Net.Http.Json;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using SQLiteSampleApp;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class SqliteSampleAppTests : WebApplicationTestBase
{
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

    protected override async Task SetupDatabaseContainers()
    {
        // SQLite uses in-memory database, no container needed
        await Task.CompletedTask;
    }

    [OneTimeSetUp]
    public override async Task OneTimeSetUp()
    {
        await base.OneTimeSetUp();
    }

    [OneTimeTearDown]
    public override async Task OneTimeTearDown()
    {
        await base.OneTimeTearDown();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen_WhenSafePayload_ShouldSucceed()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        var safePayload = new { Name = "Bobby" };

        // Act
        var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", safePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(content, Does.Contain("rows"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen_WhenUnsafePayload_ShouldBlock()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        try
        {
            var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", unsafePayload);
            var content = await response.Content.ReadAsStringAsync();
            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
        catch (AikidoException ex)
        {
            Assert.That(ex.Message, Does.Contain("SQL injection detected"));
        }
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithoutZen_WhenSafePayload_ShouldSucceed()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        var safePayload = new { Name = "Bobby" };

        // Act
        var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", safePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(content, Does.Contain("rows"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithoutZen_WhenUnsafePayload_ShouldNotBlock()
    {
        // Arrange
        await SetMode(false, false);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", unsafePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen_WhenUnsafePayload_AndBlockingDisabled_ShouldNotBlock()
    {
        // Arrange
        await SetMode(false, false);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", unsafePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZenDisabled_WhenUnsafePayload_ShouldNotBlock()
    {
        // Arrange
        await SetMode(true, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        var response = await SampleAppClient.PostAsJsonAsync("/api/pets/create", unsafePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestCommandInjection_WithBlockingEnabled_ShouldBeBlocked()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        var maliciousCommand = "ls $(echo)";

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets/command?command=" + Uri.EscapeDataString(maliciousCommand));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestCommandInjection_WithBlockingDisabled_ShouldNotBeBlocked()
    {
        // Arrange
        await SetMode(false, false);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        var maliciousCommand = "ls $(echo)";

        // Act
        var response = await SampleAppClient.GetAsync("/api/pets/command?command=" + Uri.EscapeDataString(maliciousCommand));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("command executed"), "The command injection was unexpectedly blocked.");
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWebRequest_WithSafeUrl_ShouldSucceed()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/webRequestTest?uri=https://example.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("WebRequest succeeded"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWebRequest_WithLocalhostUrl_ShouldBlock()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/webRequestTest?uri=http://localhost:8080");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Request blocked due to security policy"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWebRequest_WithPrivateIP_ShouldBlock()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/webRequestTest?uri=http://192.168.1.1");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Request blocked due to security policy"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWebRequest_WithBlockingDisabled_ShouldNotBlock()
    {
        // Arrange
        await SetMode(false, false);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/webRequestTest?uri=http://localhost:8080");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("WebRequest succeeded"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestHttpClient_WithSafeUrl_ShouldSucceed()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/httpClientTest?uri=https://example.com");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("HttpClient succeeded"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestHttpClient_WithLocalhostUrl_ShouldBlock()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/httpClientTest?uri=http://localhost:8080");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Request blocked due to security policy"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestHttpClient_WithPrivateIP_ShouldBlock()
    {
        // Arrange
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/httpClientTest?uri=http://192.168.1.1");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Request blocked due to security policy"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestHttpClient_WithBlockingDisabled_ShouldNotBlock()
    {
        // Arrange
        await SetMode(false, false);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/httpClientTest?uri=http://localhost:8080");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("HttpClient succeeded"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWebRequest_WithZenDisabled_ShouldNotBlock()
    {
        // Arrange
        await SetMode(true, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/webRequestTest?uri=http://localhost:8080");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("WebRequest succeeded"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestHttpClient_WithZenDisabled_ShouldNotBlock()
    {
        // Arrange
        await SetMode(true, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();

        // Act
        var response = await SampleAppClient.GetAsync("/api/httpClientTest?uri=http://localhost:8080");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("HttpClient succeeded"));
    }
}
