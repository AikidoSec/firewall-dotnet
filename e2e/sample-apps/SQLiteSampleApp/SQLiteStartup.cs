using Microsoft.Extensions.DependencyInjection;
using SampleApp.Common;
using SampleApp.Common.Controllers;
using SQLiteSampleApp.Controllers;

namespace SQLiteSampleApp
{
    /// <summary>
    /// SQLite-specific implementation of the base startup class
    /// </summary>
    public class SQLiteStartup : BaseStartup
    {
        private readonly DatabaseService _databaseService;
        private readonly SQLitePetsController _petsController;

        public SQLiteStartup()
        {
            _databaseService = new DatabaseService();
            _petsController = new SQLitePetsController(_databaseService);
        }

        protected override string ConnectionString => DatabaseService.ConnectionString;
        protected override BasePetsController PetsController => _petsController;

        protected override void ConfigureDatabase(IApplicationBuilder app)
        {
            // SQLite uses a fixed in-memory connection string
            var connectionString = "Data Source=SharedSQLiteDB;Mode=Memory;Cache=Shared"; // Use a named shared DB

            // Initialize the shared connection FIRST
            DatabaseService.InitializeSharedConnection(connectionString);

            // Ensure the database schema is set up using the now-open connection
            EnsureDatabaseSetupAsync().GetAwaiter().GetResult();
        }

        protected override Task EnsureDatabaseSetupAsync()
        {
            return DatabaseService.EnsureDatabaseSetupAsync();
        }
    }
}
