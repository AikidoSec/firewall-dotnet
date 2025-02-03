using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SampleApp.Common.Models;
using Umbraco.Cms.Infrastructure.Scoping;

namespace UmbracoSampleApp
{
    public class DatabaseService
    {
        public static string ConnectionString;

        private readonly IScopeProvider _scopeProvider;

        public DatabaseService(IScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        /// <summary>
        /// Creates and returns a connection to the SQLite database.
        /// </summary>
        /// <returns>A SqliteConnection object</returns>
        private static SqliteConnection CreateDataConn()
        {
            try
            {
                return new SqliteConnection(ConnectionString);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in CreateDataConn(): " + e);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all pets from the database using IScopeProvider.
        /// </summary>
        /// <returns>A list of Pet objects</returns>
        public List<Pet> GetAllPets()
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                var sql = "SELECT * FROM pets;";
                var pets = scope.Database.Fetch<Pet>(sql);
                scope.Complete();
                return pets;
            }
        }

        /// <summary>
        /// Retrieves a pet by its ID from the database using IScopeProvider.
        /// </summary>
        /// <param name="id">The ID of the pet</param>
        /// <returns>A Pet object</returns>
        public Pet GetPetById(int id)
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                var sql = "SELECT * FROM pets WHERE pet_id = @0;";
                var pet = scope.Database.FirstOrDefault<Pet>(sql, id);
                scope.Complete();
                return pet ?? new Pet(0, "Unknown");
            }
        }

        /// <summary>
        /// Creates a new pet with the given name in the database using IScopeProvider.
        /// </summary>
        /// <param name="petName">The name of the pet</param>
        /// <returns>The number of rows affected</returns>
        public int CreatePetByName(string petName)
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                var sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security');";
                var result = scope.Database.Execute(sql, petName);
                scope.Complete();
                return result;
            }
        }

        /// <summary>
        /// Ensures that the database and tables are created using IScopeProvider.
        /// </summary>
        public async Task EnsureDatabaseSetupAsync()
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                var sql = @"CREATE TABLE IF NOT EXISTS pets (
                    pet_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    pet_name TEXT NOT NULL,
                    owner TEXT NOT NULL
                );";
                await scope.Database.ExecuteAsync(sql);
                scope.Complete();
            }
        }
    }
}
