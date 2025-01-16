using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using SampleApp.Common.Models;

namespace MySqlSampleApp
{
    public class DatabaseService
    {
        public static string ConnectionString;

        /// <summary>
        /// Creates and validates a new MySQL connection
        /// </summary>
        /// <returns>A validated MySQL connection</returns>
        /// <exception cref="InvalidOperationException">Thrown when connection string is invalid or connection fails</exception>
        private static MySqlConnection CreateDataConn()
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new InvalidOperationException("Connection string not configured");
            }

            return new MySqlConnection(ConnectionString);
        }

        public static List<Pet> GetAllPets()
        {
            var pets = new List<Pet>();
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT * FROM pets;", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32("pet_id");
                                string name = reader.GetString("pet_name");
                                pets.Add(new Pet(id, name));
                            }
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                // Handle exception
            }
            return pets;
        }

        public static Pet GetPetById(int id)
        {
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand($"SELECT * FROM pets WHERE pet_id={id}", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int petId = reader.GetInt32("pet_id");
                                string name = reader.GetString("pet_name");
                                return new Pet(petId, name);
                            }
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                // Handle exception
            }
            return new Pet(0, "Unknown");
        }

        public static int CreatePetByName(string petName)
        {
            string sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (MySqlException)
            {
                // Handle exception
            }
            return 0;
        }

        public static async Task EnsureDatabaseSetupAsync()
        {
            try
            {
                using var connection = CreateDataConn();
                await connection.OpenAsync();

                // Verify connection is working
                using var testCmd = new MySqlCommand("SELECT 1;", connection);
                await testCmd.ExecuteScalarAsync();

                var setupPetsTableCommand = new MySqlCommand(
                    @"CREATE TABLE IF NOT EXISTS pets (
                        pet_id INT AUTO_INCREMENT PRIMARY KEY,
                        pet_name VARCHAR(100) NOT NULL,
                        owner VARCHAR(100) NOT NULL
                    );", connection);
                await setupPetsTableCommand.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize database: {ex.Message}", ex);
            }
        }
    }
}
