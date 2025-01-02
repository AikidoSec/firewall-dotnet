using System.Diagnostics;
using System.Net.Http.Json;
using NUnit.Framework;
using Polly;

namespace Aikido.Zen.Test.End2End;

public abstract class BaseAppTests
{
    protected readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const int MockServerPort = 5001;
    protected const int SampleAppPort = 5002;
    private readonly string _currentDirectory = Directory.GetCurrentDirectory();
    private string _root => Path.GetFullPath(Path.Combine(_currentDirectory, "..\\..\\..\\..\\"));
    private List<Process> _processes = new();

    protected async Task<Process> StartMockServer()
    {
        // Start the Mock Server project directly using dotnet run
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project e2e/Aikido.Zen.Server.Mock --urls http://localhost:{MockServerPort}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _root,
        };

        var process = Process.Start(startInfo);
        _processes.Add(process);
        await WaitForServiceToBeReady(MockServerPort);
        return process;
    }

    protected async Task<Process> StartSampleApp(string projectDirectory, Dictionary<string, string>? envVars = null)
    {

        // Set environment variables in the process
        if (!envVars.ContainsKey("AIKIDO_DISABLED"))
            envVars["AIKIDO_DISABLED"] = "false";
        if (!envVars.ContainsKey("AIKIDO_DEBUG"))
            envVars["AIKIDO_DEBUG"] = "true";
        if (!envVars.ContainsKey("AIKIDO_BLOCKING"))
            envVars["AIKIDO_BLOCKING"] = "true";
        if (!envVars.ContainsKey("AIKIDO_REALTIME_URL"))
            envVars["AIKIDO_REALTIME_URL"] = $"http://localhost:{MockServerPort}";
        if (!envVars.ContainsKey("AIKIDO_URL"))
            envVars["AIKIDO_URL"] = $"http://localhost:{MockServerPort}";
        if (!envVars.ContainsKey("AIKIDO_TOKEN"))
            envVars["AIKIDO_TOKEN"] = "test";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {projectDirectory} --urls http://localhost:{SampleAppPort} {string.Join(" ", envVars.Select(kv => $"{kv.Key}={kv.Value}"))}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _root,
        };

        if (envVars != null)
        {
            foreach (var (key, value) in envVars)
            {
                startInfo.EnvironmentVariables[key] = value;
                startInfo.Environment[key] = value;
            }
        }

        var process = Process.Start(startInfo);
        _processes.Add(process);

        await WaitForServiceToBeReady(SampleAppPort);
        return process;
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
            var body = await response.Content.ReadAsStringAsync();
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
        _processes.Add(checkProcess);
        var output = await checkProcess!.StandardOutput.ReadToEndAsync();
        checkProcess.WaitForExit(1000 * 10);

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
                CreateNoWindow = true,
                WorkingDirectory = _root
            };

            var process = Process.Start(startInfo);
            _processes.Add(process);
            process?.WaitForExit(1000 * 60);
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

    public virtual async Task Setup()
    {
        await StartMockServer();
    }

    public virtual async Task TearDown()
    {
        // Clean up environment variables
        Environment.SetEnvironmentVariable("AIKIDO_DEBUG", null);
        Environment.SetEnvironmentVariable("AIKIDO_BLOCKING", null);
        Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", null);
        Environment.SetEnvironmentVariable("AIKIDO_URL", null);
        // kill all processes
        foreach (var process in _processes)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        // clear the process list
        _processes.Clear();
    }
}
