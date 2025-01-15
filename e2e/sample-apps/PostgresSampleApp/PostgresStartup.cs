using Microsoft.Extensions.DependencyInjection;
using SampleApp.Common;
using SampleApp.Common.Controllers;
using PostgresSampleApp.Controllers;

namespace PostgresSampleApp
{
    /// <summary>
    /// PostgreSQL-specific implementation of the base startup class
    /// </summary>
    public class PostgresStartup : BaseStartup
    {
        private readonly DatabaseService _databaseService;
        private readonly PostgresPetsController _petsController;

        public PostgresStartup()
        {
            _databaseService = new DatabaseService();
            _petsController = new PostgresPetsController(_databaseService);
        }

        protected override string ConnectionString => DatabaseService.ConnectionString;
        protected override BasePetsController PetsController => _petsController;

        protected override void ConfigureDatabase(IApplicationBuilder app)
        {
            var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("PostgresConnection");
            DatabaseService.ConnectionString = connectionString;
        }

        protected override Task EnsureDatabaseSetupAsync()
        {
            return DatabaseService.EnsureDatabaseSetupAsync();
        }
    }
}
