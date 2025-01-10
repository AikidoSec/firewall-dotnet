using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace SqlServerSampleApp
{
    public class DatabaseService
    {

        public static string ConnectionString;

        /// <summary>
        /// Creates and returns a connection to the SQL Server database.
        /// </summary>
        /// <returns>A SqlConnection object</returns>
        private static SqlConnection CreateDataConn()
        {
            try
            {
                return new SqlConnection(ConnectionString);
            }
            catch (SqlException e)
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
                    using (var cmd = new SqlCommand("SELECT * FROM dbo.pets;", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(reader.GetOrdinal("pet_id"));
                                string name = reader.GetString(reader.GetOrdinal("pet_name"));
                                string owner = reader.GetString(reader.GetOrdinal("owner"));
                                pets.Add(new Pet(id, name, owner));
                            }
                        }
                    }
                }
            }
            catch (SqlException)
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
                    using (var cmd = new SqlCommand($"SELECT * FROM dbo.pets WHERE pet_id={id}", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int petId = reader.GetInt32(reader.GetOrdinal("pet_id"));
                                string name = reader.GetString(reader.GetOrdinal("pet_name"));
                                string owner = reader.GetString(reader.GetOrdinal("owner"));
                                return new Pet(petId, name, owner);
                            }
                        }
                    }
                }
            }
            catch (SqlException)
            {
                // Handle exception
            }
            return new Pet(0, "Unknown", "Unknown");
        }

        /// <summary>
        /// Creates a new pet with the given name in the database.
        /// </summary>
        /// <param name="petName">The name of the pet</param>
        /// <returns>The number of rows affected</returns>
        public static int CreatePetByName(string petName)
        {
            string sql = $"INSERT INTO dbo.pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException)
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

            // Create database if it doesn't exist
            var createDbCommand = new SqlCommand(
                @"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'test')
                BEGIN
                    CREATE DATABASE test;
                END", connection);
            await createDbCommand.ExecuteNonQueryAsync();

            // Switch to test database
            await connection.ChangeDatabaseAsync("test");

            // Create table if it doesn't exist
            var setupTableCommand = new SqlCommand(
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'pets' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    CREATE TABLE pets (
                        pet_id INT IDENTITY(1,1) PRIMARY KEY,
                        pet_name NVARCHAR(100) NOT NULL,
                        owner NVARCHAR(100) NOT NULL
                    );
                END", connection);
            await setupTableCommand.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Represents a pet with an ID, name, and owner.
    /// </summary>
    public record Pet(int Id, string Name, string Owner);

    /// <summary>
    /// Represents the data needed to create a new pet.
    /// </summary>
    /// <param name="name"></param>
    public record PetCreate(string Name);
}

