using SampleApp.Common.Controllers;
using SampleApp.Common.Models;

namespace EFCoreSqliteSampleApp.Controllers
{
    /// <summary>
    /// EF Core SQLite implementation of the pets controller
    /// </summary>
    public class EFCoreSqlitePetsController : BasePetsController
    {
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// Constructor that accepts a database service
        /// </summary>
        /// <param name="databaseService">The database service to use</param>
        public EFCoreSqlitePetsController(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public override IEnumerable<Pet> GetAllPets()
        {
            return DatabaseService.GetAllPets();
        }

        public override Pet GetPetById(int id)
        {
            return DatabaseService.GetPetById(id).GetAwaiter().GetResult();
        }

        public override int CreatePetByName(string name)
        {
            return DatabaseService.CreatePetByName(name);
        }

        public Task<int> ExecuteRawSqlAsync(string petName)
        {
            var sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            return _databaseService.ExecuteRawSqlAsync(sql);
        }

        public int ExecuteRawSql(string petName)
        {
            var sql = $"INSERT INTO pets (pet_name, owner) VALUES ('{petName}', 'Aikido Security')";
            return _databaseService.ExecuteRawSql(sql);
        }

        public override void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            base.ConfigureEndpoints(endpoints);

            endpoints.MapGet("/api/pets/execute-raw-sql", async (HttpContext context) =>
            {
                var sql = context.Request.Query["sql"].ToString();

                if (string.IsNullOrEmpty(sql))
                {
                    return Results.BadRequest("SQL query is required");
                }

                var result = ExecuteRawSql(sql);
                return Results.Ok(new { RowsAffected = result });
            });

            endpoints.MapGet("/api/pets/execute-raw-sql-async", async (HttpContext context) =>
            {
                var sql = context.Request.Query["sql"].ToString();

                if (string.IsNullOrEmpty(sql))
                {
                    return Results.BadRequest("SQL query is required");
                }

                var result = await ExecuteRawSqlAsync(sql);
                return Results.Ok(new { RowsAffected = result });
            });
        }
    }
}
