using Microsoft.EntityFrameworkCore;
using SampleApp.Common.Models;

namespace NewRelicSampleApp.Data
{
    /// <summary>
    /// DbContext for the Pet entity used in the EF Core SQLite sample app
    /// </summary>
    public class PetDbContext : DbContext
    {
        /// <summary>
        /// DbSet for Pets
        /// </summary>
        public DbSet<Pet> Pets { get; set; }

        /// <summary>
        /// Constructor that accepts DbContextOptions
        /// </summary>
        /// <param name="options">The options to be used by the DbContext</param>
        public PetDbContext(DbContextOptions<PetDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Configures the model for the Pet entity
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pet>(entity =>
            {
                entity.ToTable("pets");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("pet_id").ValueGeneratedOnAdd();
                entity.Property(e => e.Name).HasColumnName("pet_name").IsRequired();
                entity.Property<string>("Owner").HasColumnName("owner").IsRequired().HasDefaultValue("Aikido Security");
            });
        }
    }
}
