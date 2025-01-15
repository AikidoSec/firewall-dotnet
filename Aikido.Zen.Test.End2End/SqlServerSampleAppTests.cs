using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using SampleApp.Common;
using SqlServerSampleApp;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class SqlServerSampleAppTests : WebApplicationTestBase
{
    private const string ProjectDirectory = "e2e/sample-apps/SqlServerSampleApp";
    private IContainer? _sqlServerContainer;

    private WebApplicationFactory<SqlServerStartup> CreateSampleAppFactory()
    {
        var factory = new WebApplicationFactory<SqlServerStartup>()
            .WithWebHostBuilder(builder =>
            {
                //add env variables from base.SampleAppEnvironmentVariables
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
        _sqlServerContainer = await CreateSqlServerContainer();
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
        }
        catch (AikidoException ex)
        {
            Assert.That(ex.Message, Does.Contain("SQL injection detected"));
        }

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
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
}
