using Microsoft.EntityFrameworkCore;
using SampleApp.Common.Models;
using EFCoreSqliteSampleApp.Data;
namespace EFCoreSqliteSampleApp
{
    /// <summary>
    /// Service for interacting with the SQLite database using EF Core
    /// </summary>
    public class DatabaseService : IDisposable
    {
        /// <summary>
        /// The database context instance
        /// </summary>
        public PetDbContext DbContext { get; private set; }

        public static string ConnectionString { get; set; } = string.Empty;
        private bool _disposed;

        /// <summary>
        /// Constructor that creates a new instance of PetDbContext
        /// </summary>
        public DatabaseService()
        {
            var optionsBuilder = new DbContextOptionsBuilder<PetDbContext>();
            optionsBuilder.UseSqlite(ConnectionString);
            DbContext = new PetDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// Retrieves all pets from the database
        /// </summary>
        /// <returns>A list of Pet objects</returns>
        public static List<Pet> GetAllPets()
        {
            using var service = new DatabaseService();
            return service.DbContext.Pets.ToList();
        }

        /// <summary>
        /// Retrieves a pet by its ID from the database using ExecuteRawSql
        /// </summary>
        /// <param name="id">The ID of the pet</param>
        /// <returns>A Pet object</returns>
        public static async Task<Pet> GetPetById(int id)
        {
            using var service = new DatabaseService();
            // Use ExecuteRawSql to test our patching
            var sql = $"SELECT * FROM pets WHERE pet_id = {id}";
            var pets = await service.DbContext.Pets.FromSqlRaw<Pet>(sql).ToListAsync();
            return pets.FirstOrDefault() ?? new Pet(0, "Unknown");
        }


        /// <summary>
        /// Creates a new pet with the given name in the database using ExecuteRawSql (non-async)
        /// </summary>
        /// <param name="petName">The name of the pet</param>
        /// <returns>The number of rows affected</returns>
        public static int CreatePetByName(string petName)
        {
            using var service = new DatabaseService();

            // Using ExecuteRawSql with string interpolation (bad practice, intentional for testing)
            var sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";

            // Execute the insert and then query the newly inserted pet to return it
            var task = service.DbContext.Database.ExecuteSqlRawAsync(sql);
            task.Wait();

            return task.Result;
        }

        /// <summary>
        /// Ensures that the database and tables are created
        /// </summary>
        public static async Task EnsureDatabaseSetupAsync()
        {
            using var service = new DatabaseService();
            await service.DbContext.Database.EnsureCreatedAsync();

            // Add sample data if none exists
            if (!service.DbContext.Pets.Any())
            {
                service.DbContext.Pets.AddRange(
                    new Pet(0, "Fluffy"),
                    new Pet(0, "Buddy"),
                    new Pet(0, "Max")
                );
                await service.DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Executes a raw SQL query with the given SQL command
        /// </summary>
        /// <param name="sql">The SQL command to execute</param>
        /// <returns>The number of rows affected</returns>
        public async Task<int> ExecuteRawSqlAsync(string sql)
        {
            return await DbContext.Database.ExecuteSqlRawAsync(sql);
        }

        /// <summary>
        /// Executes a raw SQL query with the given SQL command
        /// </summary>
        /// <param name="sql">The SQL command to execute</param>
        /// <returns>The number of rows affected</returns>
        public int ExecuteRawSql(string sql)
        {
            return DbContext.Database.ExecuteSqlRaw(sql);
        }

        /// <summary>
        /// Disposes of the database context
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the database context
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    DbContext?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
