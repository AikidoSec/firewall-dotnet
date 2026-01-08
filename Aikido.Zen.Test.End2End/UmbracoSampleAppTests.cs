using System.Net;
using System.Net.Http.Json;
using Aikido.Zen.Core.Exceptions;
using Microsoft.AspNetCore.Mvc.Testing;
using Aikido.Zen.DotNetCore;
using NUnit.Framework;
using UmbracoSampleApp;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class UmbracoSampleAppTests : WebApplicationTestBase
{
    private const string ProjectDirectory = "e2e/sample-apps/UmbracoSampleApp";

    private WebApplicationFactory<UmbracoSampleApp.Program> CreateSampleAppFactory()
    {
        foreach (var envVar in SampleAppEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
        }
        var factory = new WebApplicationFactory<UmbracoSampleApp.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddZenFirewall(options => options.UseHttpClient(MockServerClient));
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
    public async Task TestRoutes_DiscoveryAndNoErrors()
    {
        await SetMode(false, true);
        SampleAppClient = CreateSampleAppFactory().CreateClient();
        var response = await SampleAppClient.GetAsync("/");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        response = await SampleAppClient.GetAsync("/favicon.ico");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
}
