using System.Diagnostics;
using NUnit.Framework;
using Polly;

namespace Aikido.Zen.Test.End2End;

public abstract class BaseAppTests
{
    protected readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const int MockServerPort = 5001;
    protected const int SampleAppPort = 5002;

    protected async Task StartMockServer()
    {
        // Start the Mock Server project directly using dotnet run
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project e2e/Aikido.Zen.Server.Mock --urls=http://localhost:{MockServerPort}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        Process.Start(startInfo);
        await WaitForServiceToBeReady(MockServerPort);
    }

    protected async Task StartSampleApp(string projectDirectory, Dictionary<string, string>? envVars = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {projectDirectory} --urls=http://localhost:{SampleAppPort}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Set environment variables in the process
        startInfo.EnvironmentVariables["AIKIDO_DEBUG"] = "true";
        startInfo.EnvironmentVariables["AIKIDO_BLOCKING"] = "true";
        startInfo.EnvironmentVariables["AIKIDO_MOCK_SERVER_URL"] = $"http://localhost:{MockServerPort}";

        if (envVars != null)
        {
            foreach (var (key, value) in envVars)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        Process.Start(startInfo);
        await WaitForServiceToBeReady(SampleAppPort);
    }

    private async Task WaitForServiceToBeReady(int port)
    {
        var policy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(30,
                retryAttempt => TimeSpan.FromSeconds(2),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"Attempt {retryCount}: Waiting for service to be ready...");
                });

        await policy.ExecuteAsync(async () =>
        {
            using var response = await _client.GetAsync($"http://localhost:{port}/health");
            response.EnsureSuccessStatusCode();
            return response;
        });
    }

    protected async Task EnsureDatabaseContainersAreUp()
    {
        // Check if docker-compose is running
        var checkInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "ps --filter name=sample-apps-sqlserver-1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        var checkProcess = Process.Start(checkInfo);
        var output = await checkProcess!.StandardOutput.ReadToEndAsync();
        checkProcess.WaitForExit();

        // Start docker-compose if SQL Server is not running
        if (!output.Contains("sample-apps-sqlserver-1"))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = "-f sample-apps/docker-compose.yaml up -d",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            process?.WaitForExit();

            // Give SQL Server time to start
            await Task.Delay(10000);
        }
    }

    protected async Task StopDatabaseContainers()
    {
        var stopInfo = new ProcessStartInfo
        {
            FileName = "docker-compose",
            Arguments = "-f sample-apps/docker-compose.yaml down",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(stopInfo);
        process?.WaitForExit();
    }

    [OneTimeSetUp]
    public virtual async Task Setup()
    {
        await StartMockServer();
    }

    [OneTimeTearDown]
    public virtual async Task TearDown()
    {
        // Clean up environment variables
        Environment.SetEnvironmentVariable("AIKIDO_DEBUG", null);
        Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", null);
        Environment.SetEnvironmentVariable("AIKIDO_MOCK_SERVER_URL", null);
    }
}
