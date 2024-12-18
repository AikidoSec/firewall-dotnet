using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using MySql.Data.MySqlClient;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCore.Sample.App.Controllers
{
    [ApiController]
    [Route("database")]
    public class DatabaseController : ControllerBase
    {
        private readonly Dictionary<string, (DbProviderFactory factory, DbSIConfig config)> _databases =
            new Dictionary<string, (DbProviderFactory factory, DbSIConfig config)>
        {
        { "SqlServer", (SqlClientFactory.Instance, new DbSIConfig
            {
                Name = "SqlServer",
                ConnectionString = "Server=localhost,27014;Database=YourDatabaseName;User Id=sa;Password=Strong@Password123!;TrustServerCertificate=True;",
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

        public DatabaseController()
        {

        }

        [HttpGet("inject/{dbType}")]
        public async Task<IActionResult> InjectDatabase(string dbType)
        {
            if (!_databases.ContainsKey(dbType))
            {
                return BadRequest("Unsupported database type");
            }

            var result = await ExecuteQuery(dbType);
            return Ok(result);
        }

        private async Task<string> ExecuteQuery(string dbType)
        {
            var name = GetName();
            var dbConfig = _databases[dbType];

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

        private string GetName()
        {
            string name = Request.Query["name"].ToString() ?? "";
            return name;
        }
    }
}
