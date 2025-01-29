using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using MySqlSampleApp;
using Aikido.Zen.Core.Exceptions;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class MySqlSampleAppTests : WebApplicationTestBase
{
    private const string ProjectDirectory = "e2e/sample-apps/MySqlSampleApp";
    private IContainer? _mysqlContainer;

    private WebApplicationFactory<MySqlStartup> CreateSampleAppFactory()
    {
        var factory = new WebApplicationFactory<MySqlStartup>()
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
        _mysqlContainer = await CreateMySqlContainer();
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "true";
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "true";
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "true";
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "false";
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "false";
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "true";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "true";
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
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "true";
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
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
        SampleAppEnvironmentVariables["AIKIDO_BLOCKING"] = "false";
        var factory = CreateSampleAppFactory();
        var client = factory.CreateClient();
        var maliciousCommand = "ls $(echo)";

        // Act
        var response = await client.GetAsync("/api/pets/command?command=" + maliciousCommand);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("command executed"), "The command injection was unexpectedly blocked.");
    }
}
