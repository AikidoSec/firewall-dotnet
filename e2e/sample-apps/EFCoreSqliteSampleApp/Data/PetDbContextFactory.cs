using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EFCoreSqliteSampleApp.Data
{
    /// <summary>
    /// Factory class for creating PetDbContext instances at design time
    /// Required for EF Core migrations
    /// </summary>
    public class PetDbContextFactory : IDesignTimeDbContextFactory<PetDbContext>
    {
        /// <summary>
        /// Creates a new instance of PetDbContext
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>A new instance of PetDbContext</returns>
        public PetDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PetDbContext>();
            optionsBuilder.UseSqlite("Data Source=pets.db");

            return new PetDbContext(optionsBuilder.Options);
        }
    }
}
