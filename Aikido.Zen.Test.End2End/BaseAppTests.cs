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
        // Create network first
        Network = new NetworkBuilder()
            .WithCreateParameterModifier(parameterModifier => parameterModifier.Driver = "nat") // Use nat driver for Windows
            .WithName(NetworkName)
            .Build();
        Network.CreateAsync().Wait();
        InitializeDatabaseContainers();
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
        MockServerContainer = new ContainerBuilder()
            .WithNetwork(Network)
            .WithImage("mcr.microsoft.com/dotnet/sdk:8.0")
            .WithBindMount(Path.GetFullPath("..\\..\\..\\..\\"), "/app")
            .WithWorkingDirectory("/app")
            .WithCommand("dotnet", "run", "--project", "e2e/Aikido.Zen.Server.Mock", "--urls", $"http://+:{MockServerPort}")
            .WithExposedPort(MockServerPort)
            .WithPortBinding(MockServerPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/health")
                    .ForPort(MockServerPort)))
            .WithName("mock-server")
            .Build();

        await MockServerContainer.StartAsync();
    }

    protected async Task StartSampleApp(Dictionary<string, string>? additionalEnvVars = null, string dbType = "sqlite", string dotnetVersion = "8.0")
    {
        // Get mapped ports for all services
        var containerEnvVars = new Dictionary<string, string>(DefaultEnvironmentVariables);

        _ = dbType switch
        {
            "sqlite" => containerEnvVars["ConnectionStrings__Sqlite"] = ":memory:",
            "sqlserver" => containerEnvVars["ConnectionStrings__DefaultConnection"] = $"Server=sql-server-test-server,1433;Database=master;User=sa;Password=Strong@Password123!;TrustServerCertificate=true",
            "mongodb" => containerEnvVars["ConnectionStrings__MongoDB"] = $"mongodb://root:password@mongodb-test-server:27017",
            "postgres" => containerEnvVars["ConnectionStrings__PostgresConnection"] = $"Host=postgres-test-server;Port=5432;Database=main_db;Username=root;Password=password",
            "mysql" => containerEnvVars["ConnectionStrings__MySqlConnection"] = "Server=mysql-test-server;Port=3306;Database=catsdb;User=root;Password=mypassword;Allow User Variables=true",
            _ => ""
        };

        // Add additional environment variables
        if (additionalEnvVars != null)
        {
            foreach (var (key, value) in additionalEnvVars)
            {
                containerEnvVars[key] = value;
            }
        }

        AppContainer = new ContainerBuilder()
            .WithNetwork(Network)
            .WithImage($"mcr.microsoft.com/dotnet/sdk:{dotnetVersion}")
            .WithBindMount(Path.GetFullPath("..\\..\\..\\..\\"), "/app")
            .WithWorkingDirectory("/app")
            .WithCommand("dotnet", "run", "--project", ProjectDirectory, "--urls", $"http://+:{AppPort}", "--framework", $"net{dotnetVersion}")
            .WithExposedPort(AppPort)
            .WithPortBinding(AppPort, true)
            .WithEnvironment(containerEnvVars)
            .WithName($"sample-app-{dbType ?? "unknown"}")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/health")
                    .ForPort(AppPort)
                    ))
            .Build();

        await AppContainer.StartAsync();

        var mappedPort = AppContainer.GetMappedPublicPort(AppPort);
        Client = new();
        Client.BaseAddress = new Uri($"http://localhost:{mappedPort}");
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
