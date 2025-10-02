using Microsoft.EntityFrameworkCore;
using NewRelicSampleApp.Controllers;
using NewRelicSampleApp.Data;
using SampleApp.Common;
using SampleApp.Common.Controllers;
using SampleApp.Common.Models;

namespace NewRelicSampleApp
{
    public class EFCoreSqliteStartup : BaseStartup
    {
        private readonly DatabaseService _databaseService;
        private readonly NewRelicPetsController _petsController;

        /// <summary>
        /// Constructor that initializes the database service and controller
        /// </summary>
        public EFCoreSqliteStartup()
        {
            _databaseService = new DatabaseService();
            _petsController = new NewRelicPetsController(_databaseService);
        }

        /// <summary>
        /// Gets the connection string for the database
        /// </summary>
        protected override string ConnectionString => DatabaseService.ConnectionString;

        /// <summary>
        /// Gets the pets controller
        /// </summary>
        protected override BasePetsController PetsController => _petsController;

        /// <summary>
        /// Configures services for the application
        /// </summary>
        /// <param name="services">The service collection</param>
        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Add DbContext to the services
            services.AddDbContext<PetDbContext>(options =>
                options.UseSqlite(DatabaseService.ConnectionString));
        }

        /// <summary>
        /// Configures the database connection
        /// </summary>
        /// <param name="app">The application builder</param>
        protected override void ConfigureDatabase(IApplicationBuilder app)
        {
            // For testing purposes, use a file-based SQLite database instead of in-memory
            DatabaseService.ConnectionString = "Data Source=pets.db";
        }

        /// <summary>
        /// Ensures the database is set up by applying migrations
        /// </summary>
        protected override async Task EnsureDatabaseSetupAsync()
        {
            using var service = new DatabaseService();

            // Apply migrations instead of EnsureCreated
            await service.DbContext.Database.MigrateAsync();

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
    }
}
