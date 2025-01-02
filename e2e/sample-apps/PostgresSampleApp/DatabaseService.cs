using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace PostgresSampleApp
{
    public class DatabaseService
    {
        public static string ConnectionString;

        private static NpgsqlConnection CreateDataConn()
        {
            try
            {
                return new NpgsqlConnection(ConnectionString);
            }
            catch (NpgsqlException e)
            {
                Console.WriteLine("Exception in CreateDataConn(): " + e);
            }
            return null;
        }

        public static List<Pet> GetAllPets()
        {
            var pets = new List<Pet>();
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT * FROM pets;", conn))
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
            catch (NpgsqlException)
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
                    using (var cmd = new NpgsqlCommand($"SELECT * FROM pets WHERE pet_id={id}", conn))
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
            catch (NpgsqlException)
            {
                // Handle exception
            }
            return new Pet(0, "Unknown", "Unknown");
        }

        public static int CreatePetByName(string petName)
        {
            string sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            try
            {
                using (var conn = CreateDataConn())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (NpgsqlException)
            {
                // Handle exception
            }
            return 0;
        }

        public static async Task EnsureDatabaseSetupAsync()
        {
            using var connection = CreateDataConn();
            await connection.OpenAsync();
            var setupPetsTableCommand = new NpgsqlCommand(
                @"CREATE TABLE IF NOT EXISTS pets (
                    pet_id SERIAL PRIMARY KEY,
                    pet_name VARCHAR(100) NOT NULL,
                    owner VARCHAR(100) NOT NULL
                );", connection);
            await setupPetsTableCommand.ExecuteNonQueryAsync();
        }
    }

    public record Pet(int Id, string Name, string Owner);

    public record PetCreate(string Name);
}