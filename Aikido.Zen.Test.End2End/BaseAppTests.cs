using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using NUnit.Framework;
using Polly;

namespace Aikido.Zen.Test.End2End;

/// <summary>
/// Base class for end-to-end tests that require containerized database and application services
/// </summary>
public abstract class BaseAppTests
{
    protected HttpClient Client = new();
    protected IContainer? AppContainer;
    protected IContainer? MockServerContainer;
    private readonly List<IContainer> _dbContainers = new();
    private const int MockServerPort = 5003;
    private const int AppPort = 5002;
    private const string NetworkName = "test-network";
    private readonly INetwork Network;
    private bool UseNat => Environment.GetEnvironmentVariable("USE_NAT") == "true";


    private string WorkDirectory => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROOT_DIR"))
        ? Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("ROOT_DIR"), "..", "..", "..", ".."))
        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".."));

    protected abstract string ProjectDirectory { get; }
    protected virtual Dictionary<string, string> DefaultEnvironmentVariables => new()
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Development",
        ["DOTNET_ENVIRONMENT"] = "Development",
        ["AIKIDO_DISABLED"] = "false",
        ["AIKIDO_DEBUG"] = "true",
        ["AIKIDO_BLOCKING"] = "true",
        ["AIKIDO_REALTIME_URL"] = $"http://localhost:{MockServerPort}",
        ["AIKIDO_URL"] = $"http://localhost:{MockServerPort}",
        ["AIKIDO_TOKEN"] = "test",
        ["ASPNETCORE_URLS"] = $"http://+:{AppPort}"
    };

    protected BaseAppTests()
    {
        // Set network driver based on environment
        TestContext.WriteLine($"::notice::Setting network driver to {(UseNat ? "nat" : "bridge")}");
        Network = new NetworkBuilder()
            .WithCreateParameterModifier(parameter => parameter.Driver = UseNat ? "nat" : "bridge")
            .WithName(NetworkName)
            .Build();
        Network.CreateAsync().Wait();
        InitializeDatabaseContainers();
        Trace.Listeners.Add(new ConsoleTraceListener());
    }

    private void InitializeDatabaseContainers()
    {
        // SQL Server
        var sqlServer = new ContainerBuilder()
            .WithNetwork(Network)
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SA_PASSWORD", "Strong@Password123!")
            .WithEnvironment("MSSQL_PID", "Express")
            .WithExposedPort(1433)
            .WithPortBinding(1433, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(1433))
            .WithName("sql-server-test-server")
            .Build();
        _dbContainers.Add(sqlServer);

        // MongoDB
        var mongodb = new ContainerBuilder()
            .WithNetwork(Network)
            .WithImage("mongo:5")
            .WithEnvironment("MONGO_INITDB_ROOT_USERNAME", "root")
            .WithEnvironment("MONGO_INITDB_ROOT_PASSWORD", "password")
            .WithExposedPort(27017)
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(27017))
            .WithName("mongodb-test-server")
            .Build();
        _dbContainers.Add(mongodb);

        // PostgreSQL
        var postgres = new ContainerBuilder()
            .WithNetwork(Network)
            .WithImage("postgres:14-alpine")
            .WithEnvironment("POSTGRES_PASSWORD", "password")
            .WithEnvironment("POSTGRES_USER", "root")
            .WithEnvironment("POSTGRES_DB", "main_db")
            .WithExposedPort(5432)
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(5432))
            .WithName("postgres-test-server")
            .Build();
        _dbContainers.Add(postgres);

        // MySQL
        var mysql = new ContainerBuilder()
            .WithNetwork(Network)
            .WithImage("mysql:8.0")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", "mypassword")
            .WithEnvironment("MYSQL_DATABASE", "catsdb")
            .WithCommand("--default-authentication-plugin=mysql_native_password")
            .WithExposedPort(3306)
            .WithPortBinding(3306, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(3306))
            .WithName("mysql-test-server")
            .Build();
        _dbContainers.Add(mysql);
    }

    protected async Task StartMockServer()
    {
        try
        {
            // Debug logging for paths
            TestContext.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
            TestContext.WriteLine($"Work Directory: {WorkDirectory}");
            Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"Work Directory: {WorkDirectory}");

            // Start the mock server as a local process
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project e2e/Aikido.Zen.Server.Mock --urls http://+:{MockServerPort}",
                WorkingDirectory = Path.Combine(WorkDirectory, "e2e/Aikido.Zen.Server.Mock"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for the server to be ready
            await Task.Delay(5000); // Adjust this delay as needed
        }
        catch (Exception e)
        {
            var exception = new Exception($"Current Directory: {Directory.GetCurrentDirectory()} Work Directory: {WorkDirectory}, {e.Message}", e);
            throw exception;
        }
    }

    protected async Task StartSampleApp(Dictionary<string, string>? additionalEnvVars = null, string dbType = "sqlite", string dotnetVersion = "8.0")
    {
        var containerEnvVars = new Dictionary<string, string>(DefaultEnvironmentVariables);

        _ = dbType switch
        {
            "sqlite" => containerEnvVars["ConnectionStrings__Sqlite"] = ":memory:",
            "sqlserver" => containerEnvVars["ConnectionStrings__DefaultConnection"] = $"Server=localhost,1433;Database=master;User=sa;Password=Strong@Password123!;TrustServerCertificate=true",
            "mongodb" => containerEnvVars["ConnectionStrings__MongoDB"] = $"mongodb://root:password@localhost:27017",
            "postgres" => containerEnvVars["ConnectionStrings__PostgresConnection"] = $"Host=localhost;Port=5432;Database=main_db;Username=root;Password=password",
            "mysql" => containerEnvVars["ConnectionStrings__MySqlConnection"] = "Server=localhost;Port=3306;Database=catsdb;User=root;Password=mypassword;Allow User Variables=true",
            _ => ""
        };

        if (additionalEnvVars != null)
        {
            foreach (var (key, value) in additionalEnvVars)
            {
                containerEnvVars[key] = value;
            }
        }

        // Start the sample app as a local process
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project {ProjectDirectory} --urls http://+:{AppPort} --framework net{dotnetVersion}",
            WorkingDirectory = Path.Combine(WorkDirectory, ProjectDirectory),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var envVar in containerEnvVars)
        {
            startInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
        }

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for the app to be ready
        await Task.Delay(5000); // Adjust this delay as needed

        Client = new HttpClient { BaseAddress = new Uri($"http://localhost:{AppPort}") };
    }

    public async Task InitializeAsync()
    {
        // Start mock server first
        await StartMockServer();

        // Start all database containers in parallel
        await Task.WhenAll(_dbContainers.Select(c => c.StartAsync()));
    }

    public async Task DisposeAsync()
    {
        if (AppContainer != null)
            await AppContainer.DisposeAsync();

        if (MockServerContainer != null)
            await MockServerContainer.DisposeAsync();

        // Dispose all database containers in parallel
        await Task.WhenAll(_dbContainers.Select(c => c.DisposeAsync().AsTask()));

        Client.Dispose();

        // Clean up network
        await Network.DisposeAsync();
    }

    protected static StringContent CreateJsonContent(object data)
    {
        return new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            "application/json"
        );
    }
}
