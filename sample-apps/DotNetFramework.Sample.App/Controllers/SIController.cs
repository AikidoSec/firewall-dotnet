using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace DotNetFramework.Sample.App.Controllers
{
	[RoutePrefix("api/si")]
	public class SIController : ApiController
	{
		private readonly Dictionary<string, (DbProviderFactory factory, DbSIConfig config)> databases;

		public SIController()
		{
			databases = new Dictionary<string, (DbProviderFactory factory, DbSIConfig config)>
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
				{ "Sqlite", (SQLiteFactory.Instance, new DbSIConfig
				{
					Name = "Sqlite", 
					ConnectionString = "Data Source=:memory:",
					InjectionQuery = "SELECT * FROM Users WHERE Name = '{0}'",
					CreateTableQuery = "CREATE TABLE IF NOT EXISTS Users (Id INTEGER, Name TEXT)",
					CreateDbIfNotExistsQuery = "",
					SeedTableQuery = "INSERT INTO Users VALUES (1, 'Admin')",
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
		}

		// GET api/si/{dbType}?name=value
		[Route("{dbType}")]
		public async Task<string> Get(string dbType)
		{
			if (!databases.ContainsKey(dbType))
			{
				return "Unsupported database type";
			}

			var name = GetName();
			var dbConfig = databases[dbType];
			string output = string.Empty;

			using (var connection = dbConfig.factory.CreateConnection())
			{
				if (connection == null)
					throw new NullReferenceException("connection is null");

				connection.ConnectionString = dbConfig.config.ConnectionString;
				await connection.OpenAsync();

				if (!string.IsNullOrEmpty(dbConfig.config.CreateDbIfNotExistsQuery))
				{
					using (var command = connection.CreateCommand())
					{
						command.CommandText = dbConfig.config.CreateDbIfNotExistsQuery;
						await command.ExecuteNonQueryAsync();
					}
				}

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
			var name = Request.GetQueryNameValuePairs()
				.FirstOrDefault(q => q.Key == "name")
				.Value ?? "";
			return name;
		}
	}
}
