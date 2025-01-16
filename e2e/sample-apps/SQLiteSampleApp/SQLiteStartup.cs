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
            DatabaseService.ConnectionString = "Data Source=:memory:;Cache=Shared";
        }

        protected override Task EnsureDatabaseSetupAsync()
        {
            return DatabaseService.EnsureDatabaseSetupAsync();
        }
    }
}
