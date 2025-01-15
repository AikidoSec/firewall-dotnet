using Microsoft.Extensions.DependencyInjection;
using SampleApp.Common;
using SampleApp.Common.Controllers;
using MySqlSampleApp.Controllers;

namespace MySqlSampleApp
{
    /// <summary>
    /// MySQL-specific implementation of the base startup class
    /// </summary>
    public class MySqlStartup : BaseStartup
    {
        private readonly DatabaseService _databaseService;
        private readonly MySqlPetsController _petsController;

        public MySqlStartup()
        {
            _databaseService = new DatabaseService();
            _petsController = new MySqlPetsController(_databaseService);
        }

        protected override string ConnectionString => DatabaseService.ConnectionString;
        protected override BasePetsController PetsController => _petsController;

        protected override void ConfigureDatabase(IApplicationBuilder app)
        {
            var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("MySqlConnection");
            DatabaseService.ConnectionString = connectionString;
        }

        protected override Task EnsureDatabaseSetupAsync()
        {
            return DatabaseService.EnsureDatabaseSetupAsync();
        }
    }
}
