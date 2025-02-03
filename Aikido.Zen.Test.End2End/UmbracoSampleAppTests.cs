using System.Net;
using System.Net.Http.Json;
using Aikido.Zen.Core.Exceptions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NUnit.Framework;
using UmbracoSampleApp;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class UmbracoSampleAppTests : WebApplicationTestBase
{
    private const string ProjectDirectory = "e2e/sample-apps/UmbracoSampleApp";

    private WebApplicationFactory<Program> CreateSampleAppFactory()
    {
        foreach (var envVar in SampleAppEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
        }
        var factory = new WebApplicationFactory<Program>();
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();
        var response = await client.GetAsync("/");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        response = await client.GetAsync("/favicon.ico");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen_WhenSafePayload_ShouldSucceed()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();

        var safePayload = new { Name = "Bobby" };

        // Act
        var response = await client.PostAsJsonAsync("/api/pets/create", safePayload);
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        try
        {
            var response = await client.PostAsJsonAsync("/api/pets/create", unsafePayload);
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();

        var safePayload = new { Name = "Bobby" };

        // Act
        var response = await client.PostAsJsonAsync("/api/pets/create", safePayload);
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "false";
        var client = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        var response = await client.PostAsJsonAsync("/api/pets/create", unsafePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen_WhenUnsafePayload_AndBlockingDisabled_ShouldNotBlock()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "false";
        var client = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        var response = await client.PostAsJsonAsync("/api/pets/create", unsafePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZenDisabled_WhenUnsafePayload_ShouldNotBlock()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "true";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();

        var unsafePayload = new { Name = "Malicious Pet', 'Gru from the Minions'); -- " };

        // Act
        var response = await client.PostAsJsonAsync("/api/pets/create", unsafePayload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestCommandInjection_WithBlockingEnabled_ShouldBeBlocked()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();
        var maliciousCommand = "ls $(echo)";

        // Act
        var response = await client.GetAsync("/api/pets/command?command=" + Uri.EscapeDataString(maliciousCommand));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestCommandInjection_WithBlockingDisabled_ShouldNotBeBlocked()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "false";
        var client = CreateSampleAppFactory().CreateClient();
        var maliciousCommand = "ls $(echo)";

        // Act
        var response = await client.GetAsync("/api/pets/command?command=" + Uri.EscapeDataString(maliciousCommand));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("command executed"), "The command injection was unexpectedly blocked.");
    }
}
