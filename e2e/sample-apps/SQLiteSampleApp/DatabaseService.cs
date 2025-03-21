using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SampleApp.Common.Models;

namespace SQLiteSampleApp
{
    public class DatabaseService
    {
        public static string ConnectionString;

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
            catch (SqliteException e)
            {
                Console.WriteLine("Exception in CreateDataConn(): " + e);
            }
            return null;
        }

        /// <summary>
        /// Retrieves all pets from the database.
        /// </summary>
        /// <returns>A list of Pet objects</returns>
        public static List<Pet> GetAllPets()
        {
            var pets = new List<Pet>();
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand("SELECT * FROM pets;", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(reader.GetOrdinal("pet_id"));
                                string name = reader.GetString(reader.GetOrdinal("pet_name"));
                                pets.Add(new Pet(id, name));
                            }
                        }
                    }
                }
            }
            catch (SqliteException)
            {
                // Handle exception
            }
            return pets;
        }

        /// <summary>
        /// Retrieves a pet by its ID from the database.
        /// </summary>
        /// <param name="id">The ID of the pet</param>
        /// <returns>A Pet object</returns>
        public static Pet GetPetById(int id)
        {
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand($"SELECT * FROM pets WHERE pet_id={id}", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int petId = reader.GetInt32(reader.GetOrdinal("pet_id"));
                                string name = reader.GetString(reader.GetOrdinal("pet_name"));
                                return new Pet(petId, name);
                            }
                        }
                    }
                }
            }
            catch (SqliteException)
            {
                // Handle exception
            }
            return new Pet(0, "Unknown");
        }

        /// <summary>
        /// Creates a new pet with the given name in the database.
        /// </summary>
        /// <param name="petName">The name of the pet</param>
        /// <returns>The number of rows affected</returns>
        public static int CreatePetByName(string petName)
        {
            string sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqliteException ex)
            {
                // Handle exception
            }
            return 0;
        }

        /// <summary>
        /// Ensures that the database and tables are created.
        /// </summary>
        public static async Task EnsureDatabaseSetupAsync()
        {
            using var connection = CreateDataConn();
            await connection.OpenAsync();
            var setupPetsTableCommand = new SqliteCommand(
                @"CREATE TABLE IF NOT EXISTS pets (
                    pet_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    pet_name TEXT NOT NULL,
                    owner TEXT NOT NULL
                );", connection);
            await setupPetsTableCommand.ExecuteNonQueryAsync();
        }
    }
}
