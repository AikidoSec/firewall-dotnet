using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net.Http.Json;
using Aikido.Zen.Server.Mock;
using Microsoft.AspNetCore.Hosting;
using SampleApp.Common;

namespace Aikido.Zen.Test.End2End
{
    /// <summary>
    /// Base class for E2E tests using WebApplicationFactory for both mock server and sample apps
    /// </summary>
    public abstract class WebApplicationTestBase
    {
        protected List<IContainer> DbContainers { get; private set; }
        protected HttpClient SampleAppClient { get; set; }
        protected HttpClient MockServerClient { get; private set; }
        protected WebApplicationFactory<Aikido.Zen.Server.Mock.MockServerStartup> MockServerFactory { get; private set; }
        protected string MockServerToken = "test-token";
        protected const int MockServerPort = 3000;

        protected IDictionary<string, string> SampleAppEnvironmentVariables = new Dictionary<string, string>
        {
            ["ConnectionStrings__Sqlite"] = ":memory:",
            ["ConnectionStrings__DefaultConnection"] = $"Server=127.0.0.1,1433;Database=catsdb;User=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true",
            ["ConnectionStrings__MongoDB"] = $"mongodb://root:password@127.0.0.1:27017",
            ["ConnectionStrings__PostgresConnection"] = $"Host=127.0.0.1;Port=5432;Database=catsdb;Username=postgres;Password=YourStrong!Passw0rd",
            ["ConnectionStrings__MySqlConnection"] = "Server=127.0.0.1;Port=3306;Database=catsdb;User=root;Password=YourStrong!Passw0rd;Allow User Variables=true",
        };

        /// <summary>
        /// Sets the Zen firewall mode by configuring environment variables and mock server
        /// </summary>
        /// <param name="disabled">Whether to disable the Zen firewall</param>
        /// <param name="block">Whether to enable blocking mode</param>
        protected async Task SetMode(bool disabled, bool block)
        {
            SampleAppEnvironmentVariables["AIKIDO_DISABLE"] = disabled.ToString().ToLower();
            SampleAppEnvironmentVariables["AIKIDO_BLOCK"] = block.ToString().ToLower();
            await MockServerClient.PostAsync("/api/runtime/config", JsonContent.Create(new { Block = block }));
        }

        [OneTimeSetUp]
        public virtual async Task OneTimeSetUp()
        {
            // Initialize containers list
            DbContainers = new List<IContainer>();

            // Setup database containers based on test needs
            await SetupDatabaseContainers();

            // Setup mock server
            MockServerFactory = new WebApplicationFactory<Aikido.Zen.Server.Mock.MockServerStartup>();
            // create a client for the sample app that handles gzip decompression
            MockServerClient = MockServerFactory.CreateDefaultClient(new DecompressionDelegatingHandler());

            // Get a token from the mock server
            var response = await MockServerClient.PostAsync("/api/runtime/apps", null);
            response.EnsureSuccessStatusCode();
            MockServerToken = (await response.Content.ReadFromJsonAsync<IDictionary<string, string>>())?["token"] ?? "test-token";
            MockServerClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(MockServerToken);

            // Set environment variable for the sample app
            SampleAppEnvironmentVariables["AIKIDO_TOKEN"] = MockServerToken;
            // set the base url for the mock server
            SampleAppEnvironmentVariables["AIKIDO_URL"] = $"http://localhost:{MockServerPort}";
            SampleAppEnvironmentVariables["AIKIDO_REALTIME_URL"] = $"http://localhost:{MockServerPort}";
        }

        [OneTimeTearDown]
        public virtual async Task OneTimeTearDown()
        {
            // Dispose HTTP clients and factories
            SampleAppClient?.Dispose();
            MockServerClient?.Dispose();
            MockServerFactory?.Dispose();

            // Stop and remove database containers
            foreach (var container in DbContainers)
            {
                await container.DisposeAsync();
            }
        }

        /// <summary>
        /// Sets up the required database containers
        /// </summary>
        protected abstract Task SetupDatabaseContainers();


        /// <summary>
        /// Creates a MySQL container
        /// </summary>
        protected async Task<IContainer> CreateMySqlContainer()
        {
            var mysql = new ContainerBuilder()
                .WithImage("mysql:8.0")
                .WithEnvironment("MYSQL_ROOT_PASSWORD", "YourStrong!Passw0rd")
                .WithEnvironment("MYSQL_DATABASE", "catsdb")
                .WithCommand("--default-authentication-plugin=mysql_native_password")
                .WithExposedPort(3306)
                .WithPortBinding(3306, false)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilPortIsAvailable(3306))
                .WithName($"mysql-test-server")
                .Build();

            await mysql.StartAsync();
            DbContainers.Add(mysql);
            return mysql;
        }

        /// <summary>
        /// Creates a PostgreSQL container
        /// </summary>
        protected async Task<IContainer> CreatePostgresContainer()
        {
            var postgres = new ContainerBuilder()
                .WithImage("postgres:latest")
                .WithEnvironment("POSTGRES_PASSWORD", "YourStrong!Passw0rd")
                .WithEnvironment("POSTGRES_DB", "catsdb")
                .WithExposedPort(5432)
                .WithPortBinding(5432, false)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilPortIsAvailable(5432))
                .WithName($"postgres-test-server")
                .Build();

            await postgres.StartAsync();
            DbContainers.Add(postgres);
            return postgres;
        }

        /// <summary>
        /// Creates a SQL Server container
        /// </summary>
        protected async Task<IContainer> CreateSqlServerContainer()
        {

            var sqlServer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SA_PASSWORD", "YourStrong!Passw0rd")
                .WithExposedPort(1433)
                .WithPortBinding(1433, false)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilPortIsAvailable(1433))
                .WithName($"sqlserver-test-server")
                .Build();

            await sqlServer.StartAsync();
            DbContainers.Add(sqlServer);
            return sqlServer;
        }

        /// <summary>
        /// Response model for app creation
        /// </summary>
        private class AppResponse
        {
            /// <summary>
            /// The token for the created app
            /// </summary>
            public string Token { get; set; }
        }
    }

    public class DecompressionDelegatingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.Content.Headers.ContentEncoding.LastOrDefault() == "gzip")
            {
                response.Content = CreateGZipDecompressedContent(response.Content);
            }

            return response;
        }

        private static HttpContent CreateGZipDecompressedContent(HttpContent originalContent)
        {
            var assembly = typeof(HttpClient).Assembly;

            const string typeName = "System.Net.Http.DecompressionHandler+GZipDecompressedContent";
            var gZipDecompressedContentType = assembly.GetType(typeName);
            if (gZipDecompressedContentType == null)
            {
                throw new InvalidOperationException($"Failed to find type '{typeName}'");
            }

            return (HttpContent)Activator.CreateInstance(gZipDecompressedContentType, originalContent);
        }
    }
}
