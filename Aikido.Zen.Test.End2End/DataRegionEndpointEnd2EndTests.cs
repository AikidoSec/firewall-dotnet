using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.DotNetCore;
using Aikido.Zen.Server.Mock;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SQLiteSampleApp;

namespace Aikido.Zen.Test.End2End;

[TestFixture]
public class DataRegionEndpointEnd2EndTests
{
    private const string RegionToken = "AIK_RUNTIME_1_1_US_e2eregiontest";

    private readonly Dictionary<string, string?> _originalEnvironment = new();
    private WebApplicationFactory<MockServerStartup> _mockServerFactory = null!;
    private HttpClient _mockServerClient = null!;
    private HttpClient _sampleAppClient = null!;
    private RecordingAuthRewriteHandler _recordingHandler = null!;
    private string _mockServerToken = null!;

    [SetUp]
    public async Task SetUp()
    {
        CaptureAndSetEnvironment("AIKIDO_TOKEN", RegionToken);
        CaptureAndSetEnvironment("AIKIDO_ENDPOINT", null);
        CaptureAndSetEnvironment("AIKIDO_REALTIME_ENDPOINT", null);
        CaptureAndSetEnvironment("AIKIDO_BLOCK", "false");
        CaptureAndSetEnvironment("AIKIDO_DISABLE", null);

        Agent.Instance.ClearContext();

        _mockServerFactory = new WebApplicationFactory<MockServerStartup>();
        _recordingHandler = new RecordingAuthRewriteHandler(RegionToken, () => _mockServerToken);
        _mockServerClient = _mockServerFactory.CreateDefaultClient(_recordingHandler, new DecompressionDelegatingHandler());

        var tokenResponse = await _mockServerClient.PostAsync("/api/runtime/apps", null);
        tokenResponse.EnsureSuccessStatusCode();
        _mockServerToken = (await tokenResponse.Content.ReadFromJsonAsync<IDictionary<string, string>>())?["token"]!;
        _mockServerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_mockServerToken);
    }

    [TearDown]
    public void TearDown()
    {
        _sampleAppClient?.Dispose();
        _mockServerClient?.Dispose();
        _mockServerFactory?.Dispose();
        _recordingHandler?.Dispose();

        foreach (var envVar in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
        }
    }

    [Test, NonParallelizable]
    public async Task StartupReporting_ShouldUseRegionSpecificGuardEndpoint_WhenEndpointIsNotSet()
    {
        Assert.That(EnvironmentHelper.AikidoRealtimeUrl, Is.EqualTo("https://guard.us.aikido.dev"));

        _sampleAppClient = CreateSampleAppClient();

        using var response = await _sampleAppClient.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var events = await WaitForMockServerEvents();
        Assert.That(events.Any(evt => evt.TryGetValue("type", out var type) && type.GetString() == "started"), Is.True);

        Assert.That(
            _recordingHandler.RegionTokenRequestUris.Any(uri =>
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host == "guard.us.aikido.dev" &&
                uri.AbsolutePath == "/api/runtime/events"),
            Is.True);

        Assert.That(
            _recordingHandler.RegionTokenRequestUris.Any(uri =>
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host == "guard.us.aikido.dev" &&
                uri.AbsolutePath == "/api/runtime/firewall/lists"),
            Is.True);
    }

    private HttpClient CreateSampleAppClient()
    {
        return new WebApplicationFactory<SQLiteStartup>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddZenFirewall(options => options.UseHttpClient(_mockServerClient));
                });
            })
            .CreateClient();
    }

    private async Task<List<Dictionary<string, JsonElement>>> WaitForMockServerEvents()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var events = await _mockServerClient.GetFromJsonAsync<List<Dictionary<string, JsonElement>>>("/api/runtime/events");
            if (events?.Any() == true)
            {
                return events;
            }

            await Task.Delay(100);
        }

        return new List<Dictionary<string, JsonElement>>();
    }

    private void CaptureAndSetEnvironment(string name, string? value)
    {
        if (!_originalEnvironment.ContainsKey(name))
        {
            _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }

    private sealed class RecordingAuthRewriteHandler : DelegatingHandler
    {
        private readonly string _regionToken;
        private readonly Func<string> _replacementToken;

        public RecordingAuthRewriteHandler(string regionToken, Func<string> replacementToken)
        {
            _regionToken = regionToken;
            _replacementToken = replacementToken;
        }

        public List<Uri> RegionTokenRequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization?.ToString() == _regionToken)
            {
                RegionTokenRequestUris.Add(request.RequestUri!);
                request.Headers.Authorization = new AuthenticationHeaderValue(_replacementToken());
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
