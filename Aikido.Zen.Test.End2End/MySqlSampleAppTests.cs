using System.Text;
using System.Text.Json;
using NUnit.Framework;
using System.Net;

namespace Aikido.Zen.Test.End2End;

public class MySqlSampleAppTests : BaseAppTests
{
    private const string ProjectDirectory = "e2e/sample-apps/MySqlSampleApp";
    private readonly Dictionary<string, string> _environmentVariables = new Dictionary<string, string>
    {
    };

    [SetUp]
    public override async Task Setup()
    {
        await base.Setup();
        await EnsureDatabaseContainersAreUp();
    }

    [TearDown]
    public override async Task TearDown()
    {
        await base.TearDown();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen()
    {
        _environmentVariables["AIKIDO_BLOCKING"] = "true";
        await StartSampleApp(ProjectDirectory, _environmentVariables);

        var safePayload = new StringContent(
            JsonSerializer.Serialize(new { Name = "Bobby" }),
            Encoding.UTF8,
            "application/json"
        );

        var unsafePayload = new StringContent(
            JsonSerializer.Serialize(new { Name = "Malicious Pet', 'Gru from the Minions'); -- " }),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var safeResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/api/pets/create", safePayload);
        var body = await safeResponse.Content.ReadAsStringAsync();
        var unsafeResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/api/pets/create", unsafePayload);

        // Assert
        Assert.That(safeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(unsafeResponse.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithoutZen()
    {
        _environmentVariables["AIKIDO_BLOCKING"] = "false";
        await StartSampleApp(ProjectDirectory, _environmentVariables);

        var safePayload = new StringContent(
            JsonSerializer.Serialize(new { Name = "Bobby" }),
            Encoding.UTF8,
            "application/json"
        );

        var unsafePayload = new StringContent(
            JsonSerializer.Serialize(new { Name = "Malicious Pet', 'Gru from the Minions'); -- " }),
            Encoding.UTF8,
            "application/json"
        );

        // Act
        var safeResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/api/pets/create", safePayload);
        var body = await safeResponse.Content.ReadAsStringAsync();
        var unsafeResponse = await _client.PostAsync($"http://localhost:{SampleAppPort}/api/pets/create", unsafePayload);

        // Assert
        Assert.That(safeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(unsafeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}