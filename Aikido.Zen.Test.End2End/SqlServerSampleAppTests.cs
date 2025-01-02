using System.Text;
using System.Text.Json;
using NUnit.Framework;
using System.Net;

namespace Aikido.Zen.Test.End2End;

public class SqlServerSampleAppTests : BaseAppTests
{
    private const string ProjectDirectory = "e2e/sample-apps/SqlServerSampleApp";

    [OneTimeSetUp]
    public override async Task Setup()
    {
        await base.Setup();

        var envVars = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost,27014;Database=master;User Id=sa;Password=Strong@Password123!;TrustServerCertificate=True"
        };

        await EnsureDatabaseContainersAreUp();
        
        // Start the sample app
        await StartSampleApp(ProjectDirectory, envVars);
    }

    [OneTimeTearDown]
    public override async Task TearDown()
    {
        await base.TearDown();
        // await StopDatabaseContainers();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task BlocksInBlockingMode()
    {
        var sqlInjectionContent = new StringContent(
            JsonSerializer.Serialize(new { name = "Test'), ('Test2');--" }), 
            Encoding.UTF8, 
            "application/json"
        );
        
        var normalContent = new StringContent(
            JsonSerializer.Serialize(new { name = "Miau" }), 
            Encoding.UTF8, 
            "application/json"
        );

        // Act
        var sqlInjectionResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/add", sqlInjectionContent);
        var normalAddResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/add", normalContent);

        // Assert
        Assert.That(sqlInjectionResponse.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(normalAddResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Get logs from the sample app process
        var logs = await _client.GetStringAsync($"http://localhost:{SampleAppPort}/logs");
        
        Assert.That(logs, Contains.Substring("Starting agent"));
        Assert.That(logs, Contains.Substring("Zen has blocked an SQL injection"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task DoesNotBlockInDryMode()
    {
        // Start a new instance of the app with blocking disabled
        var envVars = new Dictionary<string, string>
        {
            ["AIKIDO_BLOCKING"] = "false",
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost,27014;Database=master;User Id=sa;Password=Strong@Password123!;TrustServerCertificate=True"
        };

        await StartSampleApp(ProjectDirectory, envVars);

        var sqlInjectionContent = new StringContent(
            JsonSerializer.Serialize(new { name = "Test'), ('Test2');--" }), 
            Encoding.UTF8, 
            "application/json"
        );
        
        var normalContent = new StringContent(
            JsonSerializer.Serialize(new { name = "Miau" }), 
            Encoding.UTF8, 
            "application/json"
        );

        // Act
        var sqlInjectionResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/add", sqlInjectionContent);
        var normalAddResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/add", normalContent);

        // Assert
        Assert.That(sqlInjectionResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(normalAddResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Get logs from the sample app process
        var logs = await _client.GetStringAsync($"http://localhost:{SampleAppPort}/logs");
        
        Assert.That(logs, Contains.Substring("Starting agent"));
        Assert.That(!logs.Contains("Zen has blocked an SQL injection"));
    }
}
