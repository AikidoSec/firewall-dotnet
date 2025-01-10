using System.Text;
using System.Text.Json;
using NUnit.Framework;
using System.Net;

namespace Aikido.Zen.Test.End2End;

public class PostgresSampleAppTests : BaseAppTests
{
    protected override string ProjectDirectory => "e2e/sample-apps/PostgresSampleApp";

    [OneTimeSetUp]
    public async Task InitializeAsync()
    {
        await base.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithZen()
    {
        await StartSampleApp(new Dictionary<string, string>
        {
            ["AIKIDO_BLOCKING"] = "true"
        }, "postgres");

        var safePayload = CreateJsonContent(new { Name = "Bobby" });
        var unsafePayload = CreateJsonContent(new { Name = "Malicious Pet', 'Gru from the Minions'); -- " });

        // Act
        var safeResponse = await Client.PostAsync("/api/pets/create", safePayload);
        var body = await safeResponse.Content.ReadAsStringAsync();
        var unsafeResponse = await Client.PostAsync("/api/pets/create", unsafePayload);

        // Assert
        Assert.That(safeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(unsafeResponse.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task TestWithoutZen()
    {
        await StartSampleApp(new Dictionary<string, string>
        {
            ["AIKIDO_BLOCKING"] = "false"
        }, "postgres");

        var safePayload = CreateJsonContent(new { Name = "Bobby" });
        var unsafePayload = CreateJsonContent(new { Name = "Malicious Pet', 'Gru from the Minions'); -- " });

        // Act
        var safeResponse = await Client.PostAsync("/api/pets/create", safePayload);
        var body = await safeResponse.Content.ReadAsStringAsync();
        var unsafeResponse = await Client.PostAsync("/api/pets/create", unsafePayload);

        // Assert
        Assert.That(safeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(unsafeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}