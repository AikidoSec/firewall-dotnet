using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using MySql.Data.MySqlClient;
using System.Data.Common;
using DotNetCore.Sample.App;
using Aikido.Zen.DotNetCore;
using System.Net;
using RestSharp;


/// <summary>
/// Creates and configures the web application
/// </summary>
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddZenFireWall();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
// use the firewall
if (Environment.GetEnvironmentVariable("AIKIDO_ZEN_OFF") != "true")
    app.UseZenFireWall();

/// <summary>
/// Dictionary containing database configurations for different database types
/// </summary>
var databases = new Dictionary<string, (DbProviderFactory factory, DbSIConfig config)>
{
	{ "SqlServer", (SqlClientFactory.Instance, new DbSIConfig 
	{
		Name = "SqlServer",
		ConnectionString = "Server=localhost,27014;Database=master;User Id=sa;Password=Password123!;TrustServerCertificate=True;",
		InjectionQuery = "SELECT * FROM Users WHERE Name = '{0}'",
		CreateTableQuery = "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users') CREATE TABLE Users (Id int, Name nvarchar(50))",
		CreateDbIfNotExistsQuery = "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'UsersDb') CREATE DATABASE UsersDb",
		SeedTableQuery = "IF NOT EXISTS (SELECT * FROM Users) INSERT INTO Users VALUES (1, 'Admin')",
		DropTableQuery = "IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users') DROP TABLE Users"
	})},
	{ "Sqlite", (SqliteFactory.Instance, new DbSIConfig
	{
		Name = "Sqlite", 
		ConnectionString = "Data Source=:memory:",
		InjectionQuery = "SELECT * FROM Users WHERE Name = '{0}'",
		CreateTableQuery = "CREATE TABLE IF NOT EXISTS Users (Id INTEGER, Name TEXT)",
		CreateDbIfNotExistsQuery = "", // Not needed for SQLite in-memory
		SeedTableQuery = "INSERT INTO Users VALUES (1, 'Admin')",
		DropTableQuery = "DROP TABLE IF EXISTS Users"
	})},
	{ "Postgres", (NpgsqlFactory.Instance, new DbSIConfig
	{
		Name = "Postgres",
		ConnectionString = "Host=localhost;Port=27016;Database=main_db;Username=root;Password=password;Include Error Detail=true",
		InjectionQuery = "SELECT * FROM Users WHERE Name = '{0}'",
		CreateTableQuery = "CREATE TABLE IF NOT EXISTS Users (Id int, Name varchar(50))",
		CreateDbIfNotExistsQuery = "", // Postgres cannot create DB within transaction
		SeedTableQuery = "INSERT INTO Users VALUES (1, 'Admin') ON CONFLICT DO NOTHING",
		DropTableQuery = "DROP TABLE IF EXISTS Users"
	})},
	{ "MySql", (MySqlClientFactory.Instance, new DbSIConfig
	{
		Name = "MySql",
		ConnectionString = "Server=localhost;Port=27015;Database=catsdb;User Id=root;Password=mypassword;Allow User Variables=true",
		InjectionQuery = "SELECT * FROM Users WHERE Name = '{0}'",
		CreateTableQuery = "CREATE TABLE IF NOT EXISTS Users (Id int, Name varchar(50))",
		CreateDbIfNotExistsQuery = "CREATE DATABASE IF NOT EXISTS catsdb",
		SeedTableQuery = "INSERT IGNORE INTO Users VALUES (1, 'Admin')",
		DropTableQuery = "DROP TABLE IF EXISTS Users"
	})}
};


/// <summary>
/// Maps GET endpoint that demonstrates outbound HTTP requests using HttpClient
/// </summary>
/// <param name="context">The HTTP context for the request</param>
/// <param name="domainName">The domain name to send the request to</param>
/// <returns>The HTTP response from the request</returns>
app.MapGet("outbound/httpclient/{domainName}", async (HttpContext context, string domainName) =>
{
	domainName = Uri.UnescapeDataString(domainName);
    var client = new HttpClient();
    client.BaseAddress = new Uri(domainName);
    return await client.GetAsync("", HttpCompletionOption.ResponseHeadersRead);
});

