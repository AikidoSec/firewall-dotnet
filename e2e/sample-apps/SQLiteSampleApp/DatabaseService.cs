using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SampleApp.Common.Models;

namespace SQLiteSampleApp
{
    /// <summary>
    /// Service for interacting with the SQLite database.
    /// Manages a persistent connection for the shared in-memory database.
    /// </summary>
    public class DatabaseService : IDisposable
    {
        // Hold the single, persistent connection
        private static SqliteConnection _sharedConnection;
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        // Connection string should only be set once during initialization
        public static string ConnectionString { get; private set; }

        /// <summary>
        /// Initializes the shared database connection. Must be called once at startup.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string (should include Cache=Shared).</param>
        public static void InitializeSharedConnection(string connectionString)
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    // Avoid re-initializing or changing the connection string
                    if (ConnectionString != connectionString)
                    {
                        // Log warning or throw? For sample app, maybe just log.
                        Console.WriteLine("Warning: Attempted to re-initialize DatabaseService with a different connection string. Ignoring.");
                    }
                    return;
                }

                if (string.IsNullOrEmpty(connectionString) || !connectionString.Contains("Cache=Shared"))
                {
                    throw new ArgumentException("Connection string must be provided and contain 'Cache=Shared' for in-memory databases.", nameof(connectionString));
                }

                ConnectionString = connectionString;
                try
                {
                    _sharedConnection = new SqliteConnection(ConnectionString);
                    _sharedConnection.Open(); // Keep the connection open
                    _isInitialized = true;
                    Console.WriteLine("DatabaseService initialized and connection opened.");
                }
                catch (SqliteException e)
                {
                    Console.WriteLine($"FATAL: Failed to open initial SQLite connection: {e}");
                    // Depending on the app, might want to clean up or prevent startup
                    _sharedConnection?.Dispose();
                    throw; // Re-throw to indicate critical failure
                }
            }
        }


        /// <summary>
        /// Returns the shared, open connection to the SQLite database.
        /// Throws an exception if not initialized.
        /// </summary>
        /// <returns>The shared SqliteConnection object</returns>
        private static SqliteConnection GetOpenConnection()
        {
            if (!_isInitialized || _sharedConnection == null || _sharedConnection.State != ConnectionState.Open)
            {
                // This indicates a problem in the setup flow
                throw new InvalidOperationException("DatabaseService is not initialized or the connection is closed/broken. Ensure InitializeSharedConnection is called at startup.");
            }
            return _sharedConnection;
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
                using (var cmd = new SqliteCommand("SELECT * FROM pets;", GetOpenConnection()))
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
            catch (SqliteException e)
            {
                Console.WriteLine($"Error in GetAllPets: {e.Message}");
                // Depending on requirements, might re-throw or return empty list
            }
            return pets;
        }

        /// <summary>
        /// Retrieves a pet by its ID from the database.
        /// </summary>
        /// <param name="id">The ID of the pet</param>
        /// <returns>A Pet object or null if not found</returns>
        public static Pet GetPetById(int id)
        {
            try
            {
                // Reverted to string concatenation for SQL injection testing
                using (var cmd = new SqliteCommand($"SELECT * FROM pets WHERE pet_id={id}", GetOpenConnection()))
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
            catch (SqliteException e)
            {
                Console.WriteLine($"Error in GetPetById: {e.Message}");
            }
            return null; // Return null instead of a dummy pet
        }

        /// <summary>
        /// Creates a new pet with the given name in the database.
        /// </summary>
        /// <param name="petName">The name of the pet</param>
        /// <returns>The number of rows affected</returns>
        public static int CreatePetByName(string petName)
        {
            // Reverted to string concatenation for SQL injection testing
            string sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            try
            {
                using (var cmd = new SqliteCommand(sql, GetOpenConnection()))
                {
                    // Removed parameterization
                    return cmd.ExecuteNonQuery();
                }
            }
            catch (SqliteException e)
            {
                Console.WriteLine($"Error in CreatePetByName: {e.Message}");
                // Check for specific errors like "no such table" if needed
                if (e.SqliteErrorCode == 1) // SQLITE_ERROR
                {
                    Console.WriteLine("Attempted to insert into 'pets' table, but it seems the table doesn't exist. Check initialization.");
                }
            }
            return 0;
        }

        /// <summary>
        /// Ensures that the database and tables are created using the shared connection.
        /// </summary>
        public static async Task EnsureDatabaseSetupAsync()
        {
            var connection = GetOpenConnection(); // Use the already open shared connection
            var setupPetsTableCommand = new SqliteCommand(
                @"CREATE TABLE IF NOT EXISTS pets (
                    pet_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    pet_name TEXT NOT NULL,
                    owner TEXT NOT NULL
                );", connection); // Pass the shared connection
            try
            {
                await setupPetsTableCommand.ExecuteNonQueryAsync();
                Console.WriteLine("'pets' table checked/created successfully.");
            }
            catch (SqliteException e)
            {
                Console.WriteLine($"Error during EnsureDatabaseSetupAsync: {e.Message}");
                // This is critical, might want to throw or handle differently
                throw;
            }
        }

        /// <summary>
        /// Disposes the shared connection. Should be called on application shutdown.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lock) // Ensure thread safety during disposal
                {
                    if (_sharedConnection != null)
                    {
                        Console.WriteLine("Closing and disposing shared SQLite connection.");
                        _sharedConnection.Close();
                        _sharedConnection.Dispose();
                        _sharedConnection = null;
                        _isInitialized = false;
                    }
                }
            }
        }

        // Optional: Finalizer for safety, though explicit Dispose is preferred.
        ~DatabaseService()
        {
            Dispose(false);
        }
    }
}
