using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SampleApp.Common;
using SampleApp.Common.Controllers;
using SqlServerSampleApp.Controllers;

namespace SqlServerSampleApp
{
    /// <summary>
    /// SQL Server-specific implementation of the base startup class
    /// </summary>
    public class SqlServerStartup : BaseStartup
    {
        private readonly SqlServerPetsController _petsController;

        public SqlServerStartup()
        {
            _petsController = new SqlServerPetsController();
        }

        protected override string ConnectionString => DatabaseService.ConnectionString;
        protected override BasePetsController PetsController => _petsController;

        protected override void ConfigureDatabase(IApplicationBuilder app)
        {
            var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("DefaultConnection");
            DatabaseService.ConnectionString = connectionString;

            // Ensure database is set up immediately during startup
            DatabaseService.EnsureDatabaseSetupAsync().GetAwaiter().GetResult();
        }

        protected override Task EnsureDatabaseSetupAsync()
        {
            // Already handled in ConfigureDatabase
            return Task.CompletedTask;
        }
    }
}