/// <summary>
/// Maps GET endpoint that demonstrates outbound HTTP requests using WebRequest
/// </summary>
/// <param name="context">The HTTP context for the request</param>
/// <param name="domainName">The domain name to send the request to</param>
/// <returns>The HTTP response from the request</returns>
app.MapGet("outbound/webrequest/{domainName}", async (HttpContext context, string domainName) =>
{
	domainName = Uri.UnescapeDataString(domainName);
    var request = WebRequest.Create(domainName);
    // only HEAD
    request.Method = "HEAD";
    return await request.GetResponseAsync();
});

/// <summary>
/// Maps GET endpoint that demonstrates outbound HTTP requests using RestSharp
/// </summary>
/// <param name="context">The HTTP context for the request</param>
/// <param name="domainName">The domain name to send the request to</param>
/// <returns>The HTTP response from the request</returns>
app.MapGet("outbound/restsharp/{domainName}", async (HttpContext context, string domainName) =>
{
	domainName = Uri.UnescapeDataString(domainName);
    var client = new RestClient(domainName);
    var request = new RestRequest();
    request.CompletionOption = HttpCompletionOption.ResponseHeadersRead;
    return await client.ExecuteAsync(request);
});

/// <summary>
/// Maps GET endpoint that demonstrates SQL injection vulnerability
/// </summary>
/// <param name="context">The HTTP context for the request</param>
/// <param name="dbType">The type of database to query</param>
app.MapGet("/inject/{dbType}", async (HttpContext context, string dbType) =>
{
	if (!databases.ContainsKey(dbType))
	{
		await context.Response.WriteAsync("Unsupported database type");
		return;
	}

	var result = await ExecuteQuery(context, dbType);
	await context.Response.WriteAsync(result);
}).WithName("InjectDatabase");

/// <summary>
/// Executes a SQL query against the specified database type
/// </summary>
/// <param name="context">The HTTP context containing the query parameters</param>
/// <param name="dbType">The type of database to query</param>
/// <returns>A string containing the query results</returns>
async Task<string> ExecuteQuery(HttpContext context, string dbType)
{
	var name = GetName(context);
	var dbConfig = databases[dbType];
	string output = string.Empty;

	using (var connection = dbConfig.factory.CreateConnection())
	{
		if (connection == null)
			throw new NullReferenceException("connection is null");

		connection.ConnectionString = dbConfig.config.ConnectionString;
		await connection.OpenAsync();

		// create db if needed
		if (!string.IsNullOrEmpty(dbConfig.config.CreateDbIfNotExistsQuery))
		{
			using (var command = connection.CreateCommand())
			{
				command.CommandText = dbConfig.config.CreateDbIfNotExistsQuery;
				await command.ExecuteNonQueryAsync();
			}
		}

		// create and seed table
		using (var command = connection.CreateCommand())
		{
			command.CommandText = dbConfig.config.CreateTableQuery;
			await command.ExecuteNonQueryAsync();
		}
		using (var command = connection.CreateCommand())
		{
			command.CommandText = dbConfig.config.SeedTableQuery;
			await command.ExecuteNonQueryAsync();
		}

		// execute injection query
		using (var command = connection.CreateCommand())
		{
			command.CommandText = string.Format(dbConfig.config.InjectionQuery, name);
			using (var reader = await command.ExecuteReaderAsync())
			{
				if (reader.HasRows)
				{
					while (await reader.ReadAsync())
					{
						output += $"User: {reader["Id"]}, {reader["Name"]}\n";
					}
				}
				else
				{
					output = "No user found";
				}
			}
		}

		// cleanup
		using (var command = connection.CreateCommand())
		{
			command.CommandText = dbConfig.config.DropTableQuery;
			await command.ExecuteNonQueryAsync();
		}
	}

	return output;
}

/// <summary>
/// Gets the name parameter from the HTTP context query string
/// </summary>
/// <param name="context">The HTTP context containing the query parameters</param>
/// <returns>The name parameter value</returns>
/// <exception cref="NullReferenceException">Thrown when context or context.Request is null</exception>
string GetName(HttpContext context)
{
	if (context == null || context.Request == null)
		throw new NullReferenceException("context or context.Request is null");

	string name = context.Request.Query["name"].ToString() ?? ""; // Potentially unsafe input
	return name;
}

app.Run();
