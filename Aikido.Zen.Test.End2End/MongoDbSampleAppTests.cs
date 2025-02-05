using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class MongoDbSampleAppTests : WebApplicationTestBase
{
    private const string ProjectDirectory = "e2e/sample-apps/MongoDbSampleApp";

    private WebApplicationFactory<MongoDbSampleApp.Program> CreateSampleAppFactory()
    {
        foreach (var envVar in SampleAppEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
        }
        var factory = new WebApplicationFactory<MongoDbSampleApp.Program>();
        return factory;
    }

    protected override async Task SetupDatabaseContainers()
    {
        await CreateMongoDbContainer();
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
    public async Task TestNoSqlInjection_WhenUnsafePayload_ShouldBlock()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();

        // Attempt to exploit NoSQL injection
        var unsafePayload = new { search = "{ \"$ne\": null }" };

        // Act
        var response = await client.GetAsync($"/?search={Uri.EscapeDataString(unsafePayload.search)}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(content, Does.Contain("NoSQL injection detected"));
    }

    [Test]
    public async Task TestNoSqlInjection_WhenSafePayload_ShouldSucceed()
    {
        // Arrange
        SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = "false";
        SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = "true";
        var client = CreateSampleAppFactory().CreateClient();

        var safePayload = new { search = "Bobby" };

        // Act
        var response = await client.GetAsync($"/?search={Uri.EscapeDataString(safePayload.search)}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(content, Does.Contain(""));
    }
}
